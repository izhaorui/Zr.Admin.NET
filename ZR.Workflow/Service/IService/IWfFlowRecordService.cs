namespace ZR.Workflow.Service.IService
{
    /// <summary>
    /// 审批记录服务
    /// </summary>
    public interface IWfFlowRecordService
    {
        /// <summary>
        /// 审批记录列表。非管理员（adminView=false）仅返回本人参与的实例记录：
        /// 本人发起 / 本人是审批人或代审人 / 本人被抄送。
        /// </summary>
        PagedInfo<WfFlowRecordDto> GetList(WfFlowRecordQueryDto parm, long? viewerId = null, bool adminView = false);
        PagedInfo<WfFlowRecordDto> GetCcList(WfFlowRecordQueryDto parm, long userId);
        void Read(List<long> ids, long userId);
        /// <summary>当前用户抄送未读数量（Action=Cc 且 IsRead=false）</summary>
        int GetUnreadCount(long userId);
        /// <summary>手动生成/重生成单条审批记录的 AI 摘要并写回，返回摘要文本</summary>
        Task<string> RegenerateSummary(long recordId);
    }
}
