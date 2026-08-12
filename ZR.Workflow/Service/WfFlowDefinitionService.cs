namespace ZR.Workflow.Service
{
    /// <summary>
    /// 流程定义与节点配置服务。
    ///
    /// 职责分层：
    /// - 公共入口：列表/详情/增删改查
    /// - 版本与副本管理：复制(Copy)、版本历史(GetVersions)、现行切换(SetCurrentVersion)、发布(Publish)、另存新版本(SaveAsNewVersion)、回滚(Rollback)
    /// - 私有辅助：节点复制与软删除查询
    ///
    /// 关键约束：
    /// - 软删除统一经由 GetActiveDef(GetVersions/GetActiveDefByCode 由列表路径承担)，列表查询经由 QueryExp，避免散落过滤条件
    /// - 节点新增字段时只需改 CloneNodeForCopy 与 InsertNodes 各一处
    /// - 版本管理不删除历史定义，实例绑定各自 FlowId，旧版可继续承载在途实例
    /// </summary>
    [AppService(ServiceType = typeof(IWfFlowDefinitionService))]
    public class WfFlowDefinitionService : BaseService<WfFlowDefinition>, IWfFlowDefinitionService
    {
        #region 公共入口

        public PagedInfo<WfFlowDefinitionDto> GetList(WfFlowDefinitionQueryDto parm)
        {
            var predicate = QueryExp(parm);
            return Queryable()
                .Where(predicate.ToExpression())
                .OrderByDescending(f => f.FlowId)
                .ToPage<WfFlowDefinition, WfFlowDefinitionDto>(parm);
        }

        public WfFlowDefinitionDto GetInfo(long flowId)
        {
            var def = GetActiveDef(flowId);
            if (def == null) return null;
            var dto = def.Adapt<WfFlowDefinitionDto>();
            dto.Nodes = GetOrderedNodes(flowId).Adapt<List<WfFlowNodeDto>>();
            dto.NodeLinks = GetNodeLinks(flowId).Adapt<List<WfNodeLinkDto>>();
            return dto;
        }

        public List<WfFlowNodeDto> GetNodes(long flowId)
        {
            return GetOrderedNodes(flowId).Adapt<List<WfFlowNodeDto>>();
        }

        public WfFlowDefinition Add(WfFlowDefinitionDto dto)
        {
            var userName = App.HttpContext?.GetName();
            // 流程编码严格唯一：同一 FlowCode 视为同一流程的不同版本，新增独立流程必须走新编码；
            // 想加版本应走"另存为新版本"，而非重复 Add 同一 FlowCode。
            if (Queryable().Any(f => f.IsDelete == 0 && f.FlowCode == dto.FlowCode))
                throw new CustomException(ResultCode.CUSTOM_ERROR, $"流程编码「{dto.FlowCode}」已存在，请换一个或改用版本管理", null);
            ValidateLinks(dto.Nodes, dto.NodeLinks); // link 为唯一串联事实：非结束节点必须有出边
            var def = dto.Adapt<WfFlowDefinition>().ToCreate(App.HttpContext);
            var result = UseTran(() =>
            {
                def = InsertReturnEntity(def) ?? throw new CustomException("添加流程定义失败");
                var nodeMap = InsertNodes(def.FlowId, dto.Nodes, userName);
                InsertLinks(def.FlowId, dto.NodeLinks, userName, nodeMap);
            });
            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "添加失败", result.ErrorMessage);
            return def;
        }

        public int Update(WfFlowDefinitionDto dto)
        {
            var userName = App.HttpContext?.GetName();
            // 保留数据库中的 FlowCode / Version / IsDraft，避免编辑时版本号或草稿态被客户端覆盖
            var existing = GetActiveDef(dto.FlowId)
                ?? throw new CustomException(ResultCode.CUSTOM_ERROR, "流程定义不存在", null);
            var def = dto.Adapt<WfFlowDefinition>().ToUpdate(App.HttpContext);
            def.FlowCode = existing.FlowCode;
            def.Version = existing.Version;
            def.IsDraft = existing.IsDraft; // 编辑不清除草稿态，仅发布可改变
            ValidateLinks(dto.Nodes, dto.NodeLinks); // link 为唯一串联事实：非结束节点必须有出边
            var result = UseTran(() =>
            {
                Update(def, true, "修改流程定义");
                // 节点整体替换：先删旧连线再删旧节点（连线依赖节点），随后重建
                Context.Deleteable<WfNodeLink>().Where(l => l.FlowId == dto.FlowId).ExecuteCommand();
                Context.Deleteable<WfFlowNode>().Where(n => n.FlowId == dto.FlowId).ExecuteCommand();
                var nodeMap = InsertNodes(dto.FlowId, dto.Nodes, userName);
                InsertLinks(dto.FlowId, dto.NodeLinks, userName, nodeMap);
            });
            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "修改失败", result.ErrorMessage);
            return 1;
        }

        public int Delete(long[] ids)
        {
            // 删除前置校验：若仍存在未结束（审批中=0 / 撤回=3）的实例，禁止删除。
            // 避免活跃流程被隐藏后管理混乱（待办列表仍存活、但定义已在前台不可见）。
            var runningCount = Context.Queryable<WfFlowInstance>()
                .Where(i => ids.Contains(i.FlowId) && (i.Status == 0 || i.Status == 3))
                .Count();
            if (runningCount > 0)
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "该流程仍存在进行中或已撤回的实例，无法删除", null);
            }

            // 软删除：仅标记 IsDelete=1，保留节点/实例/任务/记录等历史数据。
            // 引擎（WfEngineService）按 FlowId 取定义/节点时不过滤 IsDelete，
            // 因此已发起的在途实例仍可继续流转直至结束，不会因定义"删除"而中断或报错。
            var rows = Context.Updateable<WfFlowDefinition>()
                .Where(f => ids.Contains(f.FlowId))
                .SetColumns(it => new WfFlowDefinition { IsDelete = 1 })
                .ExecuteCommand();
            return rows;
        }

        #endregion

        #region 版本与副本管理

        /// <summary>
        /// 复制流程定义及其节点配置：生成一份停用状态的副本。
        /// 副本编码为「原编码_copy／_copy2…」（自动避让已存在的编码），
        /// 副本名称加「_副本」后缀，避免与源定义冲突；节点配置一并复制。
        /// </summary>
        public long Copy(long flowId, string userName)
        {
            return CloneDefAndNodes(
                flowId,
                defFactory: src => new WfFlowDefinition
                {
                    FlowCode = GenCopyCode(src.FlowCode),
                    FlowName = src.FlowName + "_副本",
                    FormType = src.FormType,
                    Status = 0, // 副本默认停用，确认后再启用，避免误发起
                    FormItems = src.FormItems,
                    DesignJson = src.DesignJson,
                    Remark = src.Remark,
                    Create_by = userName,
                    Create_time = DateTime.Now,
                    Update_by = userName,
                    Update_time = DateTime.Now
                },
                errorLabel: "复制流程定义失败",
                userName: userName);
        }

        /// <summary>
        /// 查询某流程编码下的全部版本（含已冻结的历史版本），按 Version 升序，并标记现行版本
        /// </summary>
        public List<WfFlowDefinitionDto> GetVersions(string flowCode)
        {
            if (string.IsNullOrEmpty(flowCode))
                return new List<WfFlowDefinitionDto>();
            var list = Queryable()
                .Where(f => f.IsDelete == 0 && f.FlowCode == flowCode)
                .OrderBy(f => f.Version)
                .ToList()
                .Adapt<List<WfFlowDefinitionDto>>();
            // 现行版本 = 同 FlowCode 下 Status=1 且 IsDraft=0 的唯一版本
            var current = list.FirstOrDefault(x => x.Status == 1 && x.IsDraft == 0);
            if (current != null) current.IsCurrent = true;
            return list;
        }

        /// <summary>
        /// 设为现行版本：启用目标版本(Status=1,IsDraft=0)，并停用同 FlowCode 下其他所有版本，
        /// 保证同一流程编码下现行版本唯一。目标版本若为草稿则会一并发布。
        /// </summary>
        public int SetCurrentVersion(long flowId, string userName)
        {
            var target = GetActiveDef(flowId)
                ?? throw new CustomException(ResultCode.CUSTOM_ERROR, "流程定义不存在", null);

            var result = UseTran(() =>
            {
                // 同 FlowCode 其他版本全部停用（保留历史，仅取消现行资格）
                Context.Updateable<WfFlowDefinition>()
                    .Where(f => f.IsDelete == 0 && f.FlowCode == target.FlowCode && f.FlowId != flowId)
                    .SetColumns(it => new WfFlowDefinition { Status = 0 })
                    .ExecuteCommand();
                // 目标版本：启用 + 发布（非草稿）
                Context.Updateable<WfFlowDefinition>()
                    .Where(f => f.FlowId == flowId)
                    .SetColumns(it => new WfFlowDefinition { Status = 1, IsDraft = 0, Update_by = userName, Update_time = DateTime.Now })
                    .ExecuteCommand();
            });
            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "设为现行版本失败", result.ErrorMessage);
            return 1;
        }

        /// <summary>
        /// 发布草稿版本：将 IsDraft=1 的草稿转为正式(IsDraft=0)。
        /// 不会自动停用其他版本；如需切换现行请调用 SetCurrentVersion。
        /// </summary>
        public int Publish(long flowId, string userName)
        {
            var target = GetActiveDef(flowId)
                ?? throw new CustomException(ResultCode.CUSTOM_ERROR, "流程定义不存在", null);
            if (target.IsDraft == 0)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "该版本已是正式版，无需发布", null);

            var rows = Context.Updateable<WfFlowDefinition>()
                .Where(f => f.FlowId == flowId)
                .SetColumns(it => new WfFlowDefinition { IsDraft = 0, Update_by = userName, Update_time = DateTime.Now })
                .ExecuteCommand();
            return rows;
        }

        /// <summary>
        /// 另存为新版本：复制当前定义（FlowCode 不变）与节点到新的 FlowId，
        /// Version = 该 FlowCode 当前最大版本 + 1；旧版本冻结保留，不影响在途实例。
        /// 生成草稿态（IsDraft=1），需手动发布/设为现行后才可发起。
        /// </summary>
        public long SaveAsNewVersion(long flowId, string userName)
        {
            return SaveAsNewVersionInternal(flowId, userName, "另存新版本");
        }

        /// <summary>
        /// 版本回滚：将指定历史版本复制为一份新的最高版本（草稿态），保留完整版本链路。
        /// 例如当前 v3 回滚到 v1 → 生成 v4（节点/表单内容与 v1 相同），旧版不受影响。
        /// </summary>
        public long Rollback(long flowId, string userName)
        {
            return SaveAsNewVersionInternal(flowId, userName, "回滚");
        }

        /// <summary>
        /// 内部：复制某版本定义与节点到新 FlowId，Version=该 FlowCode 当前最大+1，默认草稿态。
        /// 供 SaveAsNewVersion(手动另存) 与 Rollback(回滚) 共用。
        /// </summary>
        private long SaveAsNewVersionInternal(long flowId, string userName, string reason)
        {
            return CloneDefAndNodes(
                flowId,
                defFactory: src => new WfFlowDefinition
                {
                    FlowCode = src.FlowCode,
                    FlowName = src.FlowName,
                    FormType = src.FormType,
                    Status = 0, // 回滚/另存均为草稿停用态，需手动发布/设现行
                    FormItems = src.FormItems,
                    DesignJson = src.DesignJson,
                    Version = GetNextVersion(src.FlowCode),
                    IsDraft = 1,
                    Remark = src.Remark,
                    Create_by = userName,
                    Create_time = DateTime.Now,
                    Update_by = userName,
                    Update_time = DateTime.Now
                },
                errorLabel: $"{reason}失败",
                userName: userName);
        }

        #endregion

        #region 私有辅助 —— 节点复制

        /// <summary>
        /// 新增/复制节点时构造实体的唯一入口。新增 WfFlowNode 字段时只需改本方法一处，
        /// InsertNodes / CloneDefAndNodes 自动覆盖。
        /// </summary>
        private static WfFlowNode CloneNodeForCopy(WfFlowNode src, long newFlowId, string userName)
        {
            return new WfFlowNode
            {
                FlowId = newFlowId,
                NodeName = src.NodeName,
                NodeType = src.NodeType,
                ApproverType = src.ApproverType,
                ApproverId = src.ApproverId,
                ApproverNames = src.ApproverNames,
                NodeOrder = src.NodeOrder,
                SignType = src.SignType,
                ConditionField = src.ConditionField,
                ConditionOp = src.ConditionOp,
                ConditionValue = src.ConditionValue,
                ParallelGroup = src.ParallelGroup,
                EnterHookUrl = src.EnterHookUrl,
                LeaveHookUrl = src.LeaveHookUrl,
                RejectStrategy = src.RejectStrategy,
                RejectTargetNodeId = src.RejectTargetNodeId,
                EmptyApproverStrategy = src.EmptyApproverStrategy,
                DefaultApproverId = src.DefaultApproverId,
                Create_by = userName,
                Create_time = DateTime.Now
            };
        }

        /// <summary>
        /// 写入节点并返回「客户端节点Id(clientId) → 新生成 NodeId」映射。
        /// clientId 即前端传入的 <see cref="WfFlowNodeDto.NodeId"/>：编辑时为旧 NodeId、新增时为前端自管的临时 Id（如负数/0）。
        /// 返回的映射供调用方把连线(link)的 SourceNodeId/TargetNodeId 重映射为新 NodeId，
        /// 同时也用于把本批节点的 RejectTargetNodeId（驳回到指定节点）按同一映射重映射为新主键，
        /// 避免前端传负数临时 id 时落库指向不存在的节点。
        /// 节点为空返回空字典。
        /// </summary>
        private Dictionary<long, long> InsertNodes(long flowId, List<WfFlowNodeDto> nodes, string userName)
        {
            var map = new Dictionary<long, long>();
            if (nodes == null || nodes.Count == 0) return map;
            // Dto → 实体 走 Adapt 后再走 CloneNodeForCopy，保证后续字段扩展只改一处。
            // 注意：RejectTargetNodeId 暂置 0（引用尚未生成的新节点），待本批全部写入、map 就绪后再回填重映射值。
            var entities = nodes.Select(n =>
            {
                var e = CloneNodeForCopy(n.Adapt<WfFlowNode>(), flowId, userName);
                e.RejectTargetNodeId = 0;
                return e;
            }).ToList();
            // 逐个插入以拿到新 NodeId（InsertReturnEntity 返回带 Id 的实体），建立 clientId→newId 映射
            for (var i = 0; i < nodes.Count; i++)
            {
                var saved = Context.Insertable(entities[i]).ExecuteReturnEntity() ?? throw new CustomException("写入流程节点失败");
                map[nodes[i].NodeId] = saved.NodeId;
            }
            // 驳回目标节点重映射：前端传来的是目标节点的 clientId（编辑为旧主键、新增为负数临时 id），
            // 必须按本批 map 翻译成新主键；未命中（如指向别的流程节点）保持原值，由引擎运行时校验。
            var needUpdate = false;
            for (var i = 0; i < nodes.Count; i++)
            {
                var raw = nodes[i].RejectTargetNodeId;
                if (raw == 0 || !raw.HasValue) continue;
                if (map.TryGetValue(raw.Value, out var newId))
                {
                    entities[i].RejectTargetNodeId = newId;
                    needUpdate = true;
                }
            }
            if (needUpdate)
            {
                Context.Updateable(entities.Where(e => e.RejectTargetNodeId > 0).ToList())
                    .UpdateColumns(e => new { e.RejectTargetNodeId })
                    .ExecuteCommand();
            }
            return map;
        }

        /// <summary>
        /// 复制源定义的节点到新 FlowId。无源节点则跳过。
        /// 返回「源 NodeId → 新 NodeId」映射，供复制连线时重映射。
        /// </summary>
        private Dictionary<long, long> CloneNodesToNewFlow(long srcFlowId, long newFlowId, string userName)
        {
            var map = new Dictionary<long, long>();
            var srcNodes = GetOrderedNodes(srcFlowId);
            if (srcNodes.Count == 0) return map;
            foreach (var n in srcNodes)
            {
                var copy = CloneNodeForCopy(n, newFlowId, userName);
                // 驳回目标节点：源流程中的 RejectTargetNodeId 指向源节点主键，复制后须映射为新流程的主键
                if (copy.RejectTargetNodeId > 0 && copy.RejectTargetNodeId.HasValue && map.TryGetValue(copy.RejectTargetNodeId.Value, out var newTarget))
                {
                    copy.RejectTargetNodeId = newTarget;
                }
                copy = Context.Insertable(copy).ExecuteReturnEntity() ?? throw new CustomException("复制流程节点失败");
                map[n.NodeId] = copy.NodeId;
            }
            return map;
        }

        #endregion

        #region 私有辅助 —— 软删除查询 / 排序节点

        /// <summary>
        /// 取未删除的定义；不存在返回 null（不抛，由调用方决定文案）。
        /// </summary>
        private WfFlowDefinition GetActiveDef(long flowId)
        {
            return Queryable().Where(f => f.IsDelete == 0 && f.FlowId == flowId).First();
        }

        /// <summary>
        /// 取某 FlowCode 当前最大版本号（已软删除的不算），无则视为 0。
        /// </summary>
        private int GetNextVersion(string flowCode)
        {
            return Queryable()
                .Where(f => f.IsDelete == 0 && f.FlowCode == flowCode)
                .Select(f => f.Version)
                .ToList()
                .DefaultIfEmpty(0)
                .Max() + 1;
        }

        /// <summary>
        /// 按 NodeOrder 升序取某 FlowId 的全部节点（含已软删除定义的节点，仅本服务内部使用，
        /// 不依赖 IsDelete 过滤——节点行没有 IsDelete 字段）。
        /// </summary>
        private List<WfFlowNode> GetOrderedNodes(long flowId)
        {
            return Context.Queryable<WfFlowNode>()
                .Where(n => n.FlowId == flowId)
                .OrderBy(n => n.NodeOrder)
                .ToList();
        }

        /// <summary>
        /// 取某 FlowId 的全部节点连线（按 Sort 升序），供详情返回与复制读取。
        /// </summary>
        private List<WfNodeLink> GetNodeLinks(long flowId)
        {
            return Context.Queryable<WfNodeLink>()
                .Where(l => l.FlowId == flowId)
                .OrderBy(l => l.Sort)
                .ToList();
        }

        /// <summary>
        /// 写入连线：将 DTO 中的 SourceNodeId/TargetNodeId 按 <paramref name="nodeMap"/> 重映射为新建节点的真实 Id 后落库。
        /// nodeMap 为「客户端节点Id → 新 NodeId」映射（新增/编辑场景）或「源 NodeId → 新 NodeId」映射（复制场景）。
        ///
        /// 落库前过滤三类脏数据：
        /// - 任一端未命中 nodeMap（指向不存在的节点）；
        /// - 端点为 0（前端未填）；
        /// - 自环（SourceNodeId == TargetNodeId，引擎无意义、且会引发死循环）。
        /// 无连线则空操作。
        /// </summary>
        /// <summary>
        /// 提交前校验：link 为流程串联的唯一事实来源，故每个「非结束节点」必须至少有一条有效出边，
        /// 否则运行态会从该节点断链卡死。有效出边 = SourceNodeId &gt; 0 且 != TargetNodeId（与 InsertLinks 过滤口径一致）。
        /// 结束节点（NodeType=3）允许无出边。校验在事务外执行，避免脏数据进事务。
        /// </summary>
        private void ValidateLinks(List<WfFlowNodeDto> nodes, List<WfNodeLinkDto> links)
        {
            if (nodes == null || nodes.Count == 0) return;
            // 开始/结束节点由引擎隐式处理、不在 dto.Nodes。
            // 普通审批/抄送节点：无出边即视为流程终点（流向结束），允许（支持树形多叶子结构）；
            // 仅条件网关（菱形）必须至少有 2 条有效出边且至少 1 条带条件，否则分流无意义。
            foreach (var node in nodes)
            {
                if (node.NodeType == (int)Enum.WfNodeType.End) continue; // 结束节点无需出边
                // 条件网关（菱形）：必须有 ≥2 条出边且至少一条带条件，否则分流无意义
                if (node.NodeType == (int)Enum.WfNodeType.Condition)
                {
                    var outCount = links.Count(l => l.SourceNodeId == node.NodeId
                        && l.SourceNodeId != 0 && l.TargetNodeId != 0 && l.SourceNodeId != l.TargetNodeId);
                    var hasCond = links.Any(l => l.SourceNodeId == node.NodeId
                        && !string.IsNullOrWhiteSpace(l.ConditionJson));
                    if (outCount < 2 || !hasCond)
                        throw new CustomException(ResultCode.CUSTOM_ERROR,
                            $"条件网关「{node.NodeName}」至少需 2 条出边且至少 1 条带条件", null);
                }
                // 并行分叉网关(7)：需 ≥2 条出边（全部分支并发），否则并行无意义
                else if (node.NodeType == (int)Enum.WfNodeType.ParallelFork)
                {
                    var outCount = links.Count(l => l.SourceNodeId == node.NodeId
                        && l.SourceNodeId != 0 && l.TargetNodeId != 0 && l.SourceNodeId != l.TargetNodeId);
                    if (outCount < 2)
                        throw new CustomException(ResultCode.CUSTOM_ERROR,
                            $"并行分叉「{node.NodeName}」至少需 2 条出边（并行分支）", null);
                }
                // 并行汇聚网关(8)：需 ≥1 条入边（来自并行分支）且 ≥1 条出边（汇聚后继续），否则汇聚无意义
                else if (node.NodeType == (int)Enum.WfNodeType.ParallelJoin)
                {
                    var inCount = links.Count(l => l.TargetNodeId == node.NodeId
                        && l.SourceNodeId != 0 && l.TargetNodeId != 0 && l.SourceNodeId != l.TargetNodeId);
                    var outCount = links.Count(l => l.SourceNodeId == node.NodeId
                        && l.SourceNodeId != 0 && l.TargetNodeId != 0 && l.SourceNodeId != l.TargetNodeId);
                    if (inCount < 1 || outCount < 1)
                        throw new CustomException(ResultCode.CUSTOM_ERROR,
                            $"并行汇聚「{node.NodeName}」需至少 1 条入边与 1 条出边", null);
                }
            }
        }

        private void InsertLinks(long flowId, List<WfNodeLinkDto> links, string userName, Dictionary<long, long> nodeMap)
        {
            if (links == null || links.Count == 0) return;
            var entities = links
                .Select(l => new WfNodeLink
                {
                    FlowId = flowId,
                    SourceNodeId = nodeMap.TryGetValue(l.SourceNodeId, out var s) ? s : l.SourceNodeId,
                    TargetNodeId = nodeMap.TryGetValue(l.TargetNodeId, out var t) ? t : l.TargetNodeId,
                    ConditionJson = l.ConditionJson,
                    Sort = l.Sort,
                    Create_by = userName,
                    Create_time = DateTime.Now
                })
                .Where(l => l.SourceNodeId > 0 && l.TargetNodeId > 0 && l.SourceNodeId != l.TargetNodeId)
                .ToList();
            if (entities.Count == 0) return;
            Context.Insertable(entities).ExecuteCommand();
        }

        /// <summary>
        /// 复制源 FlowId 的连线到新 FlowId，按 nodeMap（源 NodeId → 新 NodeId）重映射源/目标节点。
        /// 同样过滤未命中映射 / 端点为 0 / 自环。无源连线或空映射时跳过。
        /// </summary>
        private void CloneLinksToNewFlow(long srcFlowId, long newFlowId, Dictionary<long, long> nodeMap, string userName)
        {
            var srcLinks = GetNodeLinks(srcFlowId);
            if (srcLinks.Count == 0) return;
            var entities = srcLinks
                .Select(l => new WfNodeLink
                {
                    FlowId = newFlowId,
                    SourceNodeId = nodeMap.TryGetValue(l.SourceNodeId, out var s) ? s : l.SourceNodeId,
                    TargetNodeId = nodeMap.TryGetValue(l.TargetNodeId, out var t) ? t : l.TargetNodeId,
                    ConditionJson = l.ConditionJson,
                    Sort = l.Sort,
                    Create_by = userName,
                    Create_time = DateTime.Now
                })
                .Where(l => l.SourceNodeId > 0 && l.TargetNodeId > 0 && l.SourceNodeId != l.TargetNodeId)
                .ToList();
            if (entities.Count == 0) return;
            Context.Insertable(entities).ExecuteCommand();
        }

        #endregion

        #region 私有辅助 —— 定义/节点复制事务

        /// <summary>
        /// 复制源定义到新 FlowId（含节点）。
        ///
        /// 流转图：
        /// <code>
        ///   CloneDefAndNodes
        ///        │
        ///        ├─ 取源（GetActiveDef，null → CustomException）
        ///        │
        ///        ├─ UseTran
        ///        │     │
        ///        │     ├─ defFactory(src) 构造新定义
        ///        │     ├─ InsertReturnEntity（新 FlowId）
        ///        │     └─ CloneNodesToNewFlow
        ///        │
        ///        └─ 返回 newId
        /// </code>
        /// </summary>
        private long CloneDefAndNodes(long srcFlowId, Func<WfFlowDefinition, WfFlowDefinition> defFactory, string errorLabel, string userName)
        {
            var src = GetActiveDef(srcFlowId)
                ?? throw new CustomException(ResultCode.CUSTOM_ERROR, "流程定义不存在", null);

            long newId = 0;
            var result = UseTran(() =>
            {
                var copy = defFactory(src);
                copy = InsertReturnEntity(copy) ?? throw new CustomException(errorLabel);
                newId = copy.FlowId;
                var nodeMap = CloneNodesToNewFlow(srcFlowId, newId, userName);
                CloneLinksToNewFlow(srcFlowId, newId, nodeMap, userName);
            });
            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, errorLabel, result.ErrorMessage);
            return newId;
        }

        #endregion

        #region 私有辅助 —— 副本编码生成（带冲突重试）

        // FlowCode 列在数据库侧无唯一约束（仅 Length=64 NOTNULL），
        // 并发 Copy 时两个事务可能都读到同一份"已存在编码"集合并选中同一候选，
        // 导致后插入的版本拿到重复 FlowCode。本方法通过循环最多 999 次避让，
        // 但**真正的并发安全应依赖数据库唯一索引**，超出本服务范围。
        private const int MaxCopyCodeRetry = 999;

        /// <summary>
        /// 生成不冲突的副本编码：原编码_copy，若已存在则 _copy2 / _copy3 …
        /// </summary>
        private string GenCopyCode(string baseCode)
        {
            var prefix = $"{baseCode}_copy";
            for (var i = 2; i <= MaxCopyCodeRetry; i++)
            {
                var candidate = i == 2 ? prefix : $"{baseCode}_copy{i}";
                var exists = Context.Queryable<WfFlowDefinition>()
                    .Where(f => f.IsDelete == 0 && f.FlowCode == candidate)
                    .Any();
                if (!exists) return candidate;
            }
            return $"{baseCode}_copy_{DateTime.Now:HHmmssfff}"; // 极端并发兜底
        }

        #endregion

        #region 私有辅助 —— 列表查询条件

        private static Expressionable<WfFlowDefinition> QueryExp(WfFlowDefinitionQueryDto parm)
        {
            var predicate = Expressionable.Create<WfFlowDefinition>();
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.FlowName), it => it.FlowName.Contains(parm.FlowName));
            predicate = predicate.AndIF(parm.Status != null, it => it.Status == parm.Status);
            predicate = predicate.AndIF(parm.IsDraft != null, it => it.IsDraft == parm.IsDraft);
            // 软删除过滤：列表只展示未删除的定义
            predicate = predicate.And(it => it.IsDelete == 0);
            return predicate;
        }

        #endregion
    }
}
