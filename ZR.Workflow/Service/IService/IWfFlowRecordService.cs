namespace ZR.Workflow.Service.IService
{
    /// <summary>
    /// 审批记录服务
    /// </summary>
    public interface IWfFlowRecordService
    {
        PagedInfo<WfFlowRecordDto> GetList(WfFlowRecordQueryDto parm);
        PagedInfo<WfFlowRecordDto> GetCcList(WfFlowRecordQueryDto parm, long userId);
        void Read(List<long> ids, long userId);
        /// <summary>当前用户抄送未读数量（Action=Cc 且 IsRead=false）</summary>
        int GetUnreadCount(long userId);
        /// <summary>手动生成/重生成单条审批记录的 AI 摘要并写回，返回摘要文本</summary>
        Task<string> RegenerateSummary(long recordId);
    }
}
