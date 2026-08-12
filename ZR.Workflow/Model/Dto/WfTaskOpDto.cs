namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 转办入参
    /// </summary>
    public class WfTransferInput
    {
        /// <summary>任务Id</summary>
        public long TaskId { get; set; }

        /// <summary>转办目标用户 userId（稳定标识，不使用可变的登录名）</summary>
        public long TargetUserId { get; set; }

        /// <summary>转办说明</summary>
        public string Opinion { get; set; }
    }

    /// <summary>
    /// 加签入参
    /// </summary>
    public class WfAddSignInput
    {
        /// <summary>任务Id</summary>
        public long TaskId { get; set; }

        /// <summary>加签用户 userId 列表（稳定标识，不使用可变的登录名）</summary>
        public List<long> UserIds { get; set; } = new();

        /// <summary>加签说明</summary>
        public string Opinion { get; set; }
    }

    /// <summary>
    /// 委托代审入参（委托不转移任务归属，仅记录实际代审人）
    /// </summary>
    public class WfDelegateInput
    {
        /// <summary>任务Id</summary>
        public long TaskId { get; set; }

        /// <summary>代审人 userId（稳定标识，不使用可变的登录名）</summary>
        public long TargetUserId { get; set; }

        /// <summary>委托说明</summary>
        public string Opinion { get; set; }
    }

    /// <summary>
    /// 标记已读入参（Ids 为逗号分隔的主键，支持批量）
    /// </summary>
    public class WfReadInput
    {
        /// <summary>主键列表（逗号分隔）</summary>
        public string Ids { get; set; }
    }
}
