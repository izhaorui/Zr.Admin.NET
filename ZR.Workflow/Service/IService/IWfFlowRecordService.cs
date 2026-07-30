using System.Collections.Generic;
using ZR.Workflow.Model.Dto;

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
    }
}
