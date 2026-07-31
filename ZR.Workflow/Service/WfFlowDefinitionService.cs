namespace ZR.Workflow.Service
{
    /// <summary>
    /// 流程定义与节点配置服务
    /// </summary>
    [AppService(ServiceType = typeof(IWfFlowDefinitionService))]
    public class WfFlowDefinitionService : BaseService<WfFlowDefinition>, IWfFlowDefinitionService
    {
        public PagedInfo<WfFlowDefinitionDto> GetList(WfFlowDefinitionQueryDto parm)
        {
            var predicate = QueryExp(parm);
            return Queryable().Where(predicate.ToExpression())
                .ToPage<WfFlowDefinition, WfFlowDefinitionDto>(parm);
        }

        public WfFlowDefinitionDto GetInfo(long flowId)
        {
            var def = Queryable().Where(f => f.IsDelete == 0 && f.FlowId == flowId).First();
            if (def == null) return null;
            var dto = def.Adapt<WfFlowDefinitionDto>();
            dto.Nodes = Context.Queryable<WfFlowNode>()
                .Where(n => n.FlowId == flowId)
                .OrderBy(n => n.NodeOrder)
                .ToList()
                .Adapt<List<WfFlowNodeDto>>();
            return dto;
        }

        public List<WfFlowNodeDto> GetNodes(long flowId)
        {
            return Context.Queryable<WfFlowNode>()
                .Where(n => n.FlowId == flowId)
                .OrderBy(n => n.NodeOrder)
                .ToList()
                .Adapt<List<WfFlowNodeDto>>();
        }

        public WfFlowDefinition Add(WfFlowDefinitionDto dto)
        {
            var userName = App.HttpContext?.GetName();
            var def = dto.Adapt<WfFlowDefinition>().ToCreate(App.HttpContext);
            var result = UseTran(() =>
            {
                def = InsertReturnEntity(def) ?? throw new CustomException("添加流程定义失败");
                InsertNodes(def.FlowId, dto.Nodes, userName);
            });
            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "添加失败", result.ErrorMessage);
            return def;
        }

        public int Update(WfFlowDefinitionDto dto)
        {
            var userName = App.HttpContext?.GetName();
            // 保留数据库中的 FlowCode / Version，避免编辑时版本号被客户端覆盖（版本仅由 SaveAsNewVersion 产生）
            var existing = Queryable().Where(f => f.FlowId == dto.FlowId).First();
            if (existing == null)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "流程定义不存在", null);
            var def = dto.Adapt<WfFlowDefinition>().ToUpdate(App.HttpContext);
            def.FlowCode = existing.FlowCode;
            def.Version = existing.Version;
            def.IsDraft = existing.IsDraft; // 编辑不清除草稿态，仅发布可改变
            var result = UseTran(() =>
            {
                Update(def, true, "修改流程定义");
                Context.Deleteable<WfFlowNode>().Where(n => n.FlowId == dto.FlowId).ExecuteCommand();
                InsertNodes(dto.FlowId, dto.Nodes, userName);
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

        /// <summary>
        /// 复制流程定义及其节点配置：生成一份停用状态的副本。
        /// 副本编码为「原编码_copy／_copy2…」（自动避让已存在的编码），
        /// 副本名称加「_副本」后缀，避免与源定义冲突；节点配置一并复制。
        /// </summary>
        public long Copy(long flowId, string userName)
        {
            var src = Queryable().Where(f => f.IsDelete == 0 && f.FlowId == flowId).First();
            if (src == null)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "流程定义不存在", null);
            var srcNodes = Context.Queryable<WfFlowNode>()
                .Where(n => n.FlowId == flowId)
                .OrderBy(n => n.NodeOrder)
                .ToList();

            var copy = new WfFlowDefinition
            {
                FlowCode = GenCopyCode(src.FlowCode),
                FlowName = src.FlowName + "_副本",
                FormType = src.FormType,
                Status = 0, // 副本默认停用，确认后再启用，避免误发起
                FormItems = src.FormItems,
                Remark = src.Remark,
                Create_by = userName,
                Create_time = DateTime.Now,
                Update_by = userName,
                Update_time = DateTime.Now
            };

            long newId = 0;
            var result = UseTran(() =>
            {
                copy = InsertReturnEntity(copy) ?? throw new CustomException("复制流程定义失败");
                newId = copy.FlowId;
                if (srcNodes.Count > 0)
                {
                    var entities = srcNodes.Select(n => new WfFlowNode
                    {
                        FlowId = newId,
                        NodeName = n.NodeName,
                        NodeType = n.NodeType,
                        ApproverType = n.ApproverType,
                        ApproverId = n.ApproverId,
                        NodeOrder = n.NodeOrder,
                        SignType = n.SignType,
                        ConditionField = n.ConditionField,
                        ConditionOp = n.ConditionOp,
                        ConditionValue = n.ConditionValue,
                        ParallelGroup = n.ParallelGroup,
                        Create_by = userName,
                        Create_time = DateTime.Now
                    }).ToList();
                    Context.Insertable(entities).ExecuteCommand();
                }
            });
            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "复制失败", result.ErrorMessage);
            return newId;
        }

        /// <summary>
        /// 生成不冲突的副本编码：原编码_copy，若已存在则 _copy2 / _copy3 …
        /// </summary>
        private string GenCopyCode(string baseCode)
        {
            var prefix = $"{baseCode}_copy";
            var existing = Context.Queryable<WfFlowDefinition>()
                .Where(f => f.IsDelete == 0 && f.FlowCode.StartsWith(prefix))
                .Select(f => f.FlowCode)
                .ToList();
            var candidate = prefix;
            var i = 2;
            while (existing.Contains(candidate))
                candidate = $"{baseCode}_copy{i++}";
            return candidate;
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
            var target = Queryable().Where(f => f.IsDelete == 0 && f.FlowId == flowId).First();
            if (target == null)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "流程定义不存在", null);

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
            var target = Queryable().Where(f => f.IsDelete == 0 && f.FlowId == flowId).First();
            if (target == null)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "流程定义不存在", null);
            if (target.IsDraft == 0)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "该版本已是正式版，无需发布", null);

            var rows = Context.Updateable<WfFlowDefinition>()
                .Where(f => f.FlowId == flowId)
                .SetColumns(it => new WfFlowDefinition { IsDraft = 0, Update_by = userName, Update_time = DateTime.Now })
                .ExecuteCommand();
            return rows;
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
            var src = Queryable().Where(f => f.IsDelete == 0 && f.FlowId == flowId).First();
            if (src == null)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "流程定义不存在", null);
            var srcNodes = Context.Queryable<WfFlowNode>()
                .Where(n => n.FlowId == flowId)
                .OrderBy(n => n.NodeOrder)
                .ToList();

            var maxVer = Queryable().Where(f => f.IsDelete == 0 && f.FlowCode == src.FlowCode)
                .Select(f => f.Version)
                .ToList()
                .DefaultIfEmpty(0)
                .Max();
            var newVer = maxVer + 1;

            var copy = new WfFlowDefinition
            {
                FlowCode = src.FlowCode,
                FlowName = src.FlowName,
                FormType = src.FormType,
                Status = 0, // 回滚/另存均为草稿停用态，需手动发布/设现行
                FormItems = src.FormItems,
                Version = newVer,
                IsDraft = 1,
                Remark = src.Remark,
                Create_by = userName,
                Create_time = DateTime.Now,
                Update_by = userName,
                Update_time = DateTime.Now
            };

            long newId = 0;
            var result = UseTran(() =>
            {
                copy = InsertReturnEntity(copy) ?? throw new CustomException($"{reason}失败");
                newId = copy.FlowId;
                if (srcNodes.Count > 0)
                {
                    var entities = srcNodes.Select(n => new WfFlowNode
                    {
                        FlowId = newId,
                        NodeName = n.NodeName,
                        NodeType = n.NodeType,
                        ApproverType = n.ApproverType,
                        ApproverId = n.ApproverId,
                        NodeOrder = n.NodeOrder,
                        SignType = n.SignType,
                        ConditionField = n.ConditionField,
                        ConditionOp = n.ConditionOp,
                        ConditionValue = n.ConditionValue,
                        ParallelGroup = n.ParallelGroup,
                        Create_by = userName,
                        Create_time = DateTime.Now
                    }).ToList();
                    Context.Insertable(entities).ExecuteCommand();
                }
            });
            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, $"{reason}失败", result.ErrorMessage);
            return newId;
        }

        private void InsertNodes(long flowId, List<WfFlowNodeDto> nodes, string userName)
        {
            if (nodes == null || nodes.Count == 0) return;
            var entities = nodes.Select(n => new WfFlowNode
            {
                FlowId = flowId,
                NodeName = n.NodeName,
                NodeType = n.NodeType,
                ApproverType = n.ApproverType,
                ApproverId = n.ApproverId,
                NodeOrder = n.NodeOrder,
                SignType = n.SignType,
                ConditionField = n.ConditionField,
                ConditionOp = n.ConditionOp,
                ConditionValue = n.ConditionValue,
                ParallelGroup = n.ParallelGroup,
                Create_by = userName,
                Create_time = DateTime.Now
            }).ToList();
            Context.Insertable(entities).ExecuteCommand();
        }

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
    }
}
