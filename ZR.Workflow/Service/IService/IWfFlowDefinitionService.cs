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
        /// <summary>
        /// 另存为新版本：复制当前定义与节点到新的 FlowId，Version 自增，旧版本冻结保留
        /// </summary>
        long SaveAsNewVersion(long flowId, string userName);
        /// <summary>
        /// 查询某流程编码下的全部版本（按 Version 升序），并标记现行版本(IsCurrent)
        /// </summary>
        List<WfFlowDefinitionDto> GetVersions(string flowCode);
        /// <summary>
        /// 设为现行版本：启用该版本(Status=1,IsDraft=0)并停用同 FlowCode 下其他版本，保证现行版本唯一
        /// </summary>
        int SetCurrentVersion(long flowId, string userName);
        /// <summary>
        /// 发布草稿版本：将 IsDraft=1 的草稿转为正式(IsDraft=0)，不会自动停用其他版本
        /// </summary>
        int Publish(long flowId, string userName);
        /// <summary>
        /// 版本回滚：将指定历史版本复制为新的最高版本（草稿态），保留完整版本链路
        /// </summary>
        long Rollback(long flowId, string userName);
    }
}
