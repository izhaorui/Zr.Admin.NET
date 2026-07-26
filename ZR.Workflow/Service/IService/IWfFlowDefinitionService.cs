using ZR.Workflow.Model.Dto;

namespace ZR.Workflow.Service.IService
{
    /// <summary>
    /// 流程定义与节点配置服务
    /// </summary>
    public interface IWfFlowDefinitionService
    {
        PagedInfo<WfFlowDefinitionDto> GetList(WfFlowDefinitionQueryDto parm);
        WfFlowDefinitionDto GetInfo(long flowId);
        List<WfFlowNodeDto> GetNodes(long flowId);
        WfFlowDefinition Add(WfFlowDefinitionDto dto);
        int Update(WfFlowDefinitionDto dto);
        int Delete(long[] ids);
        /// <summary>
        /// 复制流程定义及其节点配置，生成一份停用状态的副本
        /// </summary>
        long Copy(long flowId, string userName);
    }
}
