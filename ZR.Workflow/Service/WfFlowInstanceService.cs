using ZR.Workflow.Helper;

namespace ZR.Workflow.Service
{
    /// <summary>
    /// 流程实例服务（我发起的视角）。
    ///
    /// 职责分层：
    /// <list type="bullet">
    /// <item><b>列表 / 详情</b>：申请人维度的实例查询与详情组装，兜底关联流程定义/节点/任务的展示字段。</item>
    /// <item><b>状态变更</b>：发起（<see cref="Start"/>）与驳回后重新提交（<see cref="Resubmit"/>）——
    /// 仅做"DTO → 实体"转换与用户上下文注入，落库/状态机推进全部委托给 <see cref="IWfEngineService"/>。</item>
    /// <item><b>统计</b>：数据面板角标与流程效率聚合，全部为只读 Count/Average，GroupBy 一次拿全部状态指标。</item>
    /// </list>
    ///
    /// 关键约束：
    /// <list type="bullet">
    /// <item>展示用名称（流程名/节点名/审批人昵称/申请人昵称）一律按落库快照读取，运行时不再 JOIN 用户表/定义表，
    /// 仅在快照为空时按 Id 反查兜底（如已结束实例的 FlowName 兜底）。</item>
    /// <item>状态变更类公共方法签名稳定后即不轻易改动，扩展行为优先在私有辅助里加，遵循
    /// <see cref="WfEngineService"/> 已锁定的"pre-flight → RunInTx → ArriveNode/AdvanceToNext"模式。</item>
    /// <item>性能敏感路径（<see cref="FillApprovers"/>、<see cref="GetEfficiencyStats"/>）必须 O(N) 或单次聚合，
    /// 避免 N+1 嵌套扫描；大表下任何 N*M 都是隐形坑。</item>
    /// </list>
    /// </summary>
    [AppService(ServiceType = typeof(IWfFlowInstanceService))]
    public class WfFlowInstanceService : BaseService<WfFlowInstance>, IWfFlowInstanceService
    {
        private readonly IWfEngineService _engine;
        private readonly IWfAiService _aiService;

        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public WfFlowInstanceService(IWfEngineService engine, IWfAiService aiService)
        {
            _engine = engine;
            _aiService = aiService;
        }

        #region 列表

        /// <summary>
        /// 查询我的待办/已办/我发起/抄送的流程实例列表，按申请时间倒序分页返回。
        /// </summary>
        /// <param name="parm">查询条件（标题/状态/流程定义）</param>
        /// <param name="userId">当前用户 Id（按 <c>ApplyUserId</c> 关联）</param>
        /// <param name="allUser">是否查询全部用户的流程（管理员视角）。为 true 时跳过"仅本人"过滤，
        /// 但 parm.ApplyUserId 有值则按指定申请人过滤；为 false 时始终只看当前用户。</param>
        public PagedInfo<WfFlowInstanceDto> GetMyList(WfFlowInstanceQueryDto parm, long userId, bool allUser = false)
        {
            // 非管理员只看自己；管理员(allUser)看全部，可再按 ApplyUserId 二次筛选
            var applyUserId = allUser ? parm.ApplyUserId : userId;
            var predicate = Expressionable.Create<WfFlowInstance>()
                .AndIF(applyUserId != null, t => t.ApplyUserId == applyUserId)
                .AndIF(!string.IsNullOrEmpty(parm.Title), t => t.Title.Contains(parm.Title))
                .AndIF(parm.Status != null, t => t.Status == parm.Status)
                .AndIF(parm.FlowId != null, t => t.FlowId == parm.FlowId);

            var paged = Queryable().Where(predicate.ToExpression())
                .ToPage<WfFlowInstance, WfFlowInstanceDto>(parm);

            // 三个填充器互不依赖，固定顺序：FlowName → CurrentNodeName → Approvers
            // 前两个是按 Id 关联定义/节点表的"快照兜底"，第三个是任务表聚合
            FillFlowName(paged.Result);
            FillCurrentNode(paged.Result);
            FillApprovers(paged.Result);
            return paged;
        }

        /// <summary>
        /// 兜底填充流程名：实例上冗余的 <c>FlowName</c> 为空时，按 <c>FlowId</c> 关联 <see cref="WfFlowDefinition"/> 取一次。
        /// </summary>
        private void FillFlowName(List<WfFlowInstanceDto> list)
        {
            if (list == null || list.Count == 0) return;
            var needIds = list.Where(x => string.IsNullOrEmpty(x.FlowName))
                .Select(x => x.FlowId).Distinct().ToList();
            if (needIds.Count == 0) return;

            var map = Context.Queryable<WfFlowDefinition>()
                .Where(d => needIds.Contains(d.FlowId))
                .ToList()
                .ToDictionary(d => d.FlowId, d => d.FlowName);
            foreach (var it in list)
            {
                if (string.IsNullOrEmpty(it.FlowName) && map.TryGetValue(it.FlowId, out var fn))
                    it.FlowName = fn;
            }
        }

        /// <summary>
        /// 按活动节点集合（<c>CurrentNodeIds</c>，空则回退 <c>CurrentNodeId</c>）关联 <see cref="WfFlowNode"/> 填充当前节点名称；
        /// 并行网关下多个活动节点用"、"联接展示，已结束/无当前节点则不填。
        /// </summary>
        private void FillCurrentNode(List<WfFlowInstanceDto> list)
        {
            if (list == null || list.Count == 0) return;
            // 一次性汇总全部实例需要兜底的节点 Id（含集合回退单值），批量查询避免 N+1
            var needIds = list
                .Where(x => string.IsNullOrEmpty(x.CurrentNodeName))
                .SelectMany(x => ParseActiveNodeIds(x.CurrentNodeIds, x.CurrentNodeId))
                .Distinct()
                .ToList();
            if (needIds.Count == 0) return;

            var map = Context.Queryable<WfFlowNode>()
                .Where(n => needIds.Contains(n.NodeId))
                .ToList()
                .ToDictionary(n => n.NodeId, n => n.NodeName);
            foreach (var it in list)
            {
                if (string.IsNullOrEmpty(it.CurrentNodeName))
                {
                    var ids = ParseActiveNodeIds(it.CurrentNodeIds, it.CurrentNodeId);
                    var names = ids.Where(id => map.ContainsKey(id)).Select(id => map[id]);
                    it.CurrentNodeName = string.Join("、", names);
                }
            }
        }

        /// <summary>
        /// 填充审批人：进行中实例取当前待审任务审批人；已结束/撤回实例取全部参与审批人。
        /// 直接读取任务表的昵称快照，免运行时关联用户表。
        ///
        /// 性能：实例 × 任务全表嵌套是 O(N*M)，改用 <c>GroupBy</c> + <c>Dictionary</c> 一次性索引到 O(N+M)。
        /// </summary>
        private void FillApprovers(List<WfFlowInstanceDto> list)
        {
            if (list == null || list.Count == 0) return;
            var instanceIds = list.Select(x => x.InstanceId).Distinct().ToList();

            // 一次性按 InstanceId 分组，转 Dictionary 索引；任务表通常比实例表大得多，
            // 避免 foreach 内层再做 Where 全扫描
            var taskByInstance = Context.Queryable<WfFlowTask>()
                .Where(t => instanceIds.Contains(t.InstanceId))
                .ToList()
                .GroupBy(t => t.InstanceId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var it in list)
            {
                if (!taskByInstance.TryGetValue(it.InstanceId, out var ownTasks))
                {
                    it.Approvers = string.Empty;
                    continue;
                }
                // 进行中：当前待审人（昵称快照）；结束/撤回：全部参与人
                var isInProgress = it.Status == (int)WfInstanceStatus.Approval;
                var approvers = (isInProgress
                        ? ownTasks.Where(t => t.Status == (int)WfTaskStatus.Pending)
                        : ownTasks)
                    .SelectMany(t => t.AssigneeNickName.SplitByComma())
                    .Distinct();
                it.Approvers = string.Join(",", approvers);
            }
        }

        #endregion

        #region 详情

        /// <summary>
        /// 实例详情：含基础信息、当前节点名、任务列表、记录列表。
        /// 申请人/操作人昵称已在落库时快照（<c>ApplyNickName</c> / <c>OperatorNickName</c>），直接随 DTO 返回，无需运行时关联用户表。
        ///
        /// 性能说明：当前 4 次查询（实例 / 流程定义 / 当前节点 / 任务 / 记录），
        /// 任务/记录总量可控时无 N+1 风险；若未来详情页要展示评论附件等大字段，可考虑改用
        /// <c>Queryable().Includes(t => t.Tasks).Includes(r => r.Records)</c> 一次拉取。
        ///
        /// 表单字段权限：按查看者视角（viewerId）对 FormContent 做字段级过滤。
        /// - 申请人为本人、实例非审批中（已通过/驳回/撤回/结束）或节点未配置权限 → 全放开；
        /// - 当前审批人 → 按该节点 FieldPermission 剔除隐藏字段，并返回权限视图供前端控制只读/可编辑。
        /// </summary>
        public WfFlowInstanceDto GetInfo(long instanceId, long? viewerId = null)
        {
            var inst = Queryable().First(i => i.InstanceId == instanceId);
            if (inst == null) return null;
            var dto = inst.Adapt<WfFlowInstanceDto>();
            if (string.IsNullOrEmpty(dto.FlowName))
            {
                var def = Context.Queryable<WfFlowDefinition>().First(d => d.FlowId == inst.FlowId);
                if (def != null) dto.FlowName = def.FlowName;
            }
            // 当前活动节点集合优先（并行网关下可能多个），按 nodeId 批量关联节点名，多个用"、"联接；
            // 集合为空或不可解析时回退单值 CurrentNodeId 兼容旧数据
            var activeNodeIds = ParseActiveNodeIds(inst.CurrentNodeIds, inst.CurrentNodeId);
            if (activeNodeIds.Count > 0)
            {
                var nodeMap = Context.Queryable<WfFlowNode>()
                    .Where(n => activeNodeIds.Contains(n.NodeId))
                    .ToList()
                    .ToDictionary(n => n.NodeId, n => n.NodeName);
                dto.CurrentNodeName = string.Join("、", activeNodeIds
                    .Where(id => nodeMap.TryGetValue(id, out _))
                    .Select(id => nodeMap[id]));
            }
            dto.Tasks = Context.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == instanceId)
                .OrderBy(t => t.TaskId)
                .ToList()
                .Adapt<List<WfFlowTaskDto>>();
            dto.Records = Context.Queryable<WfFlowRecord>()
                .Where(r => r.InstanceId == instanceId)
                .OrderBy(r => r.RecordId)
                .ToList()
                .Adapt<List<WfFlowRecordDto>>();
            // 审批记录按 NodeId 关联节点名（节点名称展示用，WfFlowRecord 仅存 NodeId 不冗余名称）
            var recordNodeIds = dto.Records
                .Where(r => r.NodeId.HasValue)
                .Select(r => r.NodeId.Value)
                .Distinct()
                .ToList();
            if (recordNodeIds.Count > 0)
            {
                var nodes = Context.Queryable<WfFlowNode>()
                    .Where(n => recordNodeIds.Contains(n.NodeId))
                    .ToList();
                var nodeNameMap = nodes.ToDictionary(n => n.NodeId, n => n.NodeName);
                foreach (var r in dto.Records)
                {
                    if (r.NodeId.HasValue && nodeNameMap.TryGetValue(r.NodeId.Value, out var nn))
                    {
                        r.NodeName = nn;
                    }
                }
            }
            ApplyFieldPermission(dto, inst, viewerId);
            return dto;
        }

        /// <summary>
        /// AI 审批链汇总：读取某实例的标题/状态/表单与全部审批记录，组装成审批链上下文文本，
        /// 交由 <see cref="IWfAiService.SummarizeApprovalChainAsync"/> 生成审批全过程结论/风险/建议。
        /// 数据组装在本类（有 Context）完成，AI 服务保持纯编排，避免 Service 层循环依赖。
        /// </summary>
        public async Task<WfAiInstanceSummaryResult> SummarizeInstance(long instanceId, long viewerId, bool adminView = false)
        {
            if (instanceId <= 0)
            {
                throw new CustomException("流程实例 Id 不能为空");
            }

            var inst = await Queryable().FirstAsync(i => i.InstanceId == instanceId)
                ?? throw new CustomException("流程实例不存在");

            await EnsureInstanceReadable(inst, viewerId, adminView);

            var records = await Context.Queryable<WfFlowRecord>()
                .LeftJoin<WfFlowNode>((r, n) => r.NodeId == n.NodeId)
                .Where(r => r.InstanceId == instanceId)
                .OrderBy(r => r.RecordId)
                .Select((r, n) => new { r, NodeName = n.NodeName })
                .ToListAsync();

            var recordsDesc = string.Join("\n", records.Select(x =>
                $"- {x.r.Create_time:yyyy-MM-dd HH:mm} {x.r.OperatorNickName ?? x.r.Operator} 执行「{ActionName(x.r.Action)}」节点「{x.NodeName}」意见：{(string.IsNullOrWhiteSpace(x.r.Opinion) ? "（无）" : x.r.Opinion)}"));
            if (string.IsNullOrWhiteSpace(recordsDesc))
            {
                recordsDesc = "（暂无审批记录）";
            }

            var formItems = await GetFormItemsAsync(inst.FlowId);
            var form = await BuildFormTextWithAttachmentsAsync(inst.FormContent, formItems, inst.AttachmentParsed);
            var context = $"申请标题：{inst.Title}\n流程名称：{inst.FlowName}\n最终状态：{InstanceStatusName(inst.Status)}\n表单内容：\n{form.FormText}\n审批链记录：\n{recordsDesc}";

            return await _aiService.SummarizeApprovalChainAsync(context);
        }

        /// <summary>
        /// AI 审批风险预判：读取某待办任务对应的实例表单与审批链，站在当前节点审批人视角生成风险提示。
        /// 数据组装在本类（有 Context）完成，AI 服务保持纯编排，避免循环依赖。
        /// </summary>
        public async Task<WfAiRiskCheckResult> TaskRiskCheck(long taskId, long operatorId)
        {
            if (taskId <= 0)
            {
                throw new CustomException("审批任务 Id 不能为空");
            }

            var task = await Context.Queryable<WfFlowTask>().FirstAsync(t => t.TaskId == taskId)
                ?? throw new CustomException("审批任务不存在");

            // 越权防护：仅该任务的审批人/被委托人可发起（对齐引擎 Audit 的 AssigneeId/DelegateId 口径）
            if (task.AssigneeId != operatorId && task.DelegateId != operatorId)
            {
                throw new CustomException("仅该任务审批人可执行风险预判");
            }

            var inst = await Context.Queryable<WfFlowInstance>().FirstAsync(i => i.InstanceId == task.InstanceId)
                ?? throw new CustomException("流程实例不存在");

            var records = await Context.Queryable<WfFlowRecord>()
                .LeftJoin<WfFlowNode>((r, n) => r.NodeId == n.NodeId)
                .Where(r => r.InstanceId == inst.InstanceId)
                .OrderBy(r => r.RecordId)
                .Select((r, n) => new { r, NodeName = n.NodeName })
                .ToListAsync();

            var recordsDesc = string.Join("\n", records.Select(x =>
                $"- {x.r.Create_time:yyyy-MM-dd HH:mm} {x.r.OperatorNickName ?? x.r.Operator} 执行「{ActionName(x.r.Action)}」节点「{x.NodeName}」意见：{(string.IsNullOrWhiteSpace(x.r.Opinion) ? "（无）" : x.r.Opinion)}"));
            if (string.IsNullOrWhiteSpace(recordsDesc))
            {
                recordsDesc = "（暂无审批记录）";
            }

            var formItems = await GetFormItemsAsync(inst.FlowId);
            var form = await BuildFormTextWithAttachmentsAsync(inst.FormContent, formItems, inst.AttachmentParsed);
            var context = $"申请标题：{inst.Title}\n流程名称：{inst.FlowName}\n当前审批节点：{task.NodeName}\n表单内容：\n{form.FormText}\n已发生的审批链记录：\n{recordsDesc}";

            return await _aiService.RiskCheckAsync(context, form.ImageUrls);
        }

        /// <summary>
        /// 实例 AI 视角读取权限：管理员、申请人或该实例任务的审批人/被委托人可读，防止凭 Id 越权拉取他人申请内容。
        /// </summary>
        private async Task EnsureInstanceReadable(WfFlowInstance inst, long viewerId, bool adminView)
        {
            if (adminView || inst.ApplyUserId == viewerId) return;

            var involved = await Context.Queryable<WfFlowTask>()
                .AnyAsync(t => t.InstanceId == inst.InstanceId && (t.AssigneeId == viewerId || t.DelegateId == viewerId));
            if (!involved)
            {
                throw new CustomException("无权查看该流程实例的 AI 分析");
            }
        }

        /// <summary>动作数字 → 中文名（对齐 WfAction）。</summary>
        private static string ActionName(int action) => action switch
        {
            (int)WfAction.Submit => "提交",
            (int)WfAction.Approve => "通过",
            (int)WfAction.Reject => "驳回",
            (int)WfAction.Transfer => "转交",
            (int)WfAction.Withdraw => "撤回",
            (int)WfAction.AddSign => "加签",
            (int)WfAction.Resubmit => "重新提交",
            (int)WfAction.Cc => "抄送",
            (int)WfAction.AutoSkip => "自动跳过",
            (int)WfAction.RemoveSign => "减签",
            (int)WfAction.Delegate => "委托代审",
            _ => $"动作{action}"
        };

        /// <summary>实例状态数字 → 中文名（对齐 WfInstanceStatus）。</summary>
        private static string InstanceStatusName(int status) => status switch
        {
            (int)WfInstanceStatus.Approval => "审批中",
            (int)WfInstanceStatus.Approved => "通过",
            (int)WfInstanceStatus.Rejected => "驳回",
            (int)WfInstanceStatus.Withdrawn => "撤回",
            (int)WfInstanceStatus.Suspended => "已挂起",
            (int)WfInstanceStatus.Terminated => "已终止",
            _ => $"状态{status}"
        };

        /// <summary>
        /// 按流程 Id 读取定义表单字段配置（FormItems JSON）。AI 表单上下文相关取数的统一入口，
        /// 转换逻辑（<see cref="BuildFormTextWithAttachmentsAsync"/>）保持为不查库的静态纯方法。
        /// </summary>
        private async Task<string> GetFormItemsAsync(long flowId)
        {
            return await Context.Queryable<WfFlowDefinition>()
                .Where(d => d.FlowId == flowId)
                .Select(d => d.FormItems)
                .FirstAsync();
        }

        /// <summary>
        /// 组装供 AI 使用的表单上下文文本与图片 URL（静态纯方法，不做数据库查询）。
        /// 优先读取提交时异步填充的 AttachmentParsed（避免重复下载/抽取）；
        /// 为空（历史实例或未解析完成）时降级为实时抽取以兼容。
        /// formItems 由调用方经 <see cref="GetFormItemsAsync"/> 取得。
        /// 注意：降级分支内的附件下载属于本方法的固有取材（文件文本抽取），不属于数据环境依赖。
        /// </summary>
        private static async Task<(string FormText, List<string> ImageUrls)> BuildFormTextWithAttachmentsAsync(
            string formContent, string formItems, string attachmentParsed)
        {
            if (string.IsNullOrWhiteSpace(formContent)) return ("（表单为空）", new List<string>());

            var cached = WfAttachmentParser.Deserialize(attachmentParsed);
            if (cached.Count > 0)
            {
                // 命中提交时解析缓存：直接渲染，不再下载/抽取
                var lines = new List<string>();
                var imageUrls = new List<string>();
                var metaMap = WfFormTextHelper.ParseFieldLabels(formItems);
                Dictionary<string, string> values = null;
                try { values = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(formContent); }
                catch (Newtonsoft.Json.JsonException) { values = null; }

                if (values != null)
                {
                    foreach (var kv in values)
                    {
                        if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                        var isAttachment = cached.Any(c => !string.IsNullOrEmpty(c.Url) && kv.Value.Contains(c.Url));
                        if (isAttachment)
                        {
                            // 附件字段：图片占位 or 文件文本（来自缓存）
                            foreach (var c in cached.Where(c => !string.IsNullOrEmpty(c.Url) && kv.Value.Contains(c.Url)))
                            {
                                if (WfAttachmentParser.IsImage(c))
                                {
                                    imageUrls.Add(c.Url);
                                    lines.Add($"【图片附件：({imageUrls.Count}) {c.Url}】");
                                }
                                else if (!string.IsNullOrEmpty(c.Text))
                                {
                                    lines.Add($"【文件附件：{c.FileName}】\n{c.Text}");
                                }
                                else
                                {
                                    lines.Add($"【文件附件：{c.FileName}】（系统无法解析该文件内容，仅附文件名）");
                                }
                            }
                            continue;
                        }

                        var label = metaMap.TryGetValue(kv.Key, out var l) ? l : kv.Key;
                        var val = kv.Value;
                        lines.Add($"{label}：{val}");
                    }
                    return (string.Join("\n", lines), imageUrls);
                }
                // formContent 非法 JSON 时掉出缓存分支，走下方实时抽取兜底
            }

            // 降级：实时抽取（兼容历史实例）
            var result = await WfFormTextHelper.TranslateToTextWithAttachments(formContent, formItems).ConfigureAwait(false);
            return (result.FormText ?? formContent, result.ImageUrls ?? new List<string>());
        }

        /// <summary>
        /// 把某实例的 FormContent 翻译为"中文label：值"的多行文本（供 Controller/跨 Service 复用，
        /// 如审批意见建议 AI 需要把表单字段技术名换成中文属性名）。
        /// 按实例查流程定义 FormItems 翻译，失败回退原始 formContent。
        /// </summary>
        public async Task<string> TranslateFormContent(long instanceId, string formContent)
        {
            if (string.IsNullOrWhiteSpace(formContent)) return formContent;

            var inst = await Context.Queryable<WfFlowInstance>()
                .Where(i => i.InstanceId == instanceId)
                .FirstAsync()
                ?? throw new CustomException("流程实例不存在");
            var formItems = await Context.Queryable<WfFlowDefinition>()
                .Where(d => d.FlowId == inst.FlowId)
                .Select(d => d.FormItems)
                .FirstAsync();

            return WfFormTextHelper.TranslateToText(formContent, formItems) ?? formContent;
        }

        /// <summary>
        /// 读取实例提交时异步填充的附件解析结果（AttachmentParsed）。
        /// 供审批意见建议等 AI 场景在服务端取数，避免信任客户端传入值被伪造。
        /// </summary>
        public async Task<string> GetInstanceAttachmentParsed(long instanceId)
        {
            if (instanceId <= 0) return null;
            return await Queryable()
                .Where(i => i.InstanceId == instanceId)
                .Select(i => i.AttachmentParsed)
                .FirstAsync();
        }

        /// <summary>
        /// 按查看者视角对实例表单做字段级权限过滤：
        /// - 申请人本人 → AllEditable=true（可编辑全部，用于回填/修改）。
        /// - 实例非审批中（已通过/驳回/撤回/结束）或未传入查看者 → 全可见只读（ReadonlyFields/HiddenFields 空，历史实例直接放开）。
        /// - 当前审批人 → 取活动节点 FieldPermission：
        ///   节点未配置 → 全部字段默认可编辑（AllEditable=true）；
        ///   已配置 → perm=0 可编辑、perm=1 只读、perm=2 隐藏（FormContent 过滤掉隐藏字段），返回 ReadonlyFields/HiddenFields。
        /// </summary>
        private void ApplyFieldPermission(WfFlowInstanceDto dto, WfFlowInstance inst, long? viewerId)
        {
            if (inst == null || dto == null) return;
            var view = new WfFieldPermissionView();

            // 申请人本人 → 可编辑全部字段
            if (viewerId.HasValue && inst.ApplyUserId.HasValue && inst.ApplyUserId.Value == viewerId.Value)
            {
                view.AllEditable = true;
                dto.FieldPermissionView = view;
                return;
            }

            // 非审批中实例 / 未传入查看者 → 历史直接放开（全可见只读）
            if (!viewerId.HasValue || inst.Status != (int)WfInstanceStatus.Approval)
            {
                dto.FieldPermissionView = view;
                return;
            }

            var activeNodeIds = ParseActiveNodeIds(inst.CurrentNodeIds, inst.CurrentNodeId);
            if (activeNodeIds.Count == 0)
            {
                dto.FieldPermissionView = view;
                return;
            }

            // 汇总当前活动节点的字段权限（并行多活动节点取同字段最严格权限）
            var merged = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var hasAnyConfig = false;
            var nodes = Context.Queryable<WfFlowNode>()
                .Where(n => activeNodeIds.Contains(n.NodeId) && n.FieldPermission != null)
                .ToList();
            foreach (var n in nodes)
            {
                if (string.IsNullOrWhiteSpace(n.FieldPermission)) continue;
                hasAnyConfig = true;
                List<WfFieldPermissionItem> items;
                try
                {
                    items = JsonConvert.DeserializeObject<List<WfFieldPermissionItem>>(n.FieldPermission) ?? new List<WfFieldPermissionItem>();
                }
                catch (Exception)
                {
                    continue; // 配置损坏则忽略该节点，避免整单打不开
                }
                foreach (var it in items)
                {
                    if (string.IsNullOrWhiteSpace(it.Field)) continue;
                    var perm = NormalizePerm(it.Perm);
                    if (!merged.TryGetValue(it.Field, out var oldPerm))
                        merged[it.Field] = perm;
                    else
                        merged[it.Field] = Math.Max(oldPerm, perm); // 0<1<2，取最大=最严格
                }
            }

            // 节点未配置任何字段权限 → 全部字段默认可编辑
            if (!hasAnyConfig || merged.Count == 0)
            {
                view.AllEditable = true;
                dto.FieldPermissionView = view;
                return;
            }

            view.ReadonlyFields = merged.Where(kv => kv.Value == 1).Select(kv => kv.Key).ToList();
            view.HiddenFields = merged.Where(kv => kv.Value == 2).Select(kv => kv.Key).ToList();

            // 剔除隐藏字段：FormContent 排除 perm=2 的字段
            if (view.HiddenFields.Count > 0 && !string.IsNullOrWhiteSpace(inst.FormContent))
            {
                try
                {
                    var kv = JsonConvert.DeserializeObject<Dictionary<string, string>>(inst.FormContent);
                    if (kv != null)
                    {
                        var hidden = new HashSet<string>(view.HiddenFields, StringComparer.OrdinalIgnoreCase);
                        var filtered = kv.Where(p => !hidden.Contains(p.Key))
                            .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
                        dto.FormContent = JsonConvert.SerializeObject(filtered);
                    }
                }
                catch (Exception)
                {
                    // FormContent 非标准 JSON 时不过滤，交由前端容错
                }
            }

            dto.FieldPermissionView = view;
        }

        /// <summary>
        /// 规范化权限值：仅识别 0/1/2，其它值按 0（可编辑）处理。
        /// </summary>
        private static int NormalizePerm(int perm)
        {
            return perm == 1 || perm == 2 ? perm : 0;
        }

        /// <summary>
        /// 解析实例当前活动节点 Id 集合：优先读 <paramref name="currentNodeIds"/>（JSON 数组），
        /// 空/不可解析时回退单值 <paramref name="currentNodeId"/>，去重后返回。
        /// </summary>
        private List<long> ParseActiveNodeIds(string currentNodeIds, long? currentNodeId)
        {
            // 集合为空时回退单值 CurrentNodeId（兼容无并行活动/存量实例）
            if (string.IsNullOrWhiteSpace(currentNodeIds))
                return currentNodeId.HasValue ? [currentNodeId.Value] : [];
            return JsonConvert.DeserializeObject<long[]>(currentNodeIds)?.Distinct().ToList() ?? [];
        }

        #endregion

        #region 状态变更

        /// <summary>
        /// 发起申请。完成"DTO → 实体"转换、用户上下文（申请人/创建人/昵称）注入，
        /// 实际状态机/任务池推进委托给 <see cref="IWfEngineService.Start(WfFlowInstance)"/>。
        ///
        /// 注意：<c>Status</c> 在 Engine 内部会再次置为 <see cref="WfInstanceStatus.Approval"/>（防御性赋值），
        /// Service 层先 set 是为了避免 Engine 之前变更语义时漏处理。
        /// </summary>
        public long Start(WfFlowInstanceDto dto, LoginUser user)
        {
            var instance = dto.Adapt<WfFlowInstance>();
            
            instance.ApplyUser = user.UserName;
            instance.ApplyUserId = user.UserId;
            instance.ApplyNickName = user.NickName;
            instance.Status = (int)WfInstanceStatus.Approval;
            instance.Create_by = user.UserName;
            var instanceId = _engine.Start(instance);
            // 提交后异步解析附件并填充 AttachmentParsed，避免审批侧每次重新下载/抽取
            FillAttachmentParsedAsync(instanceId, instance.FlowId, instance.FormContent);
            return instanceId;
        }

        /// <summary>
        /// 提交后异步填充实例的 AttachmentParsed（附件解析结果）。
        /// 解析失败不影响流程，仅记日志；审批侧读取为空时自动降级实时抽取以兼容历史实例。
        /// 注意：后台线程不能复用请求级 scoped <c>Context</c>（请求结束后连接释放、租户上下文丢失），
        /// 须在请求内先捕获租户 Id，任务内用 <see cref="SqlSugar.IOC.DbScoped.SugarScope"/>.CopyNew() 独立连接并按租户路由。
        /// </summary>
        private void FillAttachmentParsedAsync(long instanceId, long flowId, string formContent)
        {
            if (string.IsNullOrWhiteSpace(formContent)) return;
            // 请求上下文内捕获租户 Id，供后台线程路由到正确租户库
            var tenantId = App.IsTenantEnabled() ? App.GetCurrentTenantId() : null;
            _ = Task.Run(async () =>
            {
                try
                {
                    var scope = SqlSugar.IOC.DbScoped.SugarScope.CopyNew();
                    ISqlSugarClient db = App.IsTenantEnabled() && !string.IsNullOrWhiteSpace(tenantId)
                        ? scope.AsTenant().GetConnectionScope(tenantId)
                        : scope;

                    var formItems = await db.Queryable<WfFlowDefinition>()
                        .Where(d => d.FlowId == flowId)
                        .Select(d => d.FormItems)
                        .FirstAsync();
                    var items = await WfAttachmentParser.ParseAsync(formContent, formItems).ConfigureAwait(false);
                    var parsed = WfAttachmentParser.Serialize(items);
                    if (!string.IsNullOrEmpty(parsed))
                    {
                        await db.Updateable<WfFlowInstance>()
                            .SetColumns(i => i.AttachmentParsed == parsed)
                            .Where(i => i.InstanceId == instanceId)
                            .ExecuteCommandAsync();
                    }
                    logger.Debug($"异步填充实例附件解析结果 InstanceId={instanceId},parsed={parsed}");
                }
                catch (Exception ex)
                {
                    // 附件解析为辅助能力，失败不影响主流程
                    logger.Error(ex, "附件解析失败 InstanceId=" + instanceId);
                }
            });
        }

        /// <summary>
        /// 驳回后重新提交：申请人修改内容再次发起，回到首节点重新审批。
        /// 参数仅接收变更字段（表单/附件/标题），避免传入整个 DTO 引入意外字段。
        /// </summary>
        /// <param name="instanceId">流程实例 Id</param>
        /// <param name="formContent">表单内容 JSON</param>
        /// <param name="attachment">附件路径（逗号分隔）</param>
        /// <param name="title">申请标题；空则保留实例原标题</param>
        /// <param name="userId">操作人 userId（须为原申请人 ApplyUserId，由 Engine 校验）</param>
        public void Resubmit(long instanceId, string formContent, string attachment, string title, long userId)
        {
            _engine.Resubmit(instanceId, formContent, attachment, title, userId);
            if (!string.IsNullOrWhiteSpace(formContent))
            {
                var flowId = Context.Queryable<WfFlowInstance>()
                    .Where(i => i.InstanceId == instanceId)
                    .Select(i => i.FlowId)
                    .First();
                FillAttachmentParsedAsync(instanceId, flowId, formContent);
            }
        }

        #endregion

        #region 统计

        /// <summary>
        /// 数据面板统计：聚合当前用户的待办/已办/我发起/抄送数量。
        /// 全部为只读 Count，待办/已办 与 我发起 各用一次 GroupBy 拿全部分组，单次调用返回所有指标。
        /// </summary>
        public WfDashboardStatsDto GetDashboardStats(long userId)
        {
            // 待办/已办：按状态分组一次聚合
            var taskStats = Context.Queryable<WfFlowTask>()
                .Where(t => t.AssigneeId == userId)
                .GroupBy(t => t.Status)
                .Select(t => new { Status = t.Status, Cnt = SqlFunc.AggregateCount(1) })
                .ToList();
            var todoCount = taskStats.FirstOrDefault(x => x.Status == (int)WfTaskStatus.Pending)?.Cnt ?? 0;
            var doneCount = taskStats.FirstOrDefault(x => x.Status == (int)WfTaskStatus.Done)?.Cnt ?? 0;

            // 我发起：按状态分组一次聚合
            var instStats = Context.Queryable<WfFlowInstance>()
                .Where(i => i.ApplyUserId == userId)
                .GroupBy(i => i.Status)
                .Select(i => new { Status = i.Status, Cnt = SqlFunc.AggregateCount(1) })
                .ToList();
            var myInProgress = instStats.FirstOrDefault(x => x.Status == (int)WfInstanceStatus.Approval)?.Cnt ?? 0;
            var myCompleted = instStats
                .Where(x => x.Status == (int)WfInstanceStatus.Approved || x.Status == (int)WfInstanceStatus.Rejected)
                .Sum(x => x.Cnt);

            // 抄送：记录已按收件人拆分并写入 OperatorId，且 Action 标记为 WfAction.Cc；
            // 角标统计「未读」抄送（IsRead=false）
            var ccCount = Context.Queryable<WfFlowRecord>()
                .Count(r => r.Action == (int)WfAction.Cc && r.OperatorId == userId && !r.IsRead);

            return new WfDashboardStatsDto
            {
                TodoCount = todoCount,
                DoneCount = doneCount,
                MyInProgress = myInProgress,
                MyCompleted = myCompleted,
                CcCount = ccCount
            };
        }

        /// <summary>
        /// 流程效率统计：
        /// <list type="number">
        /// <item>平均/最短/最长审批时长：已通过实例的 <c>Update_time - Create_time</c>（小时）；</item>
        /// <item>各节点平均耗时：已完成任务 <c>HandleTime - Create_time</c>，按节点名称聚合；</item>
        /// <item>完成率趋势：按月统计结束实例（通过+驳回），通过数 / 结束总数。</item>
        /// </list>
        /// isAdmin=true 时放开为全部用户实例（管理员全局视图）；flowId 可选，按流程定义维度过滤。
        /// </summary>
        public WfEfficiencyStatsDto GetEfficiencyStats(long userId, bool isAdmin = false, long? flowId = null)
        {
            // 单次拉取目标实例的最小字段集（Status/Create_time/Update_time），
            // 在内存里同时算出"已通过（用于时长）"和"已结束（用于完成率趋势）"，避免两次扫 wf_flow_instance
            var instQuery = Context.Queryable<WfFlowInstance>();
            if (!isAdmin)
            {
                // 普通用户仅看自己作为申请人的实例
                instQuery = instQuery.Where(i => i.ApplyUserId == userId);
            }
            if (flowId != null)
            {
                instQuery = instQuery.Where(i => i.FlowId == flowId.Value);
            }
            var allInst = instQuery
                .Select(i => new { i.InstanceId, i.Status, i.Create_time, i.Update_time })
                .ToList();
            // 目标实例范围（用于节点耗时分布关联，保证与管理员/流程维度过滤口径一致）
            var targetInstanceIds = allInst.Select(i => i.InstanceId).ToList();

            // 已通过：Update_time 为完成时间；个别情况下 Update_time 未更新（引擎漏写）时用 Create_time 兜底，避免负值
            var finished = allInst
                .Where(i => i.Status == (int)WfInstanceStatus.Approved)
                .Select(i => new { i.Create_time, EndTime = i.Update_time ?? i.Create_time })
                .ToList();

            var durations = finished
                .Select(x => (decimal)(x.EndTime - x.Create_time).TotalHours)
                .ToList();

            var eff = new WfEfficiencyStatsDto
            {
                FinishedCount = finished.Count
            };
            if (durations.Count > 0)
            {
                eff.AvgDurationHours = (decimal)Math.Round((double)durations.Average(), 2);
                eff.MinDurationHours = (decimal)Math.Round((double)durations.Min(), 2);
                eff.MaxDurationHours = (decimal)Math.Round((double)durations.Max(), 2);
            }

            // 各节点耗时分布：仅统计已处理(Status=Done)且 HandleTime 有值的任务；
            // 普通用户只看自己经手的节点(ApproveId)，管理员/按流程筛选时按目标实例范围聚合
            var nodeTaskQuery = Context.Queryable<WfFlowTask>()
                .Where(t => t.Status == (int)WfTaskStatus.Done && t.HandleTime != null);
            if (!isAdmin)
            {
                nodeTaskQuery = nodeTaskQuery.Where(t => t.AssigneeId == userId);
            }
            else if (targetInstanceIds.Count > 0)
            {
                nodeTaskQuery = nodeTaskQuery.Where(t => targetInstanceIds.Contains(t.InstanceId));
            }
            var nodeDurations = nodeTaskQuery
                .GroupBy(t => t.NodeName)
                .Select(t => new
                {
                    NodeName = t.NodeName,
                    AvgHours = SqlFunc.AggregateAvg(SqlFunc.DateDiff(DateType.Hour, t.Create_time, t.HandleTime.Value)),
                    Cnt = SqlFunc.AggregateCount(1)
                })
                .ToList()
                .Where(x => !string.IsNullOrEmpty(x.NodeName))
                .Select(x => new WfNodeDurationDto
                {
                    NodeName = x.NodeName,
                    AvgHours = (decimal)Math.Round((double)x.AvgHours, 2),
                    Count = x.Cnt
                })
                .OrderByDescending(x => x.AvgHours)
                .ToList();
            eff.NodeDurations = nodeDurations;

            // 完成率趋势：按月统计结束实例（通过 + 驳回）
            // Update_time 为 null 时用 Create_time 兜底（与 finished 同口径），避免污染"本月"
            eff.CompletionTrend = allInst
                .Where(i => i.Status == (int)WfInstanceStatus.Approved || i.Status == (int)WfInstanceStatus.Rejected)
                .GroupBy(i => (i.Update_time ?? i.Create_time).ToString("yyyy-MM"))
                .Select(g => new
                {
                    Month = g.Key,
                    TotalFinished = g.Count(),
                    Approved = g.Count(x => x.Status == (int)WfInstanceStatus.Approved),
                    Rejected = g.Count(x => x.Status == (int)WfInstanceStatus.Rejected)
                })
                .OrderBy(g => g.Month)
                .Select(g => new WfCompletionTrendDto
                {
                    Month = g.Month,
                    TotalFinished = g.TotalFinished,
                    Approved = g.Approved,
                    Rejected = g.Rejected,
                    Rate = g.TotalFinished == 0 ? 0 : Math.Round((decimal)g.Approved * 100 / g.TotalFinished, 1)
                })
                .ToList();

            return eff;
        }

        #endregion
    }
}
