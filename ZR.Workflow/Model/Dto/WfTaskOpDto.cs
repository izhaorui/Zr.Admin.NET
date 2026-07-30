namespace ZR.Workflow.Model.Dto
{
    /// <summary>
    /// 转办入参
    /// </summary>
    public class WfTransferInput
    {
        /// <summary>任务Id</summary>
        public long TaskId { get; set; }

        /// <summary>转办目标用户</summary>
        public string TargetUser { get; set; }

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

        /// <summary>加签用户列表</summary>
        public List<string> Users { get; set; } = new();

        /// <summary>加签说明</summary>
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
