using SqlSugar;
using ZR.Workflow.Model;
using ZR.Workflow.Model.Dto;
using ZR.Workflow.Service.IService;

namespace ZR.Workflow.Service
{
    /// <summary>
    /// 审批任务服务
    /// </summary>
    [AppService(ServiceType = typeof(IWfFlowTaskService))]
    public class WfFlowTaskService : BaseService<WfFlowTask>, IWfFlowTaskService
    {
        public PagedInfo<WfFlowTaskDto> GetTodoList(WfFlowTaskQueryDto parm, string userName)
        {
            var query = Context.Queryable<WfFlowTask>()
                .InnerJoin<WfFlowInstance>((t, i) => t.InstanceId == i.InstanceId)
                .LeftJoin<WfFlowDefinition>((t, i, d) => i.FlowId == d.FlowId)
                .Where((t, i, d) => t.Assignee == userName && t.Status == (int)WfTaskStatus.Pending)
                .WhereIF(!string.IsNullOrEmpty(parm.Title), (t, i, d) => i.Title.Contains(parm.Title))
                .Select((t, i, d) => new WfFlowTaskDto
                {
                    TaskId = t.TaskId,
                    InstanceId = t.InstanceId,
                    NodeId = t.NodeId,
                    NodeName = t.NodeName,
                    Assignee = t.Assignee,
                    Status = t.Status,
                    Opinion = t.Opinion,
                    Action = t.Action,
                    HandleTime = t.HandleTime,
                    Create_time = t.Create_time,
                    Title = i.Title,
                    ApplyUser = i.ApplyUser,
                    FlowName = SqlFunc.IsNull(i.FlowName, d.FlowName),
                    InstanceStatus = i.Status
                });
            return query.ToPage(parm);
        }

        public PagedInfo<WfFlowTaskDto> GetDoneList(WfFlowTaskQueryDto parm, string userName)
        {
            var query = Context.Queryable<WfFlowTask>()
                .InnerJoin<WfFlowInstance>((t, i) => t.InstanceId == i.InstanceId)
                .LeftJoin<WfFlowDefinition>((t, i, d) => i.FlowId == d.FlowId)
                .Where((t, i, d) => t.Assignee == userName && t.Status == (int)WfTaskStatus.Done)
                .WhereIF(!string.IsNullOrEmpty(parm.Title), (t, i, d) => i.Title.Contains(parm.Title))
                .Select((t, i, d) => new WfFlowTaskDto
                {
                    TaskId = t.TaskId,
                    InstanceId = t.InstanceId,
                    NodeId = t.NodeId,
                    NodeName = t.NodeName,
                    Assignee = t.Assignee,
                    Status = t.Status,
                    Opinion = t.Opinion,
                    Action = t.Action,
                    HandleTime = t.HandleTime,
                    Create_time = t.Create_time,
                    Title = i.Title,
                    ApplyUser = i.ApplyUser,
                    FlowName = SqlFunc.IsNull(i.FlowName, d.FlowName),
                    InstanceStatus = i.Status
                });
            return query.ToPage(parm);
        }
    }
}
