using ZR.Model.System;

namespace ZR.Workflow.Model
{
    /// <summary>
    /// 审批评论 / 批注（独立于审批动作，不推进流程，用于节点内交流）
    /// </summary>
    [SugarTable("wf_flow_comment", "审批评论")]
    public class WfFlowComment : SysBase
    {
        /// <summary>主键</summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long CommentId { get; set; }

        /// <summary>流程实例Id</summary>
        [SugarColumn(ExtendedAttribute = ProteryConstant.NOTNULL)]
        public long InstanceId { get; set; }

        /// <summary>节点Id（可选，不指定表示针对整个流程）</summary>
        [SugarColumn(IsNullable = true)]
        public long? NodeId { get; set; }

        /// <summary>关联任务Id（可选，针对某条待办评论）</summary>
        [SugarColumn(IsNullable = true)]
        public long? TaskId { get; set; }

        /// <summary>评论人（登录名）</summary>
        [SugarColumn(Length = 64, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string UserName { get; set; }

        /// <summary>评论人Id（稳定外键）</summary>
        [SugarColumn(IsNullable = true)]
        public long? UserId { get; set; }

        /// <summary>评论人昵称（快照）</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string NickName { get; set; }

        /// <summary>评论内容</summary>
        [SugarColumn(Length = 500, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string Comment { get; set; }
    }
}
