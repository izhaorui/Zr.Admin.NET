using ZR.Workflow.Model;

namespace ZR.Workflow.Service.IService
{
    /// <summary>
    /// 工作流流转引擎（可替换点：未来接入 Elsa/Workflow Core 时仅替换实现）
    /// </summary>
    public interface IWfEngineService
    {
        /// <summary>
        /// 发起申请：按节点顺序生成首节点待办，实例置为审批中（事务）
        /// </summary>
        long Start(WfFlowInstance instance);

        /// <summary>
        /// 通过
        /// </summary>
        void Approve(long taskId, string opinion, string operatorName);

        /// <summary>
        /// 驳回
        /// </summary>
        void Reject(long taskId, string opinion, string operatorName);

        /// <summary>
        /// 撤回（仅首节点未处理时）
        /// </summary>
        void Withdraw(long instanceId, string operatorName);
    }
}
