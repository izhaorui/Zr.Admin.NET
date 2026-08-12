using ZR.ServiceCore.Services;

namespace ZR.Workflow.Service
{
    /// <summary>
    /// 工作流流转引擎（标准版）。
    ///
    /// 架构概览：
    /// <list type="bullet">
    /// <item>节点定义（<see cref="WfFlowNode"/>）：表达流程的静态结构，按 NodeOrder 升序串联；
    /// ApproverType 区分指定用户 / 角色 / 部门 / 表单字段四类解析方式。</item>
    /// <item>任务池（<see cref="WfFlowTask"/>）：实例运行时的待办/抄送任务，Status 驱动节点完成判定。</item>
    /// <item>状态机（<c>WfFlowInstance.Status</c>）：Approval → (Approved | Rejected | Withdrawn)。</item>
    /// <item>记录（<see cref="WfFlowRecord"/>）：所有动作的轨迹日志，UI 据此渲染审批意见。</item>
    /// </list>
    ///
    /// 公共入口（Start / Approve / Reject / Resubmit / Withdraw / Transfer / AddSign）都遵循
    /// "pre-flight 校验 → <see cref="RunInTx"/> 事务 → 状态/任务/记录 落库 → <see cref="ArriveNode"/> /
    /// <see cref="AdvanceToNext"/> 推进"模式。ArriveNode / AdvanceToNext 负责节点流转，
    /// 包含顺序、条件、并行分组、抄送、结束判定。
    ///
    /// 标识约定：公共入口的"人"一律用 <c>userId</c>（见 <see cref="IWfEngineService"/>）。鉴权比对走
    /// <c>WfFlowTask.AssigneeId</c> / <c>WfFlowInstance.ApplyUserId</c>，不再比对可变的 userName；
    /// 展示用 userName / nickName 由 <see cref="LoadUser"/> 按 Id 查一次后快照落库。
    /// </summary>
    [AppService(ServiceType = typeof(IWfEngineService))]
    public class WfEngineService : BaseService<WfFlowInstance>, IWfEngineService
    {
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly ISysUserMsgService _msgService;

        public WfEngineService(ISysUserMsgService msgService)
        {
            _msgService = msgService;
        }

        #region 公共入口

        /// <summary>
        /// 发起申请
        /// </summary>
        public long Start(WfFlowInstance instance)
        {
            var (def, allNodes, linksBySource, linksByTarget, firstNode) = PrepareStartFlow(instance);
            logger.Info($"发起申请：FlowId={instance.FlowId} Title={instance.Title} 首节点={firstNode?.NodeName}({firstNode?.NodeId})");

            RunInTx(() =>
            {
                var now = DateTime.Now;

                instance.Status = (int)WfInstanceStatus.Approval;
                instance.CurrentNodeId = firstNode?.NodeId;
                instance.CurrentNodeIds = firstNode != null ? JsonConvert.SerializeObject(new[] { firstNode.NodeId }) : null;
                instance = InsertReturnEntity(instance) ?? throw new CustomException("发起申请失败");

                AddRecord(instance.InstanceId, null, null, ApplicantOf(instance), (int)WfAction.Submit, "发起申请");

                var formValues = ParseFormValues(instance);
                ArriveOrComplete(instance, firstNode, allNodes, linksBySource, linksByTarget, formValues);
            }, "发起申请失败");

            return instance.InstanceId;
        }

        /// <summary>
        /// 通过
        /// </summary>
        public void Approve(long taskId, string opinion, long operatorId)
        {
            var (task, instance) = LoadPendingTaskAndInstance(taskId, operatorId);
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("流程状态异常，无法审批");

            var op = LoadUser(operatorId);
            // 委托代审：操作人实为代审人，记录标注"代 X 审批"（X=原审批人昵称）
            var delegatedNote = IsDelegatedOperator(task, operatorId) ? $"（代 {task.AssigneeNickName} 审批）" : "";
            logger.Info($"审批通过：InstanceId={instance.InstanceId} TaskId={taskId} Node={task.NodeName}({task.NodeId}) 操作人={op.NickName}({operatorId}){delegatedNote}");

            var node = Context.Queryable<WfFlowNode>().First(n => n.NodeId == task.NodeId);
            var allNodes = LoadOrderedNodes(instance.FlowId);
            var linksBySource = LoadNodeLinks(instance.FlowId);
            var linksByTarget = LoadNodeLinksByTarget(instance.FlowId);
            // 活动集兜底初始化：存量实例可能无 CurrentNodeIds，用 CurrentNodeId 单值补齐，避免并行汇聚判定缺失
            if (string.IsNullOrWhiteSpace(instance.CurrentNodeIds) && instance.CurrentNodeId.HasValue)
            {
                instance.CurrentNodeIds = JsonConvert.SerializeObject(new[] { instance.CurrentNodeId.Value });
            }

            RunInTx(() =>
            {
                var now = DateTime.Now;
                Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask
                    {
                        Status = (int)WfTaskStatus.Done,
                        Action = (int)WfAction.Approve,
                        Opinion = opinion,
                        HandleTime = now,
                        Update_time = now,
                        Update_by = op.UserName
                    })
                    .Where(t => t.TaskId == taskId).ExecuteCommand();

                AddRecord(instance.InstanceId, taskId, task.NodeId, op, (int)WfAction.Approve, delegatedNote + opinion);

                // 依次审批：激活下一位等待中的审批人（前一人已通过，轮到下一顺位；不推进节点）
                if (node.SignType == (int)WfSignType.Sequential)
                {
                    var next = Context.Queryable<WfFlowTask>()
                        .Where(t => t.InstanceId == instance.InstanceId && t.NodeId == node.NodeId && t.Status == (int)WfTaskStatus.Waiting)
                        .OrderBy(t => t.TaskId)
                        .First();
                    if (next != null)
                    {
                        Context.Updateable<WfFlowTask>()
                            .SetColumns(t => new WfFlowTask { Status = (int)WfTaskStatus.Pending, Opinion = "" })
                            .Where(t => t.TaskId == next.TaskId).ExecuteCommand();
                        Notify(next.AssigneeId.Value, $"【审批待办】{instance.Title}（{instance.FlowName}），节点「{node.NodeName}」轮到您审批。");
                        logger.Info($"依次审批轮转：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId}) 轮到下一审批人({next.AssigneeId})");
                        return;
                    }
                }

                if (!IsNodeComplete(instance.InstanceId, node)) return;
                logger.Info($"节点完成：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId}) 满足签类型完成条件 → 推进后续");

                NotifyUser(instance.ApplyUserId, $"【审批进度】{instance.Title} 的「{node.NodeName}」节点已通过。");

                // 本节点已完成：跳过同节点其余待办，避免或签/并发下重复流转下一节点
                Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask { Status = (int)WfTaskStatus.Skipped })
                    .Where(t => t.InstanceId == instance.InstanceId && t.NodeId == node.NodeId && t.Status == (int)WfTaskStatus.Pending)
                    .ExecuteCommand();

                var formValues = ParseFormValues(instance);
                AdvanceToNext(instance, node, allNodes, linksBySource, linksByTarget, formValues);
            }, "审批失败");
        }

        /// <summary>
        /// 驳回
        /// </summary>
        public void Reject(long taskId, string opinion, long operatorId)
        {
            var (task, instance) = LoadPendingTaskAndInstance(taskId, operatorId);
            var op = LoadUser(operatorId);
            var delegatedNote = IsDelegatedOperator(task, operatorId) ? $"（代 {task.AssigneeNickName} 审批）" : "";
            logger.Info($"审批驳回：InstanceId={instance.InstanceId} TaskId={taskId} Node={task.NodeName}({task.NodeId}) 操作人={op.NickName}({operatorId}){delegatedNote}");
            var node = Context.Queryable<WfFlowNode>().First(n => n.NodeId == task.NodeId);
            var allNodes = LoadOrderedNodes(instance.FlowId);
            var linksBySource = LoadNodeLinks(instance.FlowId);
            var linksByTarget = LoadNodeLinksByTarget(instance.FlowId);

            RunInTx(() =>
            {
                var now = DateTime.Now;
                Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask
                    {
                        Status = (int)WfTaskStatus.Done,
                        Action = (int)WfAction.Reject,
                        Opinion = opinion,
                        HandleTime = now,
                        Update_time = now,
                        Update_by = op.UserName
                    })
                    .Where(t => t.TaskId == taskId).ExecuteCommand();

                AddRecord(instance.InstanceId, taskId, task.NodeId, op, (int)WfAction.Reject, delegatedNote + opinion);

                NotifyUser(instance.ApplyUserId, $"【审批驳回】{instance.Title} 被 {op.NickName} 驳回{(string.IsNullOrEmpty(opinion) ? "" : "：" + opinion)}");

                var strategy = (WfRejectStrategy)node.RejectStrategy;
                WfFlowNode targetNode = null;
                if (strategy == WfRejectStrategy.ToPrevNode)
                {
                    // 驳回到上一审批节点（NodeOrder 小于当前且为审批节点的最后一个）
                    targetNode = allNodes
                        .Where(n => n.NodeType == (int)WfNodeType.Audit && n.NodeOrder < node.NodeOrder)
                        .OrderByDescending(n => n.NodeOrder)
                        .FirstOrDefault();
                }
                else if (strategy == WfRejectStrategy.ToSpecifiedNode && node.RejectTargetNodeId.HasValue)
                {
                    targetNode = allNodes.FirstOrDefault(n => n.NodeId == node.RejectTargetNodeId.Value);
                }

                if (targetNode == null)
                {
                    // 退化策略：无上一节点 / 未配置指定节点 → 驳回发起人（默认行为）
                    logger.Info($"驳回退化：InstanceId={instance.InstanceId} 无回退目标节点 → 直接驳回发起人");
                    instance.Status = (int)WfInstanceStatus.Rejected;
                    Context.Updateable(instance).UpdateColumns(i => new { i.Status }).ExecuteCommand();
                    Context.Updateable<WfFlowTask>()
                        .SetColumns(t => new WfFlowTask { Status = (int)WfTaskStatus.Skipped })
                        .Where(t => t.InstanceId == instance.InstanceId && t.Status == (int)WfTaskStatus.Pending)
                        .ExecuteCommand();
                    return;
                }

                // 回退到目标节点重新审批：清掉目标及之后所有任务（轨迹保留在 WfFlowRecord），重置活动集并重新进入目标节点
                logger.Info($"驳回回退：InstanceId={instance.InstanceId} 回退到节点 {targetNode?.NodeName}({targetNode?.NodeId})（策略={(WfRejectStrategy)node.RejectStrategy}）");
                RollbackToNode(instance, targetNode, allNodes, linksBySource, linksByTarget);
            }, "驳回失败");
        }

        /// <summary>
        /// 将流程回退到指定节点重新审批（可配置驳回策略：驳回到上一步 / 指定节点）。
        /// 清掉目标节点及其之后所有任务，重置活动集为目标节点，重新触发该节点的进入/任务生成。
        /// 历史审批轨迹由 WfFlowRecord 保留，任务仅作为"当前待办"快照被清理。
        /// </summary>
        private void RollbackToNode(WfFlowInstance instance, WfFlowNode targetNode, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<long, List<WfNodeLink>> linksByTarget)
        {
            var cleanupIds = allNodes
                .Where(n => n.NodeOrder >= targetNode.NodeOrder)
                .Select(n => n.NodeId)
                .ToList();
            logger.Info($"回退重置：InstanceId={instance.InstanceId} 清理节点集 Order>={targetNode.NodeOrder}（{cleanupIds.Count} 个）并重新进入 {targetNode.NodeName}({targetNode.NodeId})");
            Context.Updateable<WfFlowTask>()
                .SetColumns(t => new WfFlowTask { Status = (int)WfTaskStatus.Skipped })
                .Where(t => t.InstanceId == instance.InstanceId && cleanupIds.Contains(t.NodeId))
                .ExecuteCommand();

            instance.CurrentNodeId = targetNode.NodeId;
            instance.CurrentNodeIds = JsonConvert.SerializeObject(new[] { targetNode.NodeId });
            instance.Status = (int)WfInstanceStatus.Approval;
            Context.Updateable(instance)
                .UpdateColumns(i => new { i.CurrentNodeId, i.CurrentNodeIds, i.Status })
                .ExecuteCommand();

            var formValues = ParseFormValues(instance);
            ArriveNode(instance, targetNode, allNodes, linksBySource, linksByTarget, formValues);
        }

        /// <summary>
        /// 重新提交：驳回后由申请人修改内容再次发起，实例回到首节点重新审批。
        /// 历史审批任务与记录保留作为轨迹；仅当实例处于驳回状态时可操作。
        /// </summary>
        public void Resubmit(long instanceId, string formContent, string attachment, string title, long operatorId)
        {
            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == instanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.ApplyUserId != operatorId)
                throw new CustomException("仅申请人可重新提交");
            if (instance.Status != (int)WfInstanceStatus.Rejected)
                throw new CustomException("当前状态不可重新提交");

            var op = LoadUser(operatorId);
            var (def, allNodes, linksBySource, linksByTarget, firstNode) = PrepareStartFlow(instance);
            logger.Info($"发起申请：FlowId={instance.FlowId} Title={instance.Title} 首节点={firstNode?.NodeName}({firstNode?.NodeId})");

            RunInTx(() =>
            {
                var now = DateTime.Now;
                instance.Status = (int)WfInstanceStatus.Approval;
                instance.CurrentNodeId = firstNode?.NodeId;
                instance.CurrentNodeIds = firstNode != null ? JsonConvert.SerializeObject(new[] { firstNode.NodeId }) : null;
                instance.FormContent = formContent;
                instance.Attachment = attachment;
                if (!string.IsNullOrEmpty(title)) instance.Title = title;
                instance.Update_time = now;
                instance.Update_by = op.UserName;
                Context.Updateable(instance)
                    .UpdateColumns(i => new { i.Status, i.CurrentNodeId, i.FormContent, i.Attachment, i.Title, i.Update_time, i.Update_by })
                    .ExecuteCommand();

                AddRecord(instanceId, null, null, op, (int)WfAction.Resubmit, "重新提交");

                var formValues = ParseFormValues(instance);
                ArriveOrComplete(instance, firstNode, allNodes, linksBySource, linksByTarget, formValues);
            }, "重新提交失败");
        }

        /// <summary>
        /// 撤回
        /// </summary>
        public void Withdraw(long instanceId, long operatorId)
        {
            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == instanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.ApplyUserId != operatorId)
                throw new CustomException("仅申请人可撤回");
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("当前状态不可撤回");

            var op = LoadUser(operatorId);
            logger.Info($"撤回申请：InstanceId={instanceId} 发起人={op.NickName}({operatorId})");

            // 仅当前审批节点尚未被处理时允许撤回；已被审批则流程已进入下一环节，不可撤回。
            // 放在事务外做预检，使业务校验异常直接抛出（不会被包裹成通用的"撤回失败"）。
            var currentNodeHandled = Context.Queryable<WfFlowTask>()
                .Any(t => t.InstanceId == instanceId
                         && t.NodeId == instance.CurrentNodeId
                         && t.Status == (int)WfTaskStatus.Done);
            if (currentNodeHandled)
                throw new CustomException("当前节点已审批，无法撤回");

            RunInTx(() =>
            {
                // 待办审批人直接取 AssigneeId（userId），无需再按登录名反查用户表
                var pendingAssigneeIds = Context.Queryable<WfFlowTask>()
                    .Where(t => t.InstanceId == instanceId && t.Status == (int)WfTaskStatus.Pending && t.AssigneeId != null)
                    .Select(t => t.AssigneeId)
                    .ToList();

                Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask { Status = (int)WfTaskStatus.Skipped })
                    .Where(t => t.InstanceId == instanceId && t.Status == (int)WfTaskStatus.Pending)
                    .ExecuteCommand();

                AddRecord(instanceId, null, null, op, (int)WfAction.Withdraw, "撤回申请");

                NotifyUserIds(pendingAssigneeIds, $"【审批撤回】{instance.Title} 已被申请人撤回。");

                instance.Status = (int)WfInstanceStatus.Withdrawn;
                logger.Info($"撤回完成：InstanceId={instanceId} → 状态=Withdrawn（已撤回）");
                Context.Updateable(instance).UpdateColumns(i => new { i.Status }).ExecuteCommand();
            }, "撤回失败");
        }

        /// <summary>
        /// 转办：将当前待办转移给目标用户（节点不变，由目标用户接手）
        /// </summary>
        public void Transfer(long taskId, long targetUserId, string opinion, long operatorId)
        {
            if (targetUserId <= 0) throw new CustomException("请选择转办人");
            if (targetUserId == operatorId) throw new CustomException("不能转办给自己");

            var (task, instance) = LoadPendingTaskAndInstance(taskId, operatorId);
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("流程状态异常，无法转办");

            var op = LoadUser(operatorId);
            logger.Info($"转办：InstanceId={instance.InstanceId} TaskId={taskId} Node={task.NodeName}({task.NodeId}) {op.NickName}({operatorId}) → 目标用户({targetUserId})");
            // 转办目标按 userId 取用户；不存在则直接拒绝（避免把任务转给一个无效 Id 造成流程卡死）
            var target = Context.Queryable<SysUser>().First(u => u.UserId == targetUserId)
                ?? throw new CustomException("转办人不存在");
            var targetName = target.UserName;
            var targetNickName = target.NickName;
            RunInTx(() =>
            {
                var now = DateTime.Now;
                Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask
                    {
                        Assignee = targetName,
                        AssigneeId = targetUserId,
                        AssigneeNickName = targetNickName,
                        Opinion = opinion,
                        Action = (int)WfAction.Transfer,
                        Update_time = now,
                        Update_by = op.UserName
                    })
                    .Where(t => t.TaskId == taskId).ExecuteCommand();

                var recordOpinion = "转办给 " + targetNickName + (string.IsNullOrEmpty(opinion) ? "" : "：" + opinion);
                AddRecord(instance.InstanceId, taskId, task.NodeId, op, (int)WfAction.Transfer, recordOpinion);

                Notify(targetUserId, $"【审批转办】{instance.Title} 由 {op.NickName} 转办给您处理。");
            }, "转办失败");
        }

        /// <summary>
        /// 加签：在当前审批节点追加额外审批人，新增待办纳入节点完成判定
        /// </summary>
        public void AddSign(long taskId, List<long> userIds, string opinion, long operatorId)
        {
            if (userIds == null || userIds.Count == 0) throw new CustomException("请选择加签人");

            var (task, instance) = LoadPendingTaskAndInstance(taskId, operatorId);
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("流程状态异常，无法加签");

            var op = LoadUser(operatorId);
            logger.Info($"加签：InstanceId={instance.InstanceId} TaskId={taskId} Node={task.NodeName}({task.NodeId}) 操作人={op.NickName}({operatorId}) 加签用户=[{string.Join(",", userIds)}]");
            // 去重按 AssigneeId（userId）比对，不再按可变的登录名
            var existingIds = Context.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == task.InstanceId && t.NodeId == task.NodeId && t.AssigneeId != null)
                .Select(t => t.AssigneeId)
                .ToList();
            var toAdd = userIds.Where(id => id > 0 && !existingIds.Contains(id))
                .Distinct().ToList();
            if (toAdd.Count == 0) throw new CustomException("加签人已在该节点审批人中");

            // 加签人前端传 userId，统一解析为 ResolvedApprover（带 UserName/NickName 快照）再落库
            var toAddApprovers = ResolveByUserIds(toAdd);
            if (toAddApprovers.Count == 0) throw new CustomException("加签人不存在");

            RunInTx(() =>
            {
                BatchCreateTasks(task.InstanceId, task.NodeId, task.NodeName, toAddApprovers, (int)WfTaskStatus.Pending, op.UserName);

                NotifyUsers(toAddApprovers, $"【审批加签】{instance.Title} 由 {op.NickName} 邀请您加签审批。");

                var recordOpinion = "加签：" + string.Join(",", toAddApprovers.Select(a => a.NickName)) + (string.IsNullOrEmpty(opinion) ? "" : "：" + opinion);
                AddRecord(instance.InstanceId, taskId, task.NodeId, op, (int)WfAction.AddSign, recordOpinion);
            }, "加签失败");
        }

        /// <summary>
        /// 委托代审：原审批人把当前待办委托给他人代审。
        /// 与转办的本质区别——<b>不转移任务归属</b>：AssigneeId（原审批人）保持不变，仅写入 DelegateId/DelegateName 记录实际代审人；
        /// 代审人可凭 DelegateId 在待办看到并代为通过/驳回，操作记录标注"代 X 审批"。
        /// 已委托（DelegateId 已有值）则拒绝重复委托；不能委托给自己或无效用户。
        /// </summary>
        public void Delegate(long taskId, long targetUserId, string opinion, long operatorId)
        {
            if (targetUserId <= 0) throw new CustomException("请选择代审人");
            if (targetUserId == operatorId) throw new CustomException("不能委托给自己");

            var (task, instance) = LoadPendingTaskAndInstance(taskId, operatorId);
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("流程状态异常，无法委托");
            if (task.DelegateId != null)
                throw new CustomException("该任务已委托他人代审，请勿重复委托");

            var op = LoadUser(operatorId);
            logger.Info($"委托代审：InstanceId={instance.InstanceId} TaskId={taskId} Node={task.NodeName}({task.NodeId}) {op.NickName}({operatorId}) → 代审人({targetUserId})");
            var target = Context.Queryable<SysUser>().First(u => u.UserId == targetUserId)
                ?? throw new CustomException("代审人不存在");

            RunInTx(() =>
            {
                var now = DateTime.Now;
                Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask
                    {
                        // 注意：AssigneeId / Assignee / AssigneeNickName 均保持不变，任务仍归属原审批人
                        DelegateId = targetUserId,
                        DelegateName = target.NickName,
                        Opinion = opinion,
                        Update_time = now,
                        Update_by = op.UserName
                    })
                    .Where(t => t.TaskId == taskId).ExecuteCommand();

                var recordOpinion = "委托 " + target.NickName + " 代审" + (string.IsNullOrEmpty(opinion) ? "" : "：" + opinion);
                AddRecord(instance.InstanceId, taskId, task.NodeId, op, (int)WfAction.Delegate, recordOpinion);

                Notify(targetUserId, $"【审批委托】{instance.Title} 由 {op.NickName} 委托您代审（任务仍归属 {op.NickName}）。");
            }, "委托失败");
        }

        /// <summary>
        /// 减签：移除本节点某审批人（将其待办置 Skipped 并重新判定节点完成）。
        /// 操作人必须是该节点某一审批任务的审批人（含已处理），被减签目标须为该节点处于 Pending/Waiting 的任务。
        /// 减签后：若节点满足完成条件则按原流转推进；若依次审批(Sequential)下当前处理人被减掉，则自动激活下一位 Waiting。
        /// </summary>
        /// <param name="taskId">操作人自己的任务 ID（用于鉴权该节点）</param>
        /// <param name="targetUserId">被减签的审批人 userId</param>
        /// <param name="opinion">减签意见（可选）</param>
        /// <param name="operatorId">操作人 userId</param>
        public void RemoveSign(long taskId, long targetUserId, string opinion, long operatorId)
        {
            var op = LoadUser(operatorId);
            var operatorTask = Context.Queryable<WfFlowTask>().First(t => t.TaskId == taskId)
                ?? throw new CustomException("审批任务不存在");

            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == operatorTask.InstanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("流程状态异常，无法减签");

            var nodeId = operatorTask.NodeId;
            // 操作人必须是该节点某一任务的审批人（含已处理），否则无减签权限
            var nodeTasks = Context.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == instance.InstanceId && t.NodeId == nodeId)
                .ToList();
            if (!nodeTasks.Any(t => t.AssigneeId == operatorId))
                throw new CustomException("无减签权限");

            var target = nodeTasks
                .FirstOrDefault(t => t.AssigneeId == targetUserId && (t.Status == (int)WfTaskStatus.Pending || t.Status == (int)WfTaskStatus.Waiting))
                ?? throw new CustomException("被减签人不是该节点待审批人");

            var node = Context.Queryable<WfFlowNode>().First(n => n.NodeId == nodeId)
                ?? throw new CustomException("流程节点不存在");
            var targetName = target.Assignee;

            RunInTx(() =>
            {
                target.Status = (int)WfTaskStatus.Skipped;
                target.Update_by = op.UserName;
                target.Update_time = DateTime.Now;
                Context.Updateable(target).ExecuteCommand();

                var recordOpinion = "减签：" + targetName + (string.IsNullOrEmpty(opinion) ? "" : "：" + opinion);
                AddRecord(instance.InstanceId, taskId, nodeId, op, (int)WfAction.RemoveSign, recordOpinion);

                // 减签后重新判定节点完成 / 依次审批推进
                ReevaluateNodeAfterRemove(instance, node, operatorTask);
            }, "减签失败");
        }

        /// <summary>
        /// 减签后对该节点重新评估：
        /// 1) 若节点已完成（或签任一 Done / 会签全 Done / 剩余无人）→ 推进到下一节点；
        /// 2) 若依次审批(Sequential)且当前无 Pending 但有 Waiting → 激活首位 Waiting；
        /// 3) 否则保持原状（仍有人待审批）。
        /// </summary>
        private void ReevaluateNodeAfterRemove(WfFlowInstance instance, WfFlowNode node, WfFlowTask callerTask)
        {
            var tasks = Context.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == instance.InstanceId && t.NodeId == node.NodeId)
                .OrderBy(t => t.TaskId)
                .ToList();
            var pending = tasks.Where(t => t.Status == (int)WfTaskStatus.Pending).ToList();
            var waiting = tasks.Where(t => t.Status == (int)WfTaskStatus.Waiting).ToList();
            var done = tasks.Where(t => t.Status == (int)WfTaskStatus.Done).ToList();

            // 或签/会签完成判定：或签任一 Done；会签需全部 Done（无剩余 Pending/Waiting）
            bool complete;
            if (node.SignType == (int)WfSignType.And)
                complete = tasks.All(t => t.Status == (int)WfTaskStatus.Done || t.Status == (int)WfTaskStatus.Skipped);
            else
                complete = done.Count > 0 || (pending.Count == 0 && waiting.Count == 0);

            if (complete)
            {
                var allNodes = LoadOrderedNodes(instance.FlowId);
                var linksBySource = LoadNodeLinks(instance.FlowId);
                var linksByTarget = LoadNodeLinksByTarget(instance.FlowId);
                var formValues = ParseFormValues(instance);
                AdvanceToNext(instance, node, allNodes, linksBySource, linksByTarget, formValues);
                return;
            }

            // 依次审批：当前无 Pending 但有 Waiting → 激活首位
            if (node.SignType == (int)WfSignType.Sequential && pending.Count == 0 && waiting.Count > 0)
            {
                var next = waiting.First();
                next.Status = (int)WfTaskStatus.Pending;
                next.Update_by = callerTask.Update_by;
                next.Update_time = DateTime.Now;
                Context.Updateable(next).ExecuteCommand();
                Notify(next.AssigneeId.Value, $"【待审批】{instance.Title}（{node.NodeName}）");
            }
        }

        #endregion

        #region 私有辅助

        /// <summary>
        /// <see cref="BaseService{T}.UseTran(Action)"/> + 失败包装的统一入口。
        /// 事务回滚或异常时抛出带 <paramref name="errorLabel"/> 的 CustomException，
        /// 原 errorMessage 透传便于排障。所有公共入口均通过此方法走事务。
        /// </summary>
        private void RunInTx(Action action, string errorLabel)
        {
            var result = UseTran(action);
            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, errorLabel, result.ErrorMessage);
        }

        /// <summary>
        /// 加载待办任务 + 关联实例。统一做"任务存在 / 状态待办 / 审批人匹配"三项校验，
        /// instance 存在性校验一并处理；instance 业务状态校验（!=Approval）由调用方按场景 message 决定
        /// （如 Approve 用"无法审批"、Transfer 用"无法转办"、AddSign 用"无法加签"；Reject 不校验）。
        ///
        /// 审批权限按 <c>AssigneeId</c>（userId）比对：userName 可被改名，用它鉴权会在改名后误判无权限。
        /// 委托代审场景下，<c>DelegateId</c> 命中操作者亦视为有权（任务仍归属原审批人，代审人代为操作）。
        /// </summary>
        private (WfFlowTask task, WfFlowInstance instance) LoadPendingTaskAndInstance(long taskId, long operatorId)
        {
            var task = Context.Queryable<WfFlowTask>().First(t => t.TaskId == taskId)
                ?? throw new CustomException("审批任务不存在");
            if (task.Status != (int)WfTaskStatus.Pending)
                throw new CustomException("该任务已处理");
            if (task.AssigneeId != operatorId && task.DelegateId != operatorId)
                throw new CustomException("无审批权限");

            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == task.InstanceId)
                ?? throw new CustomException("流程实例不存在");
            return (task, instance);
        }

        /// <summary>
        /// 判断当前操作者是否为"代审人"（任务被委托给该用户，任务本身归属原审批人）。
        /// 代审场景下审批记录需标注"代 X 审批"，且操作人以代审人身份落痕。
        /// </summary>
        private bool IsDelegatedOperator(WfFlowTask task, long operatorId)
            => task.DelegateId == operatorId && task.AssigneeId != operatorId;

        /// <summary>
        /// 按 userId 取操作人（登录名 + 昵称快照）。公共入口只收 userId，展示用名称在此一次性取出，
        /// 后续落 <c>Update_by</c> / 记录快照 / 通知文案直接复用，避免各处重复查库。
        /// 用户不存在时抛业务异常（Token 有效但用户已被删除的边界）。
        /// </summary>
        private ResolvedApprover LoadUser(long userId)
        {
            var u = Context.Queryable<SysUser>().First(x => x.UserId == userId)
                ?? throw new CustomException("操作用户不存在");
            return new ResolvedApprover(u.UserId, u.UserName, u.NickName);
        }

        /// <summary>
        /// 加载"可发起"流程定义：必须已发布、启用、未删除、非草稿。
        /// </summary>
        private WfFlowDefinition LoadActivatableDefinition(long flowId)
        {
            var def = Context.Queryable<WfFlowDefinition>().First(d => d.FlowId == flowId);
            if (def == null) throw new CustomException("流程定义不存在");
            if (def.IsDraft == 1) throw new CustomException("该流程版本为草稿态，暂不可发起，请先发布");
            if (def.Status != 1) throw new CustomException("该流程版本已停用，暂不可发起");
            if (def.IsDelete == 1) throw new CustomException("该流程定义已删除，不可发起");
            return def;
        }

        /// <summary>
        /// 按 NodeOrder 升序返回流程的全部节点。Start / Resubmit / Approve 共用。
        /// </summary>
        private List<WfFlowNode> LoadOrderedNodes(long flowId)
        {
            return Context.Queryable<WfFlowNode>()
                .Where(n => n.FlowId == flowId)
                .OrderBy(n => n.NodeOrder)
                .ToList();
        }

        /// <summary>
        /// 判断节点是否属于"流程节点"（审计/抄送），用于首节点查找与 <see cref="GetNextAuditNode"/>。
        /// 静态方法便于在 LINQ 表达式树外复用。
        /// </summary>
        private static bool IsAuditableNode(int nodeType) =>
            nodeType == (int)WfNodeType.Audit || nodeType == (int)WfNodeType.Cc;

        /// <summary>
        /// 取某 FlowId 的全部节点连线，按 SourceNodeId 分组（便于 O(1) 取某节点的出边集合）。
        /// 无连线返回空字典。连线的存在与否决定引擎是否走"图分支"通道。
        /// </summary>
        private Dictionary<long, List<WfNodeLink>> LoadNodeLinks(long flowId)
        {
            var links = Context.Queryable<WfNodeLink>()
                .Where(l => l.FlowId == flowId)
                .OrderBy(l => l.Sort)
                .ToList();
            return links
                .GroupBy(l => l.SourceNodeId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// 按 TargetNodeId 分组的节点入边表（汇聚网关判定用：某汇聚节点的所有入边源是否都完成）。
        /// </summary>
        private Dictionary<long, List<WfNodeLink>> LoadNodeLinksByTarget(long flowId)
        {
            var links = Context.Queryable<WfNodeLink>()
                .Where(l => l.FlowId == flowId)
                .OrderBy(l => l.Sort)
                .ToList();
            return links
                .GroupBy(l => l.TargetNodeId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// Start / Resubmit 共同的 pre-flight：取定义、校验、查节点全集、取首节点、加载节点连线。
        /// 调用方在事务体内完成各自的 Insert / Update 持久化差异。
        /// </summary>
        private (WfFlowDefinition def, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<long, List<WfNodeLink>> linksByTarget, WfFlowNode firstNode) PrepareStartFlow(WfFlowInstance instance)
        {
            var def = LoadActivatableDefinition(instance.FlowId);
            if (string.IsNullOrEmpty(instance.FlowName)) instance.FlowName = def.FlowName;
            var allNodes = LoadOrderedNodes(instance.FlowId);
            var linksBySource = LoadNodeLinks(instance.FlowId);
            var linksByTarget = LoadNodeLinksByTarget(instance.FlowId);
            // 首节点须包含条件网关（NodeType=4）与并行分叉网关（NodeType=7）：网关可作为流程的第一个节点（发起后立即分流/分叉）。
            // 若这里沿用 IsAuditableNode（只认 Audit/Cc），会直接跳过网关落到 NodeOrder 上的第一个审批节点，
            // 导致分支条件从未被评估、始终走"第一条分支"。ArriveNode 内部会对 Condition/ParallelFork 做透传处理。
            var firstNode = allNodes.FirstOrDefault(n => IsAuditableNode(n.NodeType) || n.NodeType == (int)WfNodeType.Condition || n.NodeType == (int)WfNodeType.ParallelFork);
            return (def, allNodes, linksBySource, linksByTarget, firstNode);
        }

        /// <summary>
        /// 流程走到终点：置实例为「通过」并清空当前节点指针。
        /// 所有"下一节点为空"的分支统一走此方法，避免 CurrentNodeId 残留指向最后一个已完成节点
        /// （残留会让详情页/列表在已结束实例上仍显示"当前节点：xxx"）。
        /// </summary>
        private void CompleteInstance(WfFlowInstance instance)
        {
            logger.Info($"流程结束：InstanceId={instance.InstanceId} Title={instance.Title} → 状态=Approved（通过）");
            instance.Status = (int)WfInstanceStatus.Approved;
            instance.CurrentNodeId = null;
            instance.CurrentNodeIds = null;
            Context.Updateable(instance).UpdateColumns(i => new { i.Status, i.CurrentNodeId, i.CurrentNodeIds }).ExecuteCommand();
        }

        /// <summary>
        /// 到达下一节点或结束流程：<paramref name="next"/> 为空则置通过，否则递归 ArriveNode。
        /// 收敛 ArriveNode / AdvanceToNext 中大量重复的 "next == null ? 置通过 : ArriveNode" 模板。
        /// </summary>
        private void ArriveOrComplete(WfFlowInstance instance, WfFlowNode next, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<long, List<WfNodeLink>> linksByTarget, Dictionary<string, string> formValues)
        {
            if (next == null) CompleteInstance(instance);
            else ArriveNode(instance, next, allNodes, linksBySource, linksByTarget, formValues);
        }

        // —— 活动节点集（并行网关节点 7/8 并发时，多个分支同时活动的节点集合）——
        // 存于 instance.CurrentNodeIds（JSON 数组）。单值 CurrentNodeId 同步取集合首个作为兼容字段。

        private static List<long> GetActiveNodeIds(WfFlowInstance instance)
        {
            if (string.IsNullOrWhiteSpace(instance.CurrentNodeIds)) return new List<long>();
            try
            {
                var arr = JsonConvert.DeserializeObject<long[]>(instance.CurrentNodeIds);
                return arr == null ? new List<long>() : arr.ToList();
            }
            catch { return new List<long>(); }
        }

        private static void SetActiveNodeIds(WfFlowInstance instance, List<long> ids)
        {
            var distinct = ids.Distinct().ToList();
            instance.CurrentNodeIds = distinct.Count == 0 ? null : JsonConvert.SerializeObject(distinct);
            instance.CurrentNodeId = distinct.Count > 0 ? distinct.Min() : (long?)null;
        }

        private static void AddActiveNodeId(WfFlowInstance instance, long nodeId)
        {
            var ids = GetActiveNodeIds(instance);
            if (!ids.Contains(nodeId)) ids.Add(nodeId);
            SetActiveNodeIds(instance, ids);
        }

        private static void RemoveActiveNodeId(WfFlowInstance instance, long nodeId)
        {
            var ids = GetActiveNodeIds(instance);
            ids.Remove(nodeId);
            SetActiveNodeIds(instance, ids);
        }

        // 将内存中维护的 CurrentNodeIds / CurrentNodeId 落库（活动集在 ArriveNode/AdvanceToNext 内多次变更后统一回写一次）
        private void SyncActiveNodeId(WfFlowInstance instance)
        {
            Context.Updateable(instance).UpdateColumns(i => new { i.CurrentNodeIds, i.CurrentNodeId }).ExecuteCommand();
        }

        /// <summary>
        /// 将 FormContent(JSON) 解析为 字段-&gt;值 字典（值均为字符串）。解析失败返回空字典。
        /// </summary>
        private Dictionary<string, string> ParseFormValues(WfFlowInstance instance)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(instance.FormContent)) return dict;
            try
            {
                var kv = JsonConvert.DeserializeObject<Dictionary<string, string>>(instance.FormContent);
                if (kv != null)
                    foreach (var k in kv) dict[k.Key] = k.Value;
            }
            catch { /* JSON 解析失败（格式错误或类型不匹配），视为无条件 */ }
            return dict;
        }

        #endregion

        #region 节点事件钩子（Webhook）

        /// <summary>
        /// 触发节点事件 Webhook。钩子失败（网络/超时/非 2xx）仅记日志不阻断流程。
        /// EnterHookUrl 在节点进入时调用；LeaveHookUrl 在节点离开（完成并推进前）时调用。
        /// </summary>
        /// <param name="instance">流程实例（提供 InstanceId/Title/FormContent）</param>
        /// <param name="node">触发节点（提供 NodeId/NodeName/钩子 URL）</param>
        /// <param name="eventType">enter / leave</param>
        /// <param name="formValues">表单字段值（快照进 payload，便于外部系统取值）</param>
        private async void FireNodeHook(WfFlowInstance instance, WfFlowNode node, string eventType, Dictionary<string, string> formValues)
        {
            var url = eventType == "enter" ? node.EnterHookUrl : node.LeaveHookUrl;
            if (string.IsNullOrWhiteSpace(url)) return;

            var payload = new
            {
                eventType,
                instanceId = instance.InstanceId,
                flowId = instance.FlowId,
                flowName = instance.FlowName,
                title = instance.Title,
                businessKey = instance.BusinessKey,
                nodeId = node.NodeId,
                nodeName = node.NodeName,
                nodeType = node.NodeType,
                formContent = instance.FormContent,
                formValues,
                time = DateTime.Now
            };
            try
            {
                var body = JsonConvert.SerializeObject(payload);
                await HttpHelper.HttpPostAsync(url, body, "application/json");
                Log.WriteLine(ConsoleColor.Green, $"[节点钩子:{eventType}] 实例{instance.InstanceId} 节点{node.NodeName}({node.NodeId}) 已通知 {url}");
            }
            catch (Exception ex)
            {
                // 钩子失败不阻断流转，仅记录
                Log.WriteLine(ConsoleColor.Red, $"[节点钩子:{eventType}] 实例{instance.InstanceId} 节点{node.NodeName}({node.NodeId}) 通知失败 {url}：{ex.Message}");
            }
        }

        #endregion

        #region 内部流转引擎

        /// <summary>
        /// 到达某节点：按条件排他跳过；并行分组则同时激活组内节点（fork）；
        /// 审批节点生成待办并等待；抄送节点生成抄送记录并继续；结束则通过。
        ///
        /// 流转图：
        /// <code>
        ///             ┌─ 条件不满足 → 递归到下一节点
        ///             │
        ///  ArriveNode ┼─ 并行分组 → fork 组内节点 → 等待 join
        ///             │   （汇聚由 AdvanceToNext 触发）
        ///             │
        ///             ├─ 抄送节点 → CreateCcTask → 递归到下一节点
        ///             │
        ///             └─ 审批节点 → 生成待办并等待
        /// </code>
        /// </summary>
        private void ArriveNode(WfFlowInstance instance, WfFlowNode node, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<long, List<WfNodeLink>> linksByTarget, Dictionary<string, string> formValues)
        {
            // 节点进入事件钩子（Webhook）：进入该节点时异步通知外部系统，失败不阻断流转
            FireNodeHook(instance, node, "enter", formValues);
            logger.Info($"进入节点：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId}) Type={(WfNodeType)node.NodeType}{(node.ParallelGroup > 0 ? $" ParallelGroup={node.ParallelGroup}" : "")}");

            // 条件网关（菱形，NodeType=4）：本身不生成任务，到达后按出边 ConditionJson 选一路继续。
            // 条件在连线（link）上表达，无需节点级 EvalCondition；无条件出边作为默认分支。
            if (node.NodeType == (int)WfNodeType.Condition)
            {
                // 网关自身不是活动审批节点（Start 可能把它当首节点塞进活动集），透传前先移除自己，
                // 否则活动集残留网关 id、CurrentNodeId 被 SetActiveNodeIds 的 Min() 取成网关而非真正活动节点。
                RemoveActiveNodeId(instance, node.NodeId);
                SyncActiveNodeId(instance);
                logger.Info($"条件网关选路：InstanceId={instance.InstanceId} Condition={node.NodeName}({node.NodeId}) 走 ResolveNextNode 选路");
                ArriveOrComplete(instance, ResolveNextNode(node, allNodes, linksBySource, linksByTarget, formValues), allNodes, linksBySource, linksByTarget, formValues);
                return;
            }

            // 并行分叉网关(7)：本身不生成任务，fork 同时激活全部出边目标（多活动分支并发）。
            if (node.NodeType == (int)WfNodeType.ParallelFork)
            {
                RemoveActiveNodeId(instance, node.NodeId);
                var targets = ResolveNextNodes(node, allNodes, linksBySource, formValues);
                if (targets.Count == 0) { CompleteInstance(instance); return; }
                logger.Info($"并行分叉网关：InstanceId={instance.InstanceId} Fork={node.NodeName}({node.NodeId}) → {targets.Count} 条出边");
                foreach (var t in targets) ArriveNode(instance, t, allNodes, linksBySource, linksByTarget, formValues);
                SyncActiveNodeId(instance);
                return;
            }

            // 并行汇聚网关(8)：本身不生成任务，等待所有入边分支均完成才继续（join）。
            if (node.NodeType == (int)WfNodeType.ParallelJoin)
            {
                if (IsJoinComplete(instance, node, allNodes, linksByTarget))
                {
                    logger.Info($"并行汇聚完成：InstanceId={instance.InstanceId} Join={node.NodeName}({node.NodeId}) 全部入边完成 → 继续推进");
                    RemoveActiveNodeId(instance, node.NodeId);
                    var after = ResolveNextNode(node, allNodes, linksBySource, linksByTarget, formValues);
                    ArriveOrComplete(instance, after, allNodes, linksBySource, linksByTarget, formValues);
                }
                else
                {
                    // 仍有分支未完成：汇聚网关保持在活动集等待，不推进
                    logger.Info($"并行汇聚等待：InstanceId={instance.InstanceId} Join={node.NodeName}({node.NodeId}) 仍有入边分支未完成 → 等待");
                    AddActiveNodeId(instance, node.NodeId);
                    SyncActiveNodeId(instance);
                }
                return;
            }

            // 排他跳过：条件不满足则顺延到下一节点（递归）；全部不满足则流程直接通过
            if (!EvalCondition(node, formValues))
            {
                logger.Info($"排他跳过：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId}) 条件不满足 → 顺延下一节点");
                ArriveOrComplete(instance, ResolveNextNode(node, allNodes, linksBySource, linksByTarget, formValues), allNodes, linksBySource, linksByTarget, formValues);
                return;
            }

            // 并行分支：首次到达该分组时，同时激活组内所有满足条件的节点
            if (node.ParallelGroup > 0)
            {
                logger.Info($"并行分组进入：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId}) ParallelGroup={node.ParallelGroup}");
                var groupNodes = allNodes.Where(n => n.ParallelGroup == node.ParallelGroup).ToList();
                var groupNodeIds = groupNodes.Select(g => g.NodeId).ToList();
                var groupActive = Context.Queryable<WfFlowTask>()
                    .Any(t => t.InstanceId == instance.InstanceId && groupNodeIds.Contains(t.NodeId));
                if (!groupActive)
                {
                    // 并行分组 fork：把组内「将活动」的成员（生成待办/抄送的节点）同时加入活动集 CurrentNodeIds，
                    // 使 CurrentNodeId（取活动集 Min）与活动集保持一致；条件不满足的成员不进活动集（视为已完成）。
                    foreach (var g in groupNodes)
                    {
                        if (!EvalCondition(g, formValues)) continue; // 分支条件不满足：不生成待办，视为已完成（包容网关）
                        if (g.NodeType == (int)WfNodeType.Cc)
                        {
                            CreateCcTask(instance, g, formValues);
                            AddActiveNodeId(instance, g.NodeId);
                        }
                        else
                        {
                            var nodeApprovers = ResolveApprovers(g, formValues, instance.ApplyUserId);
                            if (nodeApprovers.Count == 0)
                            {
                                // 并行成员审批人为空：留痕自动跳过，不加入活动集（视为已完成，由 IsNodeComplete 的 !tasks.Any() 判定完成）
                                CreateAutoSkipTask(instance, g, "审批人为空，节点自动跳过");
                            }
                            else
                            {
                                BatchCreateTasks(instance.InstanceId, g.NodeId, g.NodeName, nodeApprovers, (int)WfTaskStatus.Pending, instance.ApplyUser);
                                NotifyUsers(nodeApprovers, $"【审批待办】{instance.Title}（{instance.FlowName}），节点「{g.NodeName}」待您审批。");
                                AddActiveNodeId(instance, g.NodeId);
                            }
                        }
                    }

                    // 分组内无任何待办（条件均不满足 / 全为抄送）：视为已完成，直接汇聚
                    var hasPending = Context.Queryable<WfFlowTask>()
                        .Any(t => t.InstanceId == instance.InstanceId && groupNodeIds.Contains(t.NodeId) && t.Status == (int)WfTaskStatus.Pending);
                    logger.Info($"并行分组 fork：InstanceId={instance.InstanceId} Group={node.ParallelGroup} 组内活跃成员={groupNodes.Count} 是否产生待办={hasPending}");
                    if (!hasPending)
                    {
                        // 组内所有分支条件均不满足：视为完成，直接汇聚到后续节点
                        var after = ResolveNextNode(groupNodes.MaxBy(g => g.NodeOrder)!, allNodes, linksBySource, linksByTarget, formValues);
                        ArriveOrComplete(instance, after, allNodes, linksBySource, linksByTarget, formValues);
                    }
                    else
                    {
                        // 有分支待审：把活动集（并行分组全部活跃成员）落库，等待组内审批完成（由 Approve 的并行 join 汇聚推进）
                        SyncActiveNodeId(instance);
                    }
                    return; // 等待组内审批完成（由 Approve 的并行 join 汇聚推进）
                }
                // 分组已激活：fork 已覆盖全部成员，避免重复生成
                return;
            }

            // —— 非并行节点 ——
            if (node.NodeType == (int)WfNodeType.Cc)
            {
                CreateCcTask(instance, node, formValues);
                ArriveOrComplete(instance, ResolveNextNode(node, allNodes, linksBySource, linksByTarget, formValues), allNodes, linksBySource, linksByTarget, formValues);
                return;
            }

            // 审批节点
            instance.CurrentNodeId = node.NodeId;
            Context.Updateable(instance).UpdateColumns(i => new { i.CurrentNodeId }).ExecuteCommand();

            var approvers = ResolveApprovers(node, formValues, instance.ApplyUserId);
            if (approvers.Count == 0)
            {
                // 审批人为空：按节点配置的兜底策略处理，避免流程卡死在无待办的节点上。
                var emptyStrategy = (WfEmptyApproverStrategy)node.EmptyApproverStrategy;
                if (emptyStrategy == WfEmptyApproverStrategy.DefaultUser && node.DefaultApproverId.HasValue && node.DefaultApproverId.Value > 0)
                {
                    approvers = ResolveByUserIds(new List<long> { node.DefaultApproverId.Value });
                }
                if (approvers.Count == 0)
                {
                    // 自动通过（默认策略 / 未配置默认审批人 / 默认审批人解析失败）：生成一条 Skipped 留痕任务并立即推进。
                    // 关键：自动跳过等价于「节点完成并推进」，需先从活动集移除当前节点（否则会残留高亮，
                    // 且下一节点加入后活动集变成 [当前, 下一] 两个，导致当前节点与下一节点同时处于活动态）。
                    logger.Info($"审批人自动跳过：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId}) 审批人为空 → 自动通过");
                    CreateAutoSkipTask(instance, node, "审批人为空，节点自动跳过");
                    RemoveActiveNodeId(instance, node.NodeId);
                    SyncActiveNodeId(instance);
                    ArriveOrComplete(instance, ResolveNextNode(node, allNodes, linksBySource, linksByTarget, formValues), allNodes, linksBySource, linksByTarget, formValues);
                    return;
                }
                // 兜底默认审批人生效：继续走下方正常待办生成逻辑（approvers 已被替换为默认审批人）
                logger.Info($"审批人为空 → 使用兜底默认审批人：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId}) DefaultApproverId={node.DefaultApproverId}");
            }
            AddActiveNodeId(instance, node.NodeId);
            // 审批节点：生成审批人待办（或签/会签同时激活；依次审批仅首位 Pending，其余 Waiting）
            var sequential = node.SignType == (int)WfSignType.Sequential;
            logger.Info($"生成审批待办：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId}) 审批人={approvers.Count} 签类型={(WfSignType)node.SignType}{(sequential ? " 依次审批" : "")}");
            BatchCreateTasks(instance.InstanceId, node.NodeId, node.NodeName, approvers, (int)WfTaskStatus.Pending, instance.ApplyUser, sequential: sequential);
            // 通知：或签/会签通知全部；依次审批仅通知当前首位（其余 Waiting 待轮到再通知）
            var notifyList = sequential ? approvers.Take(1).ToList() : approvers;
            NotifyUsers(notifyList, $"【审批待办】{instance.Title}（{instance.FlowName}），节点「{node.NodeName}」待您审批。");
            SyncActiveNodeId(instance);
        }

        /// <summary>
        /// 节点完成后推进：并行分组内需整组完成才汇聚到后续节点；并行分叉(7)的下游各自独立推进、
        /// 汇聚网关(8)需等所有入边分支完成才继续；否则取下一节点。
        ///
        /// 流转图：
        /// <code>
        ///   AdvanceToNext
        ///        │
        ///        ├─ 并行分组未全部完成 → 等待
        ///        │
        ///        ├─ 出边目标是汇聚网关(8)且未全部完成 → 8 入活动集等待
        ///        │
        ///        ├─ 下一节点为空 → 置通过
        ///        │
        ///        └─ 存在下一节点（含多目标 fork）→ ArriveNode(next)
        /// </code>
        /// </summary>
        private void AdvanceToNext(WfFlowInstance instance, WfFlowNode completedNode, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<long, List<WfNodeLink>> linksByTarget, Dictionary<string, string> formValues)
        {
            logger.Info($"节点完成推进：InstanceId={instance.InstanceId} CompletedNode={completedNode.NodeName}({completedNode.NodeId})");
            RemoveActiveNodeId(instance, completedNode.NodeId);

            // 节点离开事件钩子（Webhook）：节点完成并推进前异步通知外部系统，失败不阻断流转
            FireNodeHook(instance, completedNode, "leave", formValues);

            if (completedNode.ParallelGroup > 0)
            {
                var groupNodes = allNodes.Where(n => n.ParallelGroup == completedNode.ParallelGroup).ToList();
                var groupDone = groupNodes.All(g => IsNodeComplete(instance.InstanceId, g));
                if (!groupDone) { logger.Info($"并行分组汇聚等待：InstanceId={instance.InstanceId} Group={completedNode.ParallelGroup} 仍有分支未完成 → 继续等待"); SyncActiveNodeId(instance); return; } // 等待组内其余分支
                var lastInGroup = groupNodes.MaxBy(g => g.NodeOrder);
                var after = lastInGroup == null ? null : ResolveNextNode(lastInGroup, allNodes, linksBySource, linksByTarget, formValues);
                ArriveOrComplete(instance, after, allNodes, linksBySource, linksByTarget, formValues);
                return;
            }

            // 取当前节点全部出边目标（并行分叉可能多目标，普通节点单目标）
            var nexts = ResolveNextNodes(completedNode, allNodes, linksBySource, formValues);
            if (nexts.Count == 0) { CompleteInstance(instance); return; }
            foreach (var next in nexts)
            {
                // 出边目标是汇聚网关(8)：仅当所有入边分支均完成时，才激活 8 的后续；否则 8 入活动集等待
                if (next.NodeType == (int)WfNodeType.ParallelJoin)
                {
                    if (IsJoinComplete(instance, next, allNodes, linksByTarget))
                    {
                        RemoveActiveNodeId(instance, next.NodeId);
                        var after = ResolveNextNode(next, allNodes, linksBySource, linksByTarget, formValues);
                        ArriveOrComplete(instance, after, allNodes, linksBySource, linksByTarget, formValues);
                    }
                    else
                    {
                        logger.Info($"汇聚网关等待：InstanceId={instance.InstanceId} Join={next.NodeName}({next.NodeId}) 仍有入边分支未完成 → 保持活动集等待");
                        AddActiveNodeId(instance, next.NodeId);
                    }
                }
                else
                {
                    ArriveNode(instance, next, allNodes, linksBySource, linksByTarget, formValues);
                }
            }
            SyncActiveNodeId(instance);
        }

        /// <summary>
        /// 解析当前节点的所有下一节点（多目标，供并行分叉 fork / 普通节点发散用）。
        /// 按连线 + ConditionJson 选边：条件命中走该边，无任一命中且有默认分支[ConditionJson 为空]走默认分支，仍无则空。
        /// 与 <see cref="ResolveNextNode"/> 的单目标选取口径一致，只是返回全部可达目标。
        /// </summary>
        private List<WfFlowNode> ResolveNextNodes(WfFlowNode current, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<string, string> formValues)
        {
            var result = new List<WfFlowNode>();
            if (!linksBySource.TryGetValue(current.NodeId, out var outLinks) || outLinks.Count == 0)
            {
                // 无出边：若流程完全无 link（存量老数据）才 fallback 到 NodeOrder 取下一审批/抄送节点
                if (linksBySource.Count == 0)
                {
                    var fb = GetNextAuditNode(allNodes, current.NodeOrder);
                    if (fb != null) result.Add(fb);
                }
                return result;
            }
            foreach (var link in outLinks) // 已按 Sort 升序
            {
                if (!string.IsNullOrWhiteSpace(link.ConditionJson) && !EvalLinkCondition(link.ConditionJson, formValues)) continue;
                var hit = allNodes.FirstOrDefault(n => n.NodeId == link.TargetNodeId);
                if (hit != null && !result.Contains(hit)) result.Add(hit);
            }
            return result;
        }

        /// <summary>
        /// 判断汇聚网关(8)是否已满足 join 条件：所有入边源节点（linksByTarget 中 SourceNodeId）均已"完成"。
        /// 任一入边源尚未完成 → 返回 false（继续等待）。
        /// </summary>
        private bool IsJoinComplete(WfFlowInstance instance, WfFlowNode joinNode, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksByTarget)
        {
            if (!linksByTarget.TryGetValue(joinNode.NodeId, out var inLinks) || inLinks.Count == 0) return true;
            foreach (var l in inLinks)
            {
                var src = allNodes.FirstOrDefault(n => n.NodeId == l.SourceNodeId);
                // 入边源节点完成判定：源节点若为网关(7/8/4)则视为瞬时完成（它们不生成任务，由流转自然跳过），
                // 实际并行分支的"完成"体现在分支末端的审批/抄送节点；这里只校验真实业务节点（审批/抄送）的完成。
                if (src != null && (src.NodeType == (int)WfNodeType.Audit || src.NodeType == (int)WfNodeType.Cc))
                {
                    if (!IsNodeComplete(instance.InstanceId, src)) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 解析当前节点的下一节点（单目标，原有串行语义）：取 <see cref="ResolveNextNodes"/> 的首个可达目标。
        /// **link 为唯一串联事实，NodeOrder 仅作展示排序 / 存量数据兜底**。
        /// 前端为每条边（含直线）生成一条 WfNodeLink（直线 ConditionJson 留空），
        /// 分支终点 / 末节点则**不生成出边**——即"无出边 = 流程终点"，与 ValidateLinks 的口径一致。
        /// - 当前节点存在出边：按连线 + ConditionJson 选边（条件命中走该边，无任一边命中且有默认分支则走默认分支；仍无则流程结束）。
        /// - 当前节点无出边且流程存在 link 数据：此节点就是终点 → 返回 null（流程结束）。
        ///   ⚠️ 此处**绝不能** fallback 到 NodeOrder（条件分支叶子节点天然无出边，顺延会错误流入另一分支）。
        /// - 整个流程**完全没有 link**（存量老数据）：才 fallback 到 NodeOrder 串联，避免老实例卡死。
        /// </summary>
        private WfFlowNode ResolveNextNode(WfFlowNode current, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<long, List<WfNodeLink>> linksByTarget, Dictionary<string, string> formValues)
        {
            var nexts = ResolveNextNodes(current, allNodes, linksBySource, formValues);
            return nexts.Count > 0 ? nexts[0] : null;
        }

        /// <summary>
        /// 取下一审批/抄送节点（跳过开始/结束），NodeOrder fallback 通道使用。
        /// </summary>
        private WfFlowNode GetNextAuditNode(List<WfFlowNode> allNodes, int currentOrder)
        {
            return allNodes
                .Where(n => n.NodeOrder > currentOrder && IsAuditableNode(n.NodeType))
                .OrderBy(n => n.NodeOrder)
                .FirstOrDefault();
        }

        /// <summary>
        /// 评估连线条件（ConditionJson）。空 JSON 视为无条件（默认分支），由 <see cref="ResolveNextNode"/> 上层分流，
        /// 不进入本方法。
        ///
        /// 解析失败 / 字段缺失视为条件不满足（保守，避免误走分支）。
        /// 复用 <see cref="CompareValue"/> 的比较语义，仅数据源来自连线 JSON。
        /// </summary>
        private bool EvalLinkCondition(string conditionJson, Dictionary<string, string> formValues)
        {
            if (string.IsNullOrWhiteSpace(conditionJson)) return false;
            try
            {
                var cond = JsonConvert.DeserializeObject<WfLinkCondition>(conditionJson);
                if (cond == null
                    || string.IsNullOrWhiteSpace(cond.Field)
                    || !cond.Op.HasValue
                    || string.IsNullOrWhiteSpace(cond.Value)
                    || !formValues.TryGetValue(cond.Field, out var raw)
                    || string.IsNullOrWhiteSpace(raw))
                {
                    return false;
                }
                return CompareValue((WfConditionOp)cond.Op.Value, raw, cond.Value);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 判断节点是否完成（或签：任一已审；会签：全部已审）
        /// </summary>
        private bool IsNodeComplete(long instanceId, WfFlowNode node)
        {
            var tasks = Context.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == instanceId && t.NodeId == node.NodeId)
                .ToList();
            if (!tasks.Any()) return true;
            // 抄送节点：任务生成即视为完成（状态 Skipped），无需审批；并行汇聚时依赖此判定
            if (node.NodeType == (int)WfNodeType.Cc)
                return !tasks.Any(t => t.Status == (int)WfTaskStatus.Pending);
            if (node.SignType == (int)WfSignType.And || node.SignType == (int)WfSignType.Sequential)
                // 会签/依次：已减签(Skipped)或被跳过(AutoSkip)的任务不阻塞完成判定，仅校验未跳过的任务是否全部 Done
                return tasks.Where(t => t.Status != (int)WfTaskStatus.Skipped).All(t => t.Status == (int)WfTaskStatus.Done);
            return tasks.Any(t => t.Status == (int)WfTaskStatus.Done);
        }

        /// <summary>
        /// 评估节点条件：字段/运算符/值三者齐全才生效，任一缺失视为无条件（返回 true）。
        /// 字段缺失或无值视为条件不满足（保守跳过）。比较语义委托 <see cref="CompareValue"/>。
        /// </summary>
        private bool EvalCondition(WfFlowNode node, Dictionary<string, string> formValues)
        {
            if (string.IsNullOrWhiteSpace(node.ConditionField)) return true;
            if (node.ConditionOp == (int)WfConditionOp.None) return true;
            if (string.IsNullOrWhiteSpace(node.ConditionValue)) return true;
            if (!formValues.TryGetValue(node.ConditionField, out var raw) || string.IsNullOrWhiteSpace(raw))
                return false;
            return CompareValue((WfConditionOp)node.ConditionOp, raw, node.ConditionValue);
        }

        /// <summary>
        /// 条件比较核心：节点条件 / 连线条件共用。
        /// 两端都能解析为 double 时按数值比较，否则按 OrdinalIgnoreCase 字符串比较。
        /// - Eq/Ne 始终按字符串比较（忽略大小写），避免 "1" vs "1.0" 数值相等的歧义。
        /// - 未知 op 视为 false（连线场景保守）/ true（节点场景：节点条件不严谨时仍放行）。
        /// 调用方应先保证 op 落在 <see cref="WfConditionOp"/> 范围。
        /// </summary>
        private static bool CompareValue(WfConditionOp op, string raw, string target)
        {
            var leftOk = double.TryParse(raw, out var left);
            var rightOk = double.TryParse(target, out var right);
            var bothNum = leftOk && rightOk;
            switch (op)
            {
                case WfConditionOp.Lt: return bothNum ? left < right : string.CompareOrdinal(raw, target) < 0;
                case WfConditionOp.Le: return bothNum ? left <= right : string.CompareOrdinal(raw, target) <= 0;
                case WfConditionOp.Gt: return bothNum ? left > right : string.CompareOrdinal(raw, target) > 0;
                case WfConditionOp.Ge: return bothNum ? left >= right : string.CompareOrdinal(raw, target) >= 0;
                case WfConditionOp.Eq: return string.Equals(raw, target, StringComparison.OrdinalIgnoreCase);
                case WfConditionOp.Ne: return !string.Equals(raw, target, StringComparison.OrdinalIgnoreCase);
                default: return false;
            }
        }

        #endregion

        #region 审批人解析与通知

        /// <summary>
        /// 解析后的审批人（直接用 UserId 落库，不依赖 userName 反查）。
        /// </summary>
        private sealed record ResolvedApprover(long UserId, string UserName, string NickName);

        /// <summary>
        /// 解析节点审批人列表，统一返回 (UserId, UserName, NickName)。
        /// 定义态存的是稳定标识：ApproverType=0/指定用户存 userId；
        /// =1 角色Id；=2 部门Id；=3 表单字段 key，字段值为逗号分隔的 userId；
        /// =4 部门负责人（ApproverId 存部门Id，取部门 LeaderIds）；=5 发起人主管（取流程发起人 LeaderId）。
        /// 所有分支最终都查 SysUser 得到 UserId，运行态任务/记录直接用 UserId，避免 userName 变更失效。
        /// </summary>
        private List<ResolvedApprover> ResolveApprovers(WfFlowNode node, Dictionary<string, string> formValues, long? applyUserId = null)
        {
            var ids = (node.ApproverId ?? "").SplitByComma();

            List<SysUser> users;
            switch (node.ApproverType)
            {
                case (int)WfApproverType.Role:
                    {
                        var roleIds = ids.Select(s => long.TryParse(s, out var v) ? v : (long?)null).Where(v => v.HasValue).Select(v => v.Value).ToList();
                        if (roleIds.Count == 0) return new List<ResolvedApprover>();
                        users = Context.Queryable<SysUser>()
                            .InnerJoin<SysUserRole>((u, ur) => u.UserId == ur.UserId)
                            .Where((u, ur) => roleIds.Contains(ur.RoleId))
                            .Distinct()
                            .ToList();
                        break;
                    }
                case (int)WfApproverType.Dept:
                    {
                        var deptIds = ids.Select(s => long.TryParse(s, out var v) ? v : (long?)null).Where(v => v.HasValue).Select(v => v.Value).ToList();
                        if (deptIds.Count == 0) return new List<ResolvedApprover>();
                        users = Context.Queryable<SysUser>()
                            .Where(u => deptIds.Contains(u.DeptId) && u.Status == 0)
                            .Distinct()
                            .ToList();
                        break;
                    }
                case (int)WfApproverType.Field:
                    {
                        // 表单字段动态审批人：ApproverId 为表单字段 key，字段值为逗号分隔的 userId
                        var key = node.ApproverId ?? "";
                        if (string.IsNullOrWhiteSpace(key) || formValues == null || !formValues.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                            return new List<ResolvedApprover>();
                        var userIds = raw.SplitByComma()
                            .Where(s => long.TryParse(s, out var id) && id > 0)
                            .Select(s => long.Parse(s))
                            .Distinct()
                            .ToList();
                        if (userIds.Count == 0) return new List<ResolvedApprover>();
                        users = Context.Queryable<SysUser>().Where(u => userIds.Contains(u.UserId)).Distinct().ToList();
                        break;
                    }
                case (int)WfApproverType.DeptLeader:
                    {
                        // 部门负责人：ApproverId 存部门Id（逗号分隔），取这些部门 LeaderIds 中的 userId
                        var deptIds = ids.Select(s => long.TryParse(s, out var v) ? v : (long?)null).Where(v => v.HasValue).Select(v => v.Value).ToList();
                        if (deptIds.Count == 0) return new List<ResolvedApprover>();
                        var leaderIdStrs = Context.Queryable<SysDept>()
                            .Where(d => deptIds.Contains(d.DeptId))
                            .Select(d => d.LeaderIds)
                            .ToList()
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .SelectMany(s => s.SplitByComma())
                            .Where(s => long.TryParse(s, out var id) && id > 0)
                            .Select(s => long.Parse(s))
                            .Distinct()
                            .ToList();
                        if (leaderIdStrs.Count == 0) return new List<ResolvedApprover>();
                        users = Context.Queryable<SysUser>().Where(u => leaderIdStrs.Contains(u.UserId)).Distinct().ToList();
                        break;
                    }
                case (int)WfApproverType.ApplyLeader:
                    {
                        // 发起人主管：取流程发起人的 LeaderId（再查一次得到主管用户）
                        if (applyUserId == null || applyUserId <= 0) return new List<ResolvedApprover>();
                        var leaderId = Context.Queryable<SysUser>()
                            .Where(u => u.UserId == applyUserId)
                            .Select(u => u.LeaderId)
                            .First();
                        if (leaderId == null || leaderId <= 0) return new List<ResolvedApprover>();
                        users = Context.Queryable<SysUser>().Where(u => u.UserId == leaderId).Distinct().ToList();
                        break;
                    }
                default: // 指定用户：ApproverId 存 userId（数字）
                    {
                        var userIds = ids.Where(s => long.TryParse(s, out var id) && id > 0).Select(s => long.Parse(s)).Distinct().ToList();
                        if (userIds.Count == 0) return new List<ResolvedApprover>();
                        users = Context.Queryable<SysUser>().Where(u => userIds.Contains(u.UserId)).Distinct().ToList();
                        break;
                    }
            }
            return users.Select(u => new ResolvedApprover(u.UserId, u.UserName, u.NickName)).ToList();
        }

        /// <summary>
        /// 按 userId 列表解析为 ResolvedApprover（加签等以 userId 传入的场景）。
        /// 不存在的 Id 静默丢弃，由调用方判断结果是否为空。
        /// </summary>
        private List<ResolvedApprover> ResolveByUserIds(List<long> userIds)
        {
            if (userIds == null || userIds.Count == 0) return new List<ResolvedApprover>();
            var ids = userIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0) return new List<ResolvedApprover>();
            return Context.Queryable<SysUser>().Where(u => ids.Contains(u.UserId))
                .Select(u => new ResolvedApprover(u.UserId, u.UserName, u.NickName))
                .ToList();
        }

        /// <summary>
        /// 由实例上的申请人快照（ApplyUserId / ApplyUser / ApplyNickName）构造操作人，
        /// 用于"发起 / 自动跳过"这类以申请人名义落记录的场景，无需再查用户表。
        /// </summary>
        private static ResolvedApprover ApplicantOf(WfFlowInstance instance)
            => new(instance.ApplyUserId ?? 0, instance.ApplyUser, instance.ApplyNickName);

        /// <summary>
        /// 批量创建任务（待办/抄送），替代逐条 ExecuteCommand 以减少数据库往返
        /// </summary>
        private void BatchCreateTasks(long instanceId, long nodeId, string nodeName, List<ResolvedApprover> assignees, int status, string createBy, DateTime? createTime = null, bool sequential = false)
        {
            if (assignees == null || assignees.Count == 0) return;
            var now = createTime ?? DateTime.Now;
            var tasks = assignees.Select((a, idx) => new WfFlowTask
            {
                InstanceId = instanceId,
                NodeId = nodeId,
                NodeName = nodeName,
                Assignee = a.UserName,
                AssigneeId = a.UserId,
                AssigneeNickName = a.NickName,
                // 依次审批：仅首位激活为传入 status，其余置 Waiting 排队，前一人完成才轮到下一位
                Status = (sequential && idx > 0) ? (int)WfTaskStatus.Waiting : status,
                Create_time = now,
                Create_by = createBy
            }).ToList();
            Context.Insertable(tasks).ExecuteCommand();
        }

        /// <summary>
        /// 审批人为空时生成一条 Skipped 留痕任务 + 操作记录（节点自动通过）。
        /// 用于部门未配置负责人 / 发起人无主管 / 指定用户已删除等场景，避免流程卡死在无待办的节点。
        /// 参考业界（钉钉/飞书/Activiti）「审批人为空则节点自动跳过」策略，复用抄送节点的 Skipped 模式。
        /// </summary>
        private void CreateAutoSkipTask(WfFlowInstance instance, WfFlowNode node, string reason)
        {
            // Assignee 列 NOT NULL，自动跳过无具体审批人，用申请人登录名占位（或系统常量兜底）。
            var skipAssignee = string.IsNullOrEmpty(instance.ApplyUser) ? "__SYSTEM__" : instance.ApplyUser;
            Context.Insertable(new WfFlowTask
            {
                InstanceId = instance.InstanceId,
                NodeId = node.NodeId,
                NodeName = node.NodeName,
                Assignee = skipAssignee,
                AssigneeId = instance.ApplyUserId,
                AssigneeNickName = instance.ApplyNickName,
                Status = (int)WfTaskStatus.Skipped,
                TaskType = (int)WfTaskType.Audit,
                Create_time = DateTime.Now,
                Create_by = instance.ApplyUser
            }).ExecuteCommand();
            AddRecord(instance.InstanceId, null, node.NodeId, ApplicantOf(instance), (int)WfAction.AutoSkip, reason);
        }

        /// <summary>
        /// 生成抄送任务并落库抄送记录、推送通知；审批人昵称一并快照。
        /// </summary>
        private void CreateCcTask(WfFlowInstance instance, WfFlowNode node, Dictionary<string, string> formValues)
        {
            var ccList = ResolveApprovers(node, formValues, instance.ApplyUserId);
            logger.Info($"生成抄送：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId}) 抄送人={ccList.Count}");
            var ccUsers = string.Join(",", ccList.Select(c => c.UserName));
            var ccUserIds = string.Join(",", ccList.Select(c => c.UserId));
            var ccNick = string.Join(",", ccList.Select(c => c.NickName));
            Context.Insertable(new WfFlowTask
            {
                InstanceId = instance.InstanceId,
                NodeId = node.NodeId,
                NodeName = node.NodeName,
                Assignee = ccUsers,
                AssigneeId = null,
                AssigneeNickName = ccNick,
                Status = (int)WfTaskStatus.Skipped,
                TaskType = (int)WfTaskType.Cc,
                Create_time = DateTime.Now,
                Create_by = instance.ApplyUser
            }).ExecuteCommand();
            // 每个收件人落一条抄送记录并写入各自的 OperatorId（userId），便于按 userId 精确匹配（抄送给我/数据面板）
            foreach (var c in ccList)
            {
                AddRecord(instance.InstanceId, null, node.NodeId, c, (int)WfAction.Cc, "抄送");
            }
            NotifyUsers(ccList, $"【审批抄送】{instance.Title}（{instance.FlowName}）抄送知会，请知悉。");
        }

        /// <summary>
        /// 统一创建流程操作记录。操作人以 <see cref="ResolvedApprover"/>（userId + 名称快照）传入，
        /// 调用方已持有完整身份，此处不再按登录名反查用户表。
        /// </summary>
        private void AddRecord(long instanceId, long? taskId, long? nodeId, ResolvedApprover op, int action, string opinion, DateTime? createTime = null)
        {
            Context.Insertable(new WfFlowRecord
            {
                InstanceId = instanceId,
                TaskId = taskId,
                NodeId = nodeId,
                Operator = op.UserName,
                OperatorId = op.UserId,
                OperatorNickName = op.NickName,
                Action = action,
                Opinion = opinion,
                Create_time = createTime ?? DateTime.Now,
                Create_by = op.UserName
            }).ExecuteCommand();
        }

        /// <summary>
        /// 站内信通知：落库并 SignalR 实时推送（异常不影响主流程）
        /// </summary>
        private void Notify(long userId, string content)
        {
            try { _msgService.AddSysUserMsg(userId, content, UserMsgType.WORKFLOW); }
            catch { /* 通知失败不影响流程主逻辑 */ }
        }

        /// <summary>
        /// 批量通知一组审批人（直接用 UserId 推送，无需反查用户表）
        /// </summary>
        private void NotifyUsers(List<ResolvedApprover> approvers, string content)
        {
            if (approvers == null) return;
            foreach (var a in approvers.Distinct())
                Notify(a.UserId, content);
        }

        /// <summary>
        /// 按 userId 集合批量通知（如撤回时通知全部待办审批人）。null 元素与重复项自动忽略。
        /// </summary>
        private void NotifyUserIds(IEnumerable<long?> userIds, string content)
        {
            if (userIds == null) return;
            foreach (var id in userIds.Where(i => i.HasValue && i.Value > 0).Select(i => i.Value).Distinct())
                Notify(id, content);
        }

        /// <summary>
        /// 通知单个用户（userId 为空/非法时静默跳过，如存量实例缺 ApplyUserId）。
        /// </summary>
        private void NotifyUser(long? userId, string content)
        {
            if (userId.HasValue && userId.Value > 0) Notify(userId.Value, content);
        }

        #endregion
    }
}
