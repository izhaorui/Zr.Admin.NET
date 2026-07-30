using System.Collections.Generic;

namespace ZR.Workflow.Service
{
    /// <summary>
    /// 审批任务服务
    /// </summary>
    [AppService(ServiceType = typeof(IWfFlowTaskService))]
    public class WfFlowTaskService : BaseService<WfFlowTask>, IWfFlowTaskService
    {
        public PagedInfo<WfFlowTaskDto> GetTodoList(WfFlowTaskQueryDto parm, long userId)
        {
            return GetTaskList(parm, userId, (int)WfTaskStatus.Pending);
        }

        public PagedInfo<WfFlowTaskDto> GetDoneList(WfFlowTaskQueryDto parm, long userId)
        {
            return GetTaskList(parm, userId, (int)WfTaskStatus.Done);
        }

        private PagedInfo<WfFlowTaskDto> GetTaskList(WfFlowTaskQueryDto parm, long userId, int status)
        {
            var query = Context.Queryable<WfFlowTask>()
                .InnerJoin<WfFlowInstance>((t, i) => t.InstanceId == i.InstanceId)
                .LeftJoin<WfFlowDefinition>((t, i, d) => i.FlowId == d.FlowId)
                .Where((t, i, d) => t.AssigneeId == userId && t.Status == status)
                .WhereIF(!string.IsNullOrEmpty(parm.Title), (t, i, d) => i.Title.Contains(parm.Title))
                .Select((t, i, d) => new WfFlowTaskDto
                {
                    TaskId = t.TaskId,
                    InstanceId = t.InstanceId,
                    NodeId = t.NodeId,
                    NodeName = t.NodeName,
                    Assignee = t.Assignee,
                    AssigneeNickName = t.AssigneeNickName,
                    Status = t.Status,
                    Opinion = t.Opinion,
                    Action = t.Action,
                    TaskType = t.TaskType,
                    IsRead = t.IsRead,
                    HandleTime = t.HandleTime,
                    Create_time = t.Create_time,
                    Title = i.Title,
                    ApplyUser = i.ApplyUser,
                    ApplyNickName = i.ApplyNickName,
                    FlowName = SqlFunc.IsNull(i.FlowName, d.FlowName),
                    InstanceStatus = i.Status
                });
            return query.ToPage(parm);
        }

        /// <summary>
        /// 标记待办已读（仅更新当前用户名下任务，防止越权标记他人）
        /// </summary>
        public void Read(List<long> ids, long userId)
        {
            if (ids == null || ids.Count == 0) return;
            Context.Updateable<WfFlowTask>()
                .SetColumns(t => new WfFlowTask { IsRead = true })
                .Where(t => ids.Contains(t.TaskId) && t.AssigneeId == userId)
                .ExecuteCommand();
        }
    }
}
