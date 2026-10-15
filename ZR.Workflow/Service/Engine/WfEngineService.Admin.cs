namespace ZR.Workflow.Service
{
    public partial class WfEngineService
    {
        #region 管理员运维操作（终止 / 挂起 / 恢复 / 改派 / 跳转）

        /// <summary>
        /// 管理员强制终止 / 作废流程（不可逆）。把所有未完成任务置为 Skipped，实例置 Terminated，
        /// 记一条终止记录并通知申请人/相关人。仅由 Controller 的权限过滤器保证只有管理员可调用。
        /// </summary>
        /// <param name="instanceId">流程实例Id</param>
        /// <param name="operatorId">操作管理员 userId</param>
        /// <param name="opinion">终止原因（可选）</param>
        public Task AdminTerminate(long instanceId, long operatorId, string opinion)
        {
            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == instanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.Status == (int)WfInstanceStatus.Terminated)
                throw new CustomException("流程已终止，不可重复操作");
            if (instance.Status == (int)WfInstanceStatus.Approved)
                throw new CustomException("流程已通过，不可终止");
            if (instance.Status == (int)WfInstanceStatus.Withdrawn)
                throw new CustomException("流程已撤回，不可终止");

            var op = LoadUser(operatorId);
            var def = LoadActivatableDefinition(instance.FlowId);
            var openTaskIds = Context.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == instanceId && t.Status != (int)WfTaskStatus.Done && t.Status != (int)WfTaskStatus.Skipped)
                .Select(t => t.AssigneeId).ToList();

            RunInTx(() =>
            {
                // 所有未完成任务置为跳过
                var openTasks = Context.Queryable<WfFlowTask>()
                    .Where(t => t.InstanceId == instanceId && t.Status != (int)WfTaskStatus.Done && t.Status != (int)WfTaskStatus.Skipped)
                    .ToList();
                foreach (var t in openTasks)
                {
                    t.Status = (int)WfTaskStatus.Skipped;
                    t.Action = (int)WfAction.Terminate;
                    t.Opinion = opinion;
                    t.HandleTime = DateTime.Now;
                    Context.Updateable(t).ExecuteCommand();
                }

                instance.Status = (int)WfInstanceStatus.Terminated;
                SetActiveNodeIds(instance, new List<long>());
                SyncActiveNodeId(instance);
                Context.Updateable(instance).ExecuteCommand();

                AddRecord(instanceId, null, null, op, (int)WfAction.Terminate, opinion);
            }, "AdminTerminate");

            var msg = $"流程【{def.FlowName}】已被管理员{op.NickName}终止";
            NotifyUser(instance.ApplyUserId, msg);
            NotifyUserIds(openTaskIds, msg);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 管理员挂起流程（暂停流转，等待恢复）。仅运行中实例可挂起；挂起期间普通审批操作应被前端隐藏，
        /// 本方法仅置状态，不改动任务。恢复请调 <see cref="AdminResume"/>。
        /// </summary>
        public Task AdminSuspend(long instanceId, long operatorId, string opinion)
        {
            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == instanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("仅运行中的流程可挂起");

            var op = LoadUser(operatorId);
            var def = LoadActivatableDefinition(instance.FlowId);

            RunInTx(() =>
            {
                instance.Status = (int)WfInstanceStatus.Suspended;
                Context.Updateable(instance).ExecuteCommand();
                AddRecord(instanceId, null, null, op, (int)WfAction.Suspend, opinion);
            }, "AdminSuspend");

            var msg = $"流程【{def.FlowName}】已被管理员{op.NickName}挂起";
            NotifyUser(instance.ApplyUserId, msg);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 管理员恢复被挂起的流程。仅 Suspended 态可恢复，恢复后回到 Approval 流转。
        /// </summary>
        public Task AdminResume(long instanceId, long operatorId, string opinion)
        {
            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == instanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.Status != (int)WfInstanceStatus.Suspended)
                throw new CustomException("仅被挂起的流程可恢复");

            var op = LoadUser(operatorId);
            var def = LoadActivatableDefinition(instance.FlowId);

            RunInTx(() =>
            {
                instance.Status = (int)WfInstanceStatus.Approval;
                Context.Updateable(instance).ExecuteCommand();
                AddRecord(instanceId, null, null, op, (int)WfAction.Resume, opinion);
            }, "AdminResume");

            var msg = $"流程【{def.FlowName}】已被管理员{op.NickName}恢复";
            NotifyUser(instance.ApplyUserId, msg);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 管理员改派：把指定节点的全部未完成任务（审批/抄送）重新分配给目标用户。
        /// 适用于审批人离职/失联，管理员需把卡住的待办改给其他人。节点不存在任务时抛异常。
        /// 改派后 Pending 待办按当前时间重算超时窗口（Waiting 排队不计时，轮到激活时再计算）。
        /// </summary>
        /// <param name="instanceId">流程实例Id</param>
        /// <param name="nodeId">目标节点（实例当前所处或任意未完成任务所属节点）</param>
        /// <param name="targetUserId">改派目标用户 userId</param>
        /// <param name="operatorId">操作管理员 userId</param>
        /// <param name="opinion">改派说明（可选）</param>
        public Task AdminReassign(long instanceId, long nodeId, long targetUserId, long operatorId, string opinion)
        {
            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == instanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.Status != (int)WfInstanceStatus.Approval && instance.Status != (int)WfInstanceStatus.Suspended)
                throw new CustomException("仅运行中或挂起态的流程可改派");

            var op = LoadUser(operatorId);
            var target = LoadUser(targetUserId);
            var def = LoadActivatableDefinition(instance.FlowId);
            // 节点超时配置：改派后按当前时间重算超时窗口（管理员改派通常为救活卡单，新受理人须获得完整 TimeoutHours）
            var node = Context.Queryable<WfFlowNode>().First(n => n.NodeId == nodeId)
                ?? throw new CustomException("流程节点不存在");

            // 业务校验放 RunInTx 之前，确保异常消息能透传给调用方（RunInTx 会用 errorLabel 覆盖内部异常）
            var tasks = Context.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == instanceId && t.NodeId == nodeId
                    && t.Status != (int)WfTaskStatus.Done && t.Status != (int)WfTaskStatus.Skipped)
                .ToList();
            if (tasks.Count == 0)
                throw new CustomException("该节点无可改派的未完成任务");

            RunInTx(() =>
            {
                var now = DateTime.Now;
                foreach (var t in tasks)
                {
                    t.AssigneeId = target.UserId;
                    t.Assignee = target.UserName;
                    t.AssigneeNickName = target.NickName;
                    t.DelegateId = null;
                    t.DelegateName = null;
                    t.IsRead = false;
                    // 超时窗口跟随新受理人重算：仅对 Pending 生效（Waiting 排队不计时，轮到激活时按当时时间计算）
                    if (t.Status == (int)WfTaskStatus.Pending)
                    {
                        t.ArriveTime = now;
                        t.DeadlineTime = ComputeDeadline(node, now);
                    }
                    Context.Updateable(t).ExecuteCommand();
                    AddRecord(instanceId, t.TaskId, nodeId, op, (int)WfAction.Reassign, $"改派给 {target.NickName}{(string.IsNullOrEmpty(opinion) ? "" : $"：{opinion}")}");
                }
            }, "AdminReassign");

            NotifyUser((long?)target.UserId, $"【审批改派】您有流程【{def.FlowName}】的待办已被管理员{op.NickName}改派给您");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 管理员跳转节点：把卡住的实例直接跳到指定节点（重新激活该节点，生成其待办/抄送），
        /// 清空当前活动集与未完成任务。用于流程设计变更后修复在途实例、或绕过异常节点。不可逆。
        /// </summary>
        /// <param name="instanceId">流程实例Id</param>
        /// <param name="targetNodeId">跳转目标节点（必须存在于该流程且非结束节点）</param>
        /// <param name="operatorId">操作管理员 userId</param>
        /// <param name="opinion">跳转说明（可选）</param>
        public Task AdminJump(long instanceId, long targetNodeId, long operatorId, string opinion)
        {
            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == instanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.Status != (int)WfInstanceStatus.Approval && instance.Status != (int)WfInstanceStatus.Suspended)
                throw new CustomException("仅运行中或挂起态的流程可跳转");

            var op = LoadUser(operatorId);
            var def = LoadActivatableDefinition(instance.FlowId);
            var topo = LoadTopology(instance.FlowId);
            var target = topo.GetNode(targetNodeId)
                ?? throw new CustomException("跳转目标节点不存在");

            var formValues = ParseFormValues(instance);

            RunInTx(() =>
            {
                // 清空当前活动集，把在途未完成任务置为跳过
                var openTasks = Context.Queryable<WfFlowTask>()
                    .Where(t => t.InstanceId == instanceId && t.Status != (int)WfTaskStatus.Done && t.Status != (int)WfTaskStatus.Skipped)
                    .ToList();
                foreach (var t in openTasks)
                {
                    t.Status = (int)WfTaskStatus.Skipped;
                    t.Action = (int)WfAction.Jump;
                    t.Opinion = "管理员跳转，原待办作废";
                    t.HandleTime = DateTime.Now;
                    Context.Updateable(t).ExecuteCommand();
                }

                // 恢复流转态（若当前为挂起）+ 清空旧活动集并落库：
                // 若不先 SetActiveNodeIds(empty)，并行态跳转时旧活动节点(CurrentNodeIds)会残留，
                // ArriveNode 只 AddActiveNodeId(target) → 活动集变成 [旧A,旧B,target]，CurrentNodeId=Min(旧节点)，
                // 前端高亮错乱且单值指针取到已跳过节点。参照 RollbackToNode 的写法重置活动集并持久化 Status。
                SetActiveNodeIds(instance, new List<long>());
                instance.Status = (int)WfInstanceStatus.Approval;
                Context.Updateable(instance)
                    .UpdateColumns(i => new { i.CurrentNodeId, i.CurrentNodeIds, i.Status })
                    .ExecuteCommand();

                AddRecord(instanceId, null, targetNodeId, op, (int)WfAction.Jump, $"跳转到节点【{target.NodeName}】{(string.IsNullOrEmpty(opinion) ? "" : $"：{opinion}")}");

                // 重新激活目标节点（条件/网关节点会自行顺延或 fork，无需人工处理）。
                // singleNodeOnly=true：目标若是并行分组内成员，只激活该节点本身（生成其待办/抄送），
                // 组内其它分支的未完成任务已在上面统一置 Skipped → 并行汇聚判定其已完成，目标分支通过后即可放行，
                // 不会整组重新 fork、不会多余分支高亮、不会卡死。参照业界（Activiti/钉钉等）单令牌跳转语义。
                ArriveNode(instance, target, topo, formValues, singleNodeOnly: true);
                SyncActiveNodeId(instance);
            }, "AdminJump");

            var msg = $"流程【{def.FlowName}】已被管理员{op.NickName}跳转至节点【{target.NodeName}】";
            NotifyUser(instance.ApplyUserId, msg);
            return Task.CompletedTask;
        }

        #endregion
    }
}
