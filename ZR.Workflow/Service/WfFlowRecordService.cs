using System.Collections.Generic;
using ZR.Workflow.Enum;

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
            var query = BuildRecordQuery()
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
                    ApplyNickName = i.ApplyNickName,
                    InstanceStatus = i.Status,
                    Operator = r.Operator,
                    OperatorNickName = r.OperatorNickName,
                    Action = r.Action,
                    IsRead = r.IsRead,
                    Opinion = r.Opinion,
                    Create_time = r.Create_time
                });
            return query.ToPage(parm);
        }

        /// <summary>
        /// 抄送给我：筛选操作人为当前用户且标记为抄送的记录（按 userId 精确匹配）
        /// </summary>
        public PagedInfo<WfFlowRecordDto> GetCcList(WfFlowRecordQueryDto parm, long userId)
        {
            var query = BuildRecordQuery()
                .Where((r, n, i, d) => r.Action == (int)WfAction.Cc)
                .Where((r, n, i, d) => r.OperatorId == userId)
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
                    ApplyNickName = i.ApplyNickName,
                    InstanceStatus = i.Status,
                    Operator = r.Operator,
                    OperatorNickName = r.OperatorNickName,
                    IsRead = r.IsRead,
                    Opinion = r.Opinion,
                    Create_time = r.Create_time
                });
            return query.ToPage(parm);
        }

        private ISugarQueryable<WfFlowRecord, WfFlowNode, WfFlowInstance, WfFlowDefinition> BuildRecordQuery()
        {
            return Context.Queryable<WfFlowRecord>()
                .LeftJoin<WfFlowNode>((r, n) => r.NodeId == n.NodeId)
                .LeftJoin<WfFlowInstance>((r, n, i) => r.InstanceId == i.InstanceId)
                .LeftJoin<WfFlowDefinition>((r, n, i, d) => i.FlowId == d.FlowId);
        }

        /// <summary>
        /// 标记抄送已读（仅更新当前用户作为收件人的记录，防止越权标记他人）
        /// </summary>
        public void Read(List<long> ids, long userId)
        {
            if (ids == null || ids.Count == 0) return;
            Context.Updateable<WfFlowRecord>()
                .SetColumns(r => new WfFlowRecord { IsRead = true })
                .Where(r => ids.Contains(r.RecordId) && r.OperatorId == userId)
                .ExecuteCommand();
        }
    }
}
