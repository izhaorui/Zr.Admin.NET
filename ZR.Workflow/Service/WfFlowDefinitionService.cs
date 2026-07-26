using ZR.Workflow.Model;
using ZR.Workflow.Model.Dto;
using ZR.Workflow.Service.IService;

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
            var def = dto.Adapt<WfFlowDefinition>().ToUpdate(App.HttpContext);
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
            var candidate = $"{baseCode}_copy";
            var i = 2;
            while (Context.Queryable<WfFlowDefinition>().Any(f => f.IsDelete == 0 && f.FlowCode == candidate))
            {
                candidate = $"{baseCode}_copy{i}";
                i++;
            }
            return candidate;
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
            // 软删除过滤：列表只展示未删除的定义
            predicate = predicate.And(it => it.IsDelete == 0);
            return predicate;
        }
    }
}
