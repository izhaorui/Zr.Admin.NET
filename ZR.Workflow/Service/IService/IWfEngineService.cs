namespace ZR.Workflow.Service.IService
{
    /// <summary>
    /// 工作流流转引擎（可替换点：未来接入 Elsa/Workflow Core 时仅替换实现）。
    ///
    /// 标识约定：所有"人"的入参一律使用 <c>userId</c>（SysUser.UserId），不再使用登录名（userName）。
    /// 原因：userName 可被修改，用它做鉴权比对与记录归属会在改名后失效；且引擎内部需反查用户表拿 Id/昵称，
    /// 徒增查询。运行态落库的 <c>WfFlowTask.AssigneeId</c> / <c>WfFlowRecord.OperatorId</c> 均为 userId，
    /// 入参直接用 userId 可与之直接比对。展示用的 userName / nickName 由引擎按 Id 一次性查出并快照落库。
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
        /// <param name="taskId">任务Id</param>
        /// <param name="opinion">审批意见</param>
        /// <param name="operatorId">操作人 userId（须为该任务的 AssigneeId）</param>
        void Approve(long taskId, string opinion, long operatorId);

        /// <summary>
        /// 驳回
        /// </summary>
        /// <param name="taskId">任务Id</param>
        /// <param name="opinion">驳回原因</param>
        /// <param name="operatorId">操作人 userId（须为该任务的 AssigneeId）</param>
        void Reject(long taskId, string opinion, long operatorId);

        /// <summary>
        /// 重新提交：驳回后由申请人修改表单再次发起，回到首节点重新审批
        /// </summary>
        /// <param name="operatorId">操作人 userId（须为原申请人 ApplyUserId）</param>
        void Resubmit(long instanceId, string formContent, string attachment, string title, long operatorId);

        /// <summary>
        /// 撤回（仅当前节点未处理时）
        /// </summary>
        /// <param name="operatorId">操作人 userId（须为原申请人 ApplyUserId）</param>
        void Withdraw(long instanceId, long operatorId);

        /// <summary>
        /// 转办：将当前待办转移给其他用户处理
        /// </summary>
        /// <param name="taskId">任务Id</param>
        /// <param name="targetUserId">转办目标 userId</param>
        /// <param name="opinion">转办说明</param>
        /// <param name="operatorId">操作人 userId（须为该任务的 AssigneeId）</param>
        void Transfer(long taskId, long targetUserId, string opinion, long operatorId);

        /// <summary>
        /// 加签：在当前节点增加额外审批人
        /// </summary>
        /// <param name="taskId">任务Id</param>
        /// <param name="userIds">加签人 userId 列表</param>
        /// <param name="opinion">加签说明</param>
        /// <param name="operatorId">操作人 userId（须为该任务的 AssigneeId）</param>
        void AddSign(long taskId, List<long> userIds, string opinion, long operatorId);

        /// <summary>
        /// 委托代审：原审批人将当前待办委托给他人代审（任务仍归属原审批人，仅记录代审人）
        /// </summary>
        /// <param name="taskId">任务Id</param>
        /// <param name="targetUserId">代审人 userId</param>
        /// <param name="opinion">委托说明</param>
        /// <param name="operatorId">操作人 userId（须为该任务的 AssigneeId，且未重复委托）</param>
        void Delegate(long taskId, long targetUserId, string opinion, long operatorId);

        /// <summary>
        /// 减签：移除本节点某审批人（将其待办置 Skipped 并重新判定节点完成）
        /// </summary>
        /// <param name="taskId">操作人自己的任务 Id（用于鉴权该节点）</param>
        /// <param name="targetUserId">被减签的审批人 userId（须为该节点 Pending/Waiting 任务）</param>
        /// <param name="opinion">减签说明</param>
        /// <param name="operatorId">操作人 userId（须为该节点某一任务的审批人）</param>
        void RemoveSign(long taskId, long targetUserId, string opinion, long operatorId);

        /// <summary>
        /// 管理员强制终止 / 作废流程（不可逆）
        /// </summary>
        /// <param name="instanceId">流程实例Id</param>
        /// <param name="operatorId">操作管理员 userId</param>
        /// <param name="opinion">终止原因</param>
        Task AdminTerminate(long instanceId, long operatorId, string opinion);

        /// <summary>
        /// 管理员挂起流程（暂停流转）
        /// </summary>
        Task AdminSuspend(long instanceId, long operatorId, string opinion);

        /// <summary>
        /// 管理员恢复被挂起的流程
        /// </summary>
        Task AdminResume(long instanceId, long operatorId, string opinion);

        /// <summary>
        /// 管理员改派：把指定节点的全部未完成任务改给目标用户
        /// </summary>
        /// <param name="nodeId">目标节点</param>
        /// <param name="targetUserId">改派目标用户 userId</param>
        Task AdminReassign(long instanceId, long nodeId, long targetUserId, long operatorId, string opinion);

        /// <summary>
        /// 管理员跳转节点：把卡住的实例直接跳到指定节点重新激活
        /// </summary>
        /// <param name="targetNodeId">跳转目标节点（必须存在且非结束节点）</param>
        Task AdminJump(long instanceId, long targetNodeId, long operatorId, string opinion);
    }
}
