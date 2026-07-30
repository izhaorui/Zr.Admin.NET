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
        /// 重新提交：驳回后由申请人修改表单再次发起，回到首节点重新审批
        /// </summary>
        void Resubmit(long instanceId, string formContent, string attachment, string title, string operatorName);

        /// <summary>
        /// 撤回（仅首节点未处理时）
        /// </summary>
        void Withdraw(long instanceId, string operatorName);

        /// <summary>
        /// 转办：将当前待办转移给其他用户处理
        /// </summary>
        void Transfer(long taskId, string targetUser, string opinion, string operatorName);

        /// <summary>
        /// 加签：在当前节点增加额外审批人
        /// </summary>
        void AddSign(long taskId, List<string> users, string opinion, string operatorName);
    }
}
