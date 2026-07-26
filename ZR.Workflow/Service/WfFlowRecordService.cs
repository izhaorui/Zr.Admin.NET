using SqlSugar;
using ZR.Workflow.Model;
using ZR.Workflow.Model.Dto;
using ZR.Workflow.Service.IService;

namespace ZR.Workflow.Service
{
    /// <summary>
    /// 审批记录服务
    /// </summary>
    [AppService(ServiceType = typeof(IWfFlowRecordService))]
    public class WfFlowRecordService : BaseService<WfFlowRecord>, IWfFlowRecordService
    {
        public PagedInfo<WfFlowRecordDto> GetList(WfFlowRecordQueryDto parm)
        {
            var query = Context.Queryable<WfFlowRecord>()
                .LeftJoin<WfFlowNode>((r, n) => r.NodeId == n.NodeId)
                .LeftJoin<WfFlowInstance>((r, n, i) => r.InstanceId == i.InstanceId)
                .LeftJoin<WfFlowDefinition>((r, n, i, d) => i.FlowId == d.FlowId)
                .WhereIF(parm.InstanceId != null, (r, n, i, d) => r.InstanceId == parm.InstanceId)
                .WhereIF(!string.IsNullOrEmpty(parm.Title), (r, n, i, d) => i.Title.Contains(parm.Title))
                .OrderBy(r => r.RecordId)
                .Select((r, n, i, d) => new WfFlowRecordDto
                {
                    RecordId = r.RecordId,
                    TaskId = r.TaskId,
                    InstanceId = r.InstanceId,
                    NodeId = r.NodeId,
                    NodeName = n.NodeName,
                    Title = i.Title,
                    FlowName = SqlFunc.IsNull(i.FlowName, d.FlowName),
                    ApplyUser = i.ApplyUser,
                    InstanceStatus = i.Status,
                    Operator = r.Operator,
                    Action = r.Action,
                    Opinion = r.Opinion,
                    Create_time = r.Create_time
                });
            return query.ToPage(parm);
        }

        /// <summary>
        /// 抄送给我：筛选操作人为当前用户且标记为抄送的记录
        /// </summary>
        public PagedInfo<WfFlowRecordDto> GetCcList(WfFlowRecordQueryDto parm, string userName)
        {
            var query = Context.Queryable<WfFlowRecord>()
                .LeftJoin<WfFlowNode>((r, n) => r.NodeId == n.NodeId)
                .LeftJoin<WfFlowInstance>((r, n, i) => r.InstanceId == i.InstanceId)
                .LeftJoin<WfFlowDefinition>((r, n, i, d) => i.FlowId == d.FlowId)
                .Where((r, n, i, d) => r.Opinion == "抄送")
                .Where((r, n, i, d) => SqlFunc.Contains(SqlFunc.MergeString(",", r.Operator, ","), "," + userName + ","))
                .WhereIF(parm.InstanceId != null, (r, n, i, d) => r.InstanceId == parm.InstanceId)
                .WhereIF(!string.IsNullOrEmpty(parm.Title), (r, n, i, d) => i.Title.Contains(parm.Title))
                .OrderBy(r => r.RecordId, OrderByType.Desc)
                .Select((r, n, i, d) => new WfFlowRecordDto
                {
                    RecordId = r.RecordId,
                    InstanceId = r.InstanceId,
                    NodeId = r.NodeId,
                    NodeName = n.NodeName,
                    Title = i.Title,
                    FlowName = SqlFunc.IsNull(i.FlowName, d.FlowName),
                    ApplyUser = i.ApplyUser,
                    InstanceStatus = i.Status,
                    Operator = r.Operator,
                    Opinion = r.Opinion,
                    Create_time = r.Create_time
                });
            return query.ToPage(parm);
        }
    }
}
