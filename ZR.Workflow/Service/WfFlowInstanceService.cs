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

        public WfFlowInstanceService(IWfEngineService engine)
        {
            _engine = engine;
        }

        #region 列表

        /// <summary>
        /// 查询我的待办/已办/我发起/抄送的流程实例列表，按申请时间倒序分页返回。
        /// </summary>
        /// <param name="parm">查询条件（标题/状态/流程定义）</param>
        /// <param name="userId">当前用户 Id（按 <c>ApplyUserId</c> 关联）</param>
        public PagedInfo<WfFlowInstanceDto> GetMyList(WfFlowInstanceQueryDto parm, long userId)
        {
            var predicate = Expressionable.Create<WfFlowInstance>()
                .And(t => t.ApplyUserId == userId)
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
        /// </summary>
        public WfFlowInstanceDto GetInfo(long instanceId)
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
            return dto;
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
            return _engine.Start(instance);
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
        /// 流程效率统计（基于当前用户作为申请人的实例）：
        /// <list type="number">
        /// <item>平均/最短/最长审批时长：已通过实例的 <c>Update_time - Create_time</c>（小时）；</item>
        /// <item>各节点平均耗时：已完成任务 <c>HandleTime - Create_time</c>，按节点名称聚合；</item>
        /// <item>完成率趋势：按月统计结束实例（通过+驳回），通过数 / 结束总数。</item>
        /// </list>
        /// </summary>
        public WfEfficiencyStatsDto GetEfficiencyStats(long userId)
        {
            // 单次拉取当前用户全部实例的最小字段集（Status/Create_time/Update_time），
            // 在内存里同时算出"已通过（用于时长）"和"已结束（用于完成率趋势）"，避免两次扫 wf_flow_instance
            var allInst = Context.Queryable<WfFlowInstance>()
                .Where(i => i.ApplyUserId == userId)
                .Select(i => new { i.Status, i.Create_time, i.Update_time })
                .ToList();

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

            // 各节点耗时分布：仅统计已处理(Status=Done)且 HandleTime 有值的任务
            var nodeDurations = Context.Queryable<WfFlowTask>()
                .Where(t => t.AssigneeId == userId && t.Status == (int)WfTaskStatus.Done && t.HandleTime != null)
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
