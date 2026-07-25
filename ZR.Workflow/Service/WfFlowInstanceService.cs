using ZR.Workflow.Model;
using ZR.Workflow.Model.Dto;
using ZR.Workflow.Service.IService;

namespace ZR.Workflow.Service
{
    /// <summary>
    /// 流程实例服务
    /// </summary>
    [AppService(ServiceType = typeof(IWfFlowInstanceService))]
    public class WfFlowInstanceService : BaseService<WfFlowInstance>, IWfFlowInstanceService
    {
        private readonly IWfEngineService _engine;

        public WfFlowInstanceService(IWfEngineService engine)
        {
            _engine = engine;
        }

        public PagedInfo<WfFlowInstanceDto> GetMyList(WfFlowInstanceQueryDto parm, string userName)
        {
            var predicate = Expressionable.Create<WfFlowInstance>();
            predicate = predicate.And(t => t.ApplyUser == userName);
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Title), t => t.Title.Contains(parm.Title));
            predicate = predicate.AndIF(parm.Status != null, t => t.Status == parm.Status);
            predicate = predicate.AndIF(parm.FlowId != null, t => t.FlowId == parm.FlowId);
            var paged = Queryable().Where(predicate.ToExpression())
                .ToPage<WfFlowInstance, WfFlowInstanceDto>(parm);
            // 冗余 FlowName 可能为空，按 FlowId 关联流程定义兜底填充
            FillFlowName(paged.Result);
            return paged;
        }

        private void FillFlowName(List<WfFlowInstanceDto> list)
        {
            if (list == null || list.Count == 0) return;
            var needIds = list.Where(x => string.IsNullOrEmpty(x.FlowName)).Select(x => x.FlowId).Distinct().ToList();
            if (needIds.Count == 0) return;
            var defs = Context.Queryable<WfFlowDefinition>()
                .Where(d => needIds.Contains(d.FlowId))
                .ToList();
            var map = defs.ToDictionary(d => d.FlowId, d => d.FlowName);
            foreach (var it in list)
            {
                if (string.IsNullOrEmpty(it.FlowName) && map.TryGetValue(it.FlowId, out var fn))
                    it.FlowName = fn;
            }
        }

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
            return dto;
        }

        public long Start(WfFlowInstanceDto dto, string userName)
        {
            var instance = dto.Adapt<WfFlowInstance>();
            instance.ApplyUser = userName;
            instance.Status = (int)WfInstanceStatus.Approval;
            instance.Create_by = userName;
            instance.Create_time = DateTime.Now;
            return _engine.Start(instance);
        }
    }
}
