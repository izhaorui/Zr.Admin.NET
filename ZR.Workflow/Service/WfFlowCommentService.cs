namespace ZR.Workflow.Service
{
    /// <summary>
    /// 审批评论 / 批注服务
    /// </summary>
    [AppService(ServiceType = typeof(IWfFlowCommentService))]
    public class WfFlowCommentService : BaseService<WfFlowComment>, IWfFlowCommentService
    {
        /// <summary>
        /// 评论列表（按实例 + 可选节点，按时间正序，便于对话式展示）
        /// </summary>
        public PagedInfo<WfFlowCommentDto> GetList(WfFlowCommentQueryDto parm)
        {
            var query = Queryable()
                .WhereIF(parm.InstanceId != null, c => c.InstanceId == parm.InstanceId)
                .WhereIF(parm.NodeId != null, c => c.NodeId == parm.NodeId)
                .OrderBy(c => c.Create_time, OrderByType.Asc)
                .Select(c => new WfFlowCommentDto
                {
                    CommentId = c.CommentId,
                    InstanceId = c.InstanceId,
                    NodeId = c.NodeId,
                    TaskId = c.TaskId,
                    UserName = c.UserName,
                    NickName = c.NickName,
                    Comment = c.Comment,
                    Create_time = c.Create_time
                });
            return query.ToPage(parm);
        }

        /// <summary>
        /// 新增评论（按当前用户写入，不推进流程）
        /// </summary>
        public void Add(WfFlowCommentInput parm, LoginUser user)
        {
            var entity = new WfFlowComment
            {
                InstanceId = parm.InstanceId,
                NodeId = parm.NodeId,
                TaskId = parm.TaskId,
                Comment = parm.Comment,
                UserName = user.UserName,
                UserId = user.UserId,
                NickName = user.NickName
            };
            InsertReturnEntity(entity);
        }
    }
}
