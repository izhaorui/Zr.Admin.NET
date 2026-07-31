using System.Collections.Generic;

namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 审批评论查询
    /// </summary>
    public class WfFlowCommentQueryDto : PagerInfo
    {
        /// <summary>流程实例Id（必填）</summary>
        public long? InstanceId { get; set; }

        /// <summary>节点Id（可选，按节点筛选）</summary>
        public long? NodeId { get; set; }
    }

    /// <summary>
    /// 审批评论新增入参
    /// </summary>
    public class WfFlowCommentInput
    {
        /// <summary>流程实例Id</summary>
        [Required(ErrorMessage = "流程实例Id不能为空")]
        public long InstanceId { get; set; }

        /// <summary>节点Id（可选）</summary>
        public long? NodeId { get; set; }

        /// <summary>关联任务Id（可选）</summary>
        public long? TaskId { get; set; }

        /// <summary>评论内容</summary>
        [Required(ErrorMessage = "评论内容不能为空")]
        public string Comment { get; set; }
    }

    /// <summary>
    /// 审批评论对象
    /// </summary>
    public class WfFlowCommentDto : SysBase
    {
        /// <summary>评论Id</summary>
        public long CommentId { get; set; }

        /// <summary>流程实例Id</summary>
        public long InstanceId { get; set; }

        /// <summary>节点Id</summary>
        public long? NodeId { get; set; }

        /// <summary>任务Id</summary>
        public long? TaskId { get; set; }

        /// <summary>评论人</summary>
        public string UserName { get; set; }

        /// <summary>评论人昵称</summary>
        public string NickName { get; set; }

        /// <summary>评论内容</summary>
        public string Comment { get; set; }
    }
}
