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
            var def = Queryable().First(f => f.FlowId == flowId);
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
            var result = UseTran(() =>
            {
                // 级联删除关联的实例、任务与记录，避免脏数据
                var instIds = Context.Queryable<WfFlowInstance>()
                    .Where(i => ids.Contains(i.FlowId))
                    .Select(i => i.InstanceId)
                    .ToList();
                if (instIds.Count > 0)
                {
                    Context.Deleteable<WfFlowTask>().Where(t => instIds.Contains(t.InstanceId)).ExecuteCommand();
                    Context.Deleteable<WfFlowRecord>().Where(r => instIds.Contains(r.InstanceId)).ExecuteCommand();
                    Context.Deleteable<WfFlowInstance>().Where(i => instIds.Contains(i.InstanceId)).ExecuteCommand();
                }
                Context.Deleteable<WfFlowNode>().Where(n => ids.Contains(n.FlowId)).ExecuteCommand();
                Deleteable().Where(f => ids.Contains(f.FlowId)).ExecuteCommand();
            });
            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "删除失败", result.ErrorMessage);
            return 1;
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
            return predicate;
        }
    }
}
