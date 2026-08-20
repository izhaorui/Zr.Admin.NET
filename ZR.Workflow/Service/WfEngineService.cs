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
        private readonly IWfWebhookService _webhookService;
        private readonly IWfAiService _aiService;

        /// <summary>
        /// 审批人解析策略注册表：WfApproverType → IApproverResolver。
        /// 新增审批人类型时只需「加一个 resolver 类 + 一行注册」，无需再改 switch 分支。
        /// </summary>
        private readonly Dictionary<WfApproverType, IApproverResolver> _approverResolvers;

        public WfEngineService(ISysUserMsgService msgService, IWfWebhookService webhookService, IWfAiService aiService)
        {
            _msgService = msgService;
            _webhookService = webhookService;
            _aiService = aiService;

            _approverResolvers = new Dictionary<WfApproverType, IApproverResolver>
            {
                [WfApproverType.User] = new UserApproverResolver(this),
                [WfApproverType.Role] = new RoleApproverResolver(this),
                [WfApproverType.Dept] = new DeptApproverResolver(this),
                [WfApproverType.Field] = new FormFieldApproverResolver(this),
                [WfApproverType.DeptLeader] = new DeptLeaderApproverResolver(this),
                [WfApproverType.ApplyLeader] = new ApplyLeaderApproverResolver(this),
            };
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
            var (linksBySource, linksByTarget) = LoadNodeLinks(instance.FlowId);
            // 活动集兜底初始化：存量实例可能无 CurrentNodeIds，用 CurrentNodeId 单值补齐，避免并行汇聚判定缺失
            if (string.IsNullOrWhiteSpace(instance.CurrentNodeIds) && instance.CurrentNodeId.HasValue)
            {
                instance.CurrentNodeIds = JsonConvert.SerializeObject(new[] { instance.CurrentNodeId.Value });
            }

            RunInTx(() =>
            {
                var now = DateTime.Now;
                // 并发防重：更新必须命中待办态（Pending），命中 0 行说明该任务已被并发处理（重复点击/或签他人已审），
                // 直接短路，不再进入 IsNodeComplete / AdvanceToNext，避免重复推进后续节点或并行汇聚重复放行。
                var rows = Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask
                    {
                        Status = (int)WfTaskStatus.Done,
                        Action = (int)WfAction.Approve,
                        Opinion = opinion,
                        HandleTime = now,
                        Update_time = now,
                        Update_by = op.UserName
                    })
                    .Where(t => t.TaskId == taskId && t.Status == (int)WfTaskStatus.Pending).ExecuteCommand();
                // 数据库级 CAS 抢占：能否审批由 UPDATE 命中行数决定，命中 0 行说明任务已被并发处理（重复点击/或签他人已审）
                if (rows != 1) throw new CustomException("任务已处理");

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
            var (linksBySource, linksByTarget) = LoadNodeLinks(instance.FlowId);

            RunInTx(() =>
            {
                var now = DateTime.Now;
                // 并发防重：更新须命中待办态，命中 0 行说明任务已被并发处理（重复点击/或签他人已审），直接短路不再推进
                var rows = Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask
                    {
                        Status = (int)WfTaskStatus.Done,
                        Action = (int)WfAction.Reject,
                        Opinion = opinion,
                        HandleTime = now,
                        Update_time = now,
                        Update_by = op.UserName
                    })
                    .Where(t => t.TaskId == taskId && t.Status == (int)WfTaskStatus.Pending).ExecuteCommand();
                // 数据库级 CAS 抢占：能否驳回由 UPDATE 命中行数决定，命中 0 行说明任务已被并发处理
                if (rows != 1) throw new CustomException("任务已处理");

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
                    .UpdateColumns(i => new { i.Status, i.CurrentNodeId, i.CurrentNodeIds, i.FormContent, i.Attachment, i.Title, i.Update_time, i.Update_by })
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
            // 并行场景下活动集 CurrentNodeIds 可能有多个活动节点（并行分叉/分组），
            // 一旦进入并行阶段（活动节点 >1），任一分支可能已被审批、也可能并发推进中，整单撤回会破坏已产生的分支轨迹，
            // 故并行阶段一律不允许撤回；串行（活动节点=1）沿用"当前节点未处理才可撤回"的判定。
            // 放在事务外做预检，使业务校验异常直接抛出（不会被包裹成通用的"撤回失败"）。
            var activeNodeIds = GetActiveNodeIds(instance);
            if (activeNodeIds.Count == 0 && instance.CurrentNodeId.HasValue)
                activeNodeIds.Add(instance.CurrentNodeId.Value); // 存量实例无活动集时兜底用单值
            if (activeNodeIds.Count > 1)
                throw new CustomException("并行审批进行中，无法撤回");
            var currentNodeHandled = Context.Queryable<WfFlowTask>()
                .Any(t => t.InstanceId == instanceId
                         && activeNodeIds.Contains(t.NodeId)
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
            var target = ActiveUsers().First(u => u.UserId == targetUserId)
                ?? throw new CustomException("转办人不存在或已停用");
            var targetName = target.UserName;
            var targetNickName = target.NickName;
            RunInTx(() =>
            {
                var now = DateTime.Now;
                // 并发防重：转办须命中待办态，命中 0 行说明任务已被并发处理（重复转办/或签他人已审），直接短路
                var rows = Context.Updateable<WfFlowTask>()
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
                    .Where(t => t.TaskId == taskId && t.Status == (int)WfTaskStatus.Pending).ExecuteCommand();
                // 数据库级 CAS 抢占：能否转办由 UPDATE 命中行数决定，命中 0 行说明任务已被并发处理
                if (rows != 1) throw new CustomException("任务已处理");

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
            // 事务外快速校验：重复加签给出明确业务提示（并发兜底在事务内 CAS 完成后二次查重）
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
                // 数据库级 CAS 抢占：能否加签由当前待办的原子条件更新命中行数决定。
                // 加签不改任务审批主状态，借对同一条 task 行的 UPDATE 触发行锁，串行化并发加签；
                // 命中 0 行说明任务已被并发处理（已通过/驳回/转办/委托），直接抛"任务已处理"。
                var tokenRows = Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask { Update_time = DateTime.Now, Update_by = op.UserName })
                    .Where(t => t.TaskId == taskId && t.Status == (int)WfTaskStatus.Pending).ExecuteCommand();
                if (tokenRows != 1) throw new CustomException("任务已处理");

                // 二次查重（并发兜底）：借助上方行锁串行化，后到的并发加签能读到前一请求已加的审批人；若有重复则短路
                var freshIds = Context.Queryable<WfFlowTask>()
                    .Where(t => t.InstanceId == task.InstanceId && t.NodeId == task.NodeId && t.AssigneeId != null)
                    .Select(t => t.AssigneeId)
                    .ToList();
                var freshToAdd = toAddApprovers.Where(a => !freshIds.Contains(a.UserId)).ToList();
                if (freshToAdd.Count == 0) throw new CustomException("加签人已在该节点审批人中");

                BatchCreateTasks(task.InstanceId, task.NodeId, task.NodeName, freshToAdd, (int)WfTaskStatus.Pending, op.UserName);

                NotifyUsers(freshToAdd, $"【审批加签】{instance.Title} 由 {op.NickName} 邀请您加签审批。");

                var recordOpinion = "加签：" + string.Join(",", freshToAdd.Select(a => a.NickName)) + (string.IsNullOrEmpty(opinion) ? "" : "：" + opinion);
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
            var target = ActiveUsers().First(u => u.UserId == targetUserId)
                ?? throw new CustomException("代审人不存在或已停用");

            RunInTx(() =>
            {
                var now = DateTime.Now;
                // 并发防重：委托须命中待办态且未被重复委托，命中 0 行说明任务已被并发处理或已委托，直接短路
                var rows = Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask
                    {
                        // 注意：AssigneeId / Assignee / AssigneeNickName 均保持不变，任务仍归属原审批人
                        DelegateId = targetUserId,
                        DelegateName = target.NickName,
                        Opinion = opinion,
                        Update_time = now,
                        Update_by = op.UserName
                    })
                    .Where(t => t.TaskId == taskId && t.Status == (int)WfTaskStatus.Pending && t.DelegateId == null).ExecuteCommand();
                // 数据库级 CAS 抢占：能否委托由 UPDATE 命中行数决定，命中 0 行说明任务已被并发处理或已委托
                if (rows != 1) throw new CustomException("任务已处理");

                var recordOpinion = "委托 " + target.NickName + " 代审" + (string.IsNullOrEmpty(opinion) ? "" : "：" + opinion);
                AddRecord(instance.InstanceId, taskId, task.NodeId, op, (int)WfAction.Delegate, recordOpinion);

                Notify(targetUserId, $"【审批委托】{instance.Title} 由 {op.NickName} 委托您代审（任务仍归属 {op.NickName}）。");
            }, "委托失败");
        }

        /// <summary>
        /// 超时自动处理（由定时任务 Job_WfTimeoutAutoProcess 按租户周期调用）。
        /// 扫描当前租户下 Status=Pending 且 DeadlineTime 已过、所属节点配置了超时动作的待办，
        /// 按节点 TimeoutAction 自动通过 / 自动驳回 / 自动转交，并写审批记录 + 通知。
        /// 复用既有 Approve/Reject/转交的事务体语义（以申请人名义落记录、跳过人工鉴权），
        /// 会签场景复用 IsNodeComplete 判定整组完成才推进，不破坏并行分组逻辑。
        /// </summary>
        public void ProcessTimeoutTasks()
        {
            var now = DateTime.Now;
            var dueTasks = Context.Queryable<WfFlowTask>()
                .Where(t => t.Status == (int)WfTaskStatus.Pending
                            && t.DeadlineTime != null
                            && t.DeadlineTime < now)
                .ToList();
            if (dueTasks.Count == 0) return;

            // 预加载节点配置（含 TimeoutAction / TimeoutTransferUserId），避免逐任务查库
            var nodeIds = dueTasks.Select(t => t.NodeId).Distinct().ToList();
            var nodes = Context.Queryable<WfFlowNode>().Where(n => nodeIds.Contains(n.NodeId)).ToList();
            var nodeMap = nodes.ToDictionary(n => n.NodeId);

            var handled = 0;
            foreach (var task in dueTasks)
            {
                if (!nodeMap.TryGetValue(task.NodeId, out var node)) continue;
                var action = (WfTimeoutAction)node.TimeoutAction;
                if (action == WfTimeoutAction.None) continue; // 未配置超时动作 → 跳过

                var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == task.InstanceId);
                if (instance == null || instance.Status != (int)WfInstanceStatus.Approval) continue;

                try
                {
                    switch (action)
                    {
                        case WfTimeoutAction.AutoApprove:
                            AutoApproveTask(task, instance, node);
                            break;
                        case WfTimeoutAction.AutoReject:
                            AutoRejectTask(task, instance, node);
                            break;
                        case WfTimeoutAction.Transfer:
                            AutoTransferTask(task, instance, node);
                            break;
                    }
                    handled++;
                }
                catch (Exception ex)
                {
                    // 单条失败不影响其余超时任务；记录日志后继续
                    logger.Error(ex, $"超时自动处理失败：InstanceId={task.InstanceId} TaskId={task.TaskId} Node={node.NodeName}({node.NodeId}) Action={action}");
                }
            }
            logger.Info($"超时自动处理完成：扫描 {dueTasks.Count} 条超时待办，成功处理 {handled} 条");
        }

        /// <summary>
        /// 超时自动通过：以申请人名义将待办置为通过并推进（复用 Approve 事务体，跳过人工鉴权与代审标注）。
        /// </summary>
        private void AutoApproveTask(WfFlowTask task, WfFlowInstance instance, WfFlowNode node)
        {
            var allNodes = LoadOrderedNodes(instance.FlowId);
            var (linksBySource, linksByTarget) = LoadNodeLinks(instance.FlowId);
            var op = ApplicantOf(instance); // 超时自动通过以申请人名义落记录
            RunInTx(() =>
            {
                var now = DateTime.Now;
                // 并发防重：超时自动通过须命中待办态，命中 0 行说明任务已被人工/并发处理，直接短路不再推进
                var rows = Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask
                    {
                        Status = (int)WfTaskStatus.Done,
                        Action = (int)WfAction.Approve,
                        Opinion = "超时自动通过",
                        HandleTime = now,
                        Update_time = now,
                        Update_by = op.UserName
                    })
                    .Where(t => t.TaskId == task.TaskId && t.Status == (int)WfTaskStatus.Pending).ExecuteCommand();
                if (rows == 0) return;

                AddRecord(instance.InstanceId, task.TaskId, task.NodeId, op, (int)WfAction.Approve, "超时自动通过");

                // 依次审批：超时通过同样触发下一位 Waiting 轮转
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
                        return;
                    }
                }

                if (!IsNodeComplete(instance.InstanceId, node)) return;

                NotifyUser(instance.ApplyUserId, $"【审批进度】{instance.Title} 的「{node.NodeName}」节点已超时自动通过。");

                Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask { Status = (int)WfTaskStatus.Skipped })
                    .Where(t => t.InstanceId == instance.InstanceId && t.NodeId == node.NodeId && t.Status == (int)WfTaskStatus.Pending)
                    .ExecuteCommand();

                var formValues = ParseFormValues(instance);
                AdvanceToNext(instance, node, allNodes, linksBySource, linksByTarget, formValues);
            }, "超时自动通过失败");
        }

        /// <summary>
        /// 超时自动驳回：以申请人名义将待办置为驳回，按节点驳回策略回退（复用 Reject 事务体）。
        /// </summary>
        private void AutoRejectTask(WfFlowTask task, WfFlowInstance instance, WfFlowNode node)
        {
            var allNodes = LoadOrderedNodes(instance.FlowId);
            var (linksBySource, linksByTarget) = LoadNodeLinks(instance.FlowId);
            var op = ApplicantOf(instance);
            RunInTx(() =>
            {
                var now = DateTime.Now;
                // 并发防重：超时自动驳回须命中待办态，命中 0 行说明任务已被并发处理，直接短路不再推进
                var rows = Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask
                    {
                        Status = (int)WfTaskStatus.Done,
                        Action = (int)WfAction.Reject,
                        Opinion = "超时自动驳回",
                        HandleTime = now,
                        Update_time = now,
                        Update_by = op.UserName
                    })
                    .Where(t => t.TaskId == task.TaskId && t.Status == (int)WfTaskStatus.Pending).ExecuteCommand();
                if (rows == 0) return;

                AddRecord(instance.InstanceId, task.TaskId, task.NodeId, op, (int)WfAction.Reject, "超时自动驳回");

                NotifyUser(instance.ApplyUserId, $"【审批驳回】{instance.Title} 被超时自动驳回（节点「{node.NodeName}」）");

                var strategy = (WfRejectStrategy)node.RejectStrategy;
                WfFlowNode targetNode = null;
                if (strategy == WfRejectStrategy.ToPrevNode)
                {
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
                    instance.Status = (int)WfInstanceStatus.Rejected;
                    Context.Updateable(instance).UpdateColumns(i => new { i.Status }).ExecuteCommand();
                    Context.Updateable<WfFlowTask>()
                        .SetColumns(t => new WfFlowTask { Status = (int)WfTaskStatus.Skipped })
                        .Where(t => t.InstanceId == instance.InstanceId && t.Status == (int)WfTaskStatus.Pending)
                        .ExecuteCommand();
                    return;
                }

                RollbackToNode(instance, targetNode, allNodes, linksBySource, linksByTarget);
            }, "超时自动驳回失败");
        }

        /// <summary>
        /// 超时自动转交：将待办转给节点配置的 TimeoutTransferUserId。
        /// 目标无效（未配置/不存在/即申请人）则退化为自动通过，避免流程卡死。
        /// </summary>
        private void AutoTransferTask(WfFlowTask task, WfFlowInstance instance, WfFlowNode node)
        {
            var targetUserId = node.TimeoutTransferUserId;
            if (!targetUserId.HasValue || targetUserId.Value <= 0 || targetUserId.Value == instance.ApplyUserId)
            {
                logger.Warn($"超时转交目标无效（TimeoutTransferUserId={targetUserId}）→ 退化为自动通过：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId})");
                AutoApproveTask(task, instance, node);
                return;
            }
            var target = ActiveUsers().First(u => u.UserId == targetUserId.Value);
            if (target == null)
            {
                logger.Warn($"超时转交目标用户不存在或已停用（{targetUserId}）→ 退化为自动通过：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId})");
                AutoApproveTask(task, instance, node);
                return;
            }
            var op = ApplicantOf(instance);
            RunInTx(() =>
            {
                var now = DateTime.Now;
                // 并发防重：超时自动转交须命中待办态，命中 0 行说明任务已被并发处理，直接短路
                var rows = Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask
                    {
                        Assignee = target.UserName,
                        AssigneeId = target.UserId,
                        AssigneeNickName = target.NickName,
                        Opinion = "超时自动转交",
                        Action = (int)WfAction.Transfer,
                        Update_time = now,
                        Update_by = op.UserName
                    })
                    .Where(t => t.TaskId == task.TaskId && t.Status == (int)WfTaskStatus.Pending).ExecuteCommand();
                if (rows == 0) return;

                AddRecord(instance.InstanceId, task.TaskId, task.NodeId, op, (int)WfAction.Transfer, "超时自动转交：" + target.NickName);

                Notify(target.UserId, $"【审批转办】{instance.Title} 因节点「{node.NodeName}」超时，自动转办给您处理。");
            }, "超时自动转交失败");
        }

        /// <summary>
        /// 申请人催办：对运行中的实例，向当前活动节点的全部待办审批人发送催办通知。
        /// 24 小时限频：距上次催办不足 24h 则拒绝；通过则更新 LastUrgeTime。
        /// </summary>
        /// <param name="instanceId">流程实例 ID</param>
        /// <param name="operatorId">操作人 userId（必须为实例申请人）</param>
        public void Urge(long instanceId, long operatorId)
        {
            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == instanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.ApplyUserId != operatorId)
                throw new CustomException("仅申请人可催办");
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("当前流程不在审批中，无法催办");

            var now = DateTime.Now;
            if (instance.LastUrgeTime.HasValue && (now - instance.LastUrgeTime.Value).TotalHours < 24)
                throw new CustomException("距上次催办不足 24 小时，请稍后再催办");

            // 当前活动节点的全部待办审批人（含或签/会签/依次审批的 Pending/Waiting）
            var activeNodeIds = GetActiveNodeIds(instance);
            if (activeNodeIds.Count == 0 && instance.CurrentNodeId.HasValue)
                activeNodeIds.Add(instance.CurrentNodeId.Value);
            var assigneeIds = Context.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == instanceId && activeNodeIds.Contains(t.NodeId)
                            && (t.Status == (int)WfTaskStatus.Pending || t.Status == (int)WfTaskStatus.Waiting)
                            && t.AssigneeId != null)
                .Select(t => t.AssigneeId)
                .ToList();

            if (assigneeIds.Count == 0)
                throw new CustomException("当前无审批人可催办");

            var op = LoadUser(operatorId);
            RunInTx(() =>
            {
                instance.LastUrgeTime = now;
                instance.Update_time = now;
                instance.Update_by = op.UserName;
                Context.Updateable(instance).UpdateColumns(i => new { i.LastUrgeTime, i.Update_time, i.Update_by }).ExecuteCommand();

                var nodeNames = Context.Queryable<WfFlowNode>()
                    .Where(n => activeNodeIds.Contains(n.NodeId))
                    .Select(n => n.NodeName)
                    .ToList();
                var nodeDesc = string.Join("、", nodeNames);
                NotifyUserIds(assigneeIds, $"【审批催办】{instance.Title}（{instance.FlowName}）申请人 {op.NickName} 催办：节点「{nodeDesc}」请尽快处理。");
                AddRecord(instanceId, null, null, op, (int)WfAction.Urge, "催办审批人");
            }, "催办失败");
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
                // 数据库级 CAS 抢占：减签须命中目标任务仍处于待审批态（Pending/Waiting），命中 0 行说明已被并发减签/处理，直接短路
                var rows = Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask { Status = (int)WfTaskStatus.Skipped, Update_by = op.UserName, Update_time = DateTime.Now })
                    .Where(t => t.TaskId == target.TaskId && (t.Status == (int)WfTaskStatus.Pending || t.Status == (int)WfTaskStatus.Waiting))
                    .ExecuteCommand();
                if (rows != 1) return;

                var recordOpinion = "减签：" + targetName + (string.IsNullOrEmpty(opinion) ? "" : "：" + opinion);
                AddRecord(instance.InstanceId, taskId, nodeId, op, (int)WfAction.RemoveSign, recordOpinion);

                // 减签后重新判定节点完成 / 依次审批推进
                ReevaluateNodeAfterRemove(instance, node, op.UserName);
            }, "减签失败");
        }

        /// <summary>
        /// 减签后对该节点重新评估：
        /// 1) 若节点已完成（或签任一 Done / 会签全 Done / 剩余无人）→ 推进到下一节点；
        /// 2) 若依次审批(Sequential)且当前无 Pending 但有 Waiting → 激活首位 Waiting；
        /// 3) 否则保持原状（仍有人待审批）。
        /// </summary>
        private void ReevaluateNodeAfterRemove(WfFlowInstance instance, WfFlowNode node, string operatorUserName)
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
                var (linksBySource, linksByTarget) = LoadNodeLinks(instance.FlowId);
                var formValues = ParseFormValues(instance);
                AdvanceToNext(instance, node, allNodes, linksBySource, linksByTarget, formValues);
                return;
            }

            // 依次审批：当前无 Pending 但有 Waiting → 激活首位
            if (node.SignType == (int)WfSignType.Sequential && pending.Count == 0 && waiting.Count > 0)
            {
                var next = waiting.First();
                next.Status = (int)WfTaskStatus.Pending;
                next.Update_by = operatorUserName;
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
        /// 节点 Webhook 改为"Outbox 事务发件箱"：触发时在事务体内写一条 Pending 投递记录
        /// （与业务变更原子落库），由独立定时任务 RetryWebhookDeliveries 统一投递，
        /// 避免"库回滚但外部已收事件"不一致，且支持失败重试 / 死信 / 多实例抢占。
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
        /// 一次性加载某 FlowId 的全部节点连线并分组成：按 SourceNodeId（出边）与按 TargetNodeId（入边）两张表。
        /// 仅查库一次，避免原来 LoadNodeLinks / LoadNodeLinksByTarget 各自全量查询两次。
        /// </summary>
        private (Dictionary<long, List<WfNodeLink>> bySource, Dictionary<long, List<WfNodeLink>> byTarget) LoadNodeLinks(long flowId)
        {
            var links = Context.Queryable<WfNodeLink>()
                .Where(l => l.FlowId == flowId)
                .OrderBy(l => l.Sort)
                .ToList();
            return (
                links.GroupBy(l => l.SourceNodeId).ToDictionary(g => g.Key, g => g.ToList()),
                links.GroupBy(l => l.TargetNodeId).ToDictionary(g => g.Key, g => g.ToList())
            );
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
            var (linksBySource, linksByTarget) = LoadNodeLinks(instance.FlowId);
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
        /// <paramref name="depth"/> 为本次连续到达链的层数，用于递归深度上限保护（防止环导致的无限递归）。
        /// </summary>
        private void ArriveOrComplete(WfFlowInstance instance, WfFlowNode next, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<long, List<WfNodeLink>> linksByTarget, Dictionary<string, string> formValues, int depth = 0)
        {
            if (next == null) CompleteInstance(instance);
            else ArriveNode(instance, next, allNodes, linksBySource, linksByTarget, formValues, depth: depth);
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
        /// 节点进入/离开事件钩子：Outbox 事务发件箱。
        /// 按节点关联的 Webhook 配置（EnterWebhookId / LeaveWebhookId）查询启用的端点，
        /// 在本方法被调用的"业务事务体内"插入一条 Pending 投递记录（含 EventId 幂等键、Payload 快照），
        /// 与流程推进原子落库；投递由独立定时任务 RetryWebhookDeliveries 负责。失败不阻断流转。
        /// </summary>
        /// <param name="instance">流程实例（提供 InstanceId/Title/FormContent）</param>
        /// <param name="node">触发节点（提供 NodeId/NodeName/WebhookId 引用）</param>
        /// <param name="eventType">enter / leave（映射为 node.enter / node.leave）</param>
        /// <param name="formValues">表单字段值（快照进 payload，便于外部系统取值）</param>
        private void QueueNodeHook(WfFlowInstance instance, WfFlowNode node, string eventType, Dictionary<string, string> formValues)
        {
            var webhookId = eventType == "enter" ? node.EnterWebhookId : node.LeaveWebhookId;
            if (webhookId == null || webhookId <= 0) return;

            var cfg = _webhookService.GetFirst(it => it.WebhookId == webhookId && it.Enabled == 1);
            if (cfg == null) return; // 配置不存在或已停用，不投递

            var eventTypeNorm = eventType == "enter" ? "node.enter" : "node.leave";
            var eventId = $"evt_{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
            var payload = new
            {
                eventId,
                eventType = eventTypeNorm,
                webhookId = cfg.WebhookId,
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

            var delivery = new WfWebhookDelivery
            {
                EventId = eventId,
                WebhookId = cfg.WebhookId,
                HookName = cfg.Name,
                HookUrl = cfg.Url,
                InstanceId = instance.InstanceId,
                NodeId = node.NodeId,
                NodeName = node.NodeName,
                EventType = eventTypeNorm,
                Payload = JsonConvert.SerializeObject(payload),
                Status = (int)WfWebhookDeliveryStatus.Pending,
                Processing = 0,
                RetryCount = 0,
                MaxRetry = 5,
                Create_time = DateTime.Now
            };
            Context.Insertable(delivery).ExecuteCommand();
            logger.Info($"[节点钩子:{eventTypeNorm}] 实例{instance.InstanceId} 节点{node.NodeName}({node.NodeId}) 已登记 Outbox 投递 EventId={eventId} Webhook={cfg.Name}");
        }

        /// <summary>
        /// 投递 Outbox 中待发 / 到期可重试的 Webhook 记录（由 Job_WfWebhookRetry 定时调用）。
        /// 多实例安全：用单条原子 UPDATE 抢占（WHERE Status=Pending AND LockUntil 过期），抢到才投递，
        /// 避免多个 Worker 重复投递同一条；Worker 崩溃后 LockUntil 过期可被其它实例重新抢占。
        /// 全程 try/catch，绝不向调用方抛异常，不阻断主流程。
        /// </summary>
        public void RetryWebhookDeliveries()
        {
            var now = DateTime.Now;
            var due = Context.Queryable<WfWebhookDelivery>()
                .Where(it => (it.Status == (int)WfWebhookDeliveryStatus.Pending)
                    && (it.NextRetryTime == null || it.NextRetryTime <= now))
                .OrderBy(it => it.Create_time)
                .Take(200)
                .ToList();

            foreach (var d in due)
            {
                long id = d.DeliveryId;
                // ① 原子抢占：CAS 把 Pending 改为 Processing，并置 LockUntil 防重复
                var claimed = Context.Updateable<WfWebhookDelivery>()
                    .SetColumns(it => new WfWebhookDelivery
                    {
                        Status = (int)WfWebhookDeliveryStatus.Processing,
                        Processing = 1,
                        LockUntil = DateTime.Now.AddSeconds(60)
                    })
                    .Where(it => it.DeliveryId == id
                        && it.Status == (int)WfWebhookDeliveryStatus.Pending
                        && (it.LockUntil == null || it.LockUntil < DateTime.Now))
                    .ExecuteCommand();
                if (claimed <= 0) continue; // 被其它 Worker 抢占 / 已锁定中，跳过

                try
                {
                    // 投递到 Webhook 端点（受保护虚拟方法，便于测试注入成功/失败/计数）
                    SendWebhook(d.HookUrl, d.Payload ?? "{}");
                    // 抢占到 → 投递成功：置 Sent
                    Context.Updateable<WfWebhookDelivery>()
                        .SetColumns(it => new WfWebhookDelivery
                        {
                            Status = (int)WfWebhookDeliveryStatus.Sent,
                            Processing = 0,
                            LockUntil = null,
                            LastAttemptTime = DateTime.Now,
                            LastHttpStatusCode = 200,
                            LastError = null,
                            SentTime = DateTime.Now
                        })
                        .Where(it => it.DeliveryId == id)
                        .ExecuteCommand();
                    logger.Info($"[Webhook投递] EventId={d.EventId} Webhook={d.HookName} 投递成功");
                }
                catch (Exception ex)
                {
                    // 失败：回 Pending，RetryCount++，指数退避算 NextRetryTime，超限 → Dead
                    var newCount = d.RetryCount + 1;
                    int status;
                    DateTime? next = null;
                    if (newCount >= d.MaxRetry)
                    {
                        status = (int)WfWebhookDeliveryStatus.Dead;
                    }
                    else
                    {
                        status = (int)WfWebhookDeliveryStatus.Pending;
                        // 指数退避：2^RetryCount 分钟（1→2m,2→4m,3→8m,4→16m）
                        next = DateTime.Now.AddMinutes(Math.Pow(2, newCount));
                    }
                    Context.Updateable<WfWebhookDelivery>()
                        .SetColumns(it => new WfWebhookDelivery
                        {
                            Status = status,
                            Processing = 0,
                            LockUntil = null,
                            RetryCount = newCount,
                            LastAttemptTime = DateTime.Now,
                            LastHttpStatusCode = null,
                            LastError = ex.Message,
                            NextRetryTime = next
                        })
                        .Where(it => it.DeliveryId == id)
                        .ExecuteCommand();
                    logger.Error($"[Webhook投递] EventId={d.EventId} Webhook={d.HookName} 第{newCount}次失败：{ex.Message} → {(status == (int)WfWebhookDeliveryStatus.Dead ? "Dead" : "Pending")}");
                }
            }
        }

        /// <summary>
        /// 向 Webhook 端点投递 payload（POST JSON）。抽出为受保护虚拟方法，便于单元测试通过子类重写注入成功/失败/计数。
        /// 默认实现走框架 HttpHelper；投递异常直接向上抛，由 RetryWebhookDeliveries 统一处理为退避/死信。
        /// </summary>
        protected virtual void SendWebhook(string url, string body)
        {
            HttpHelper.HttpPostAsync(url, body, "application/json").GetAwaiter().GetResult();
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
        ///
        /// <c>singleNodeOnly</c>：仅管理员跳转（AdminJump）到"并行分组内成员节点"时置 true，
        /// 跳过并行分组的整组 fork，只激活目标节点本身（生成其待办/抄送）；组内其它分支由
        /// AdminJump 先统一置 Skipped，使并行汇聚判定组内其余分支已完成、目标分支通过后即可放行，
        /// 避免卡死与多余分支高亮。
        /// </summary>
        private void ArriveNode(WfFlowInstance instance, WfFlowNode node, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<long, List<WfNodeLink>> linksByTarget, Dictionary<string, string> formValues, bool singleNodeOnly = false, int depth = 0)
        {
            // 递归深度上限保护（最后一道保险）：正常流程节点链式到达深度为几十~上百；
            // 若链路存在环（条件/抄送/空审批人这类"不落地待办即继续递归"的节点成环），ArriveNode 会无限递归 → StackOverflow。
            // 拓扑校验（DetectCycle）已在保存阶段拦截含环流程，此处兜底防存量坏数据 / 绕过校验的异常流程。
            // 上限不能设太高：每层递归都会经 CreateAutoSkipTask → SqlSugar 插入 → 连接栈，栈消耗远超纯托管帧，
            // 实测 1000 层在 SQLite 下会先于上限触发 StackOverflow；取 200 既能覆盖正常几十节点的链式到达，
            // 又在栈溢出之前（约 200×每层栈消耗）抛出可捕获的 CustomException。
            const int maxTransitionCount = 200;
            if (depth > maxTransitionCount)
                throw new CustomException($"流程流转深度超过上限（{maxTransitionCount}），疑似存在连线循环，已终止流转");

            // 节点进入事件钩子（Webhook）：登记入队，事务提交后统一投递，失败不阻断流转
            QueueNodeHook(instance, node, "enter", formValues);
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
                // 路线 α：排他条件网关"不满足出边"的下游分支若直接汇入汇聚网关(8)/并行分组出口，其末端业务节点
                // 因从未被 ArriveNode 而无任务；IsNodeComplete 已规定"无 task = 未激活 = 未完成"，Join 会傻等该节点而卡死。
                // 故对被跳过的每条不满足出边下游链级联建 Skipped 留痕并激活其下游汇聚网关，使"未到达/跳过/已完成"三态收敛为两态
                // （跳过态 Skipped→完成；激活态走正常判定；无 task 只可能出现在"本就不该走到"的分支，Join 不会等待它）。
                // 满足出边仍走正常 ArriveNode 推进；仅当无任何满足/默认出边、且不存在不满足出边时，才视为流程终点完成。
                var satisfiedNext = ResolveNextNode(node, allNodes, linksBySource, linksByTarget, formValues);
                SkipRejectedBranches(instance, node, allNodes, linksBySource, linksByTarget, formValues, depth);
                if (satisfiedNext != null)
                {
                    ArriveOrComplete(instance, satisfiedNext, allNodes, linksBySource, linksByTarget, formValues, depth + 1);
                }
                else
                {
                    // 无满足分支也无默认出边：把"不满足出边"的下游链作为被跳过分支激活（建 Skipped 并推进到汇聚点），避免流程误判完成
                    var fallbacks = linksBySource.TryGetValue(node.NodeId, out var outs) && outs.Count > 0
                        ? outs.Where(l => !string.IsNullOrWhiteSpace(l.ConditionJson) && !EvalLinkCondition(l.ConditionJson, formValues))
                              .Select(l => allNodes.FirstOrDefault(n => n.NodeId == l.TargetNodeId)).Where(n => n != null).ToList()
                        : new List<WfFlowNode>();
                    if (fallbacks.Count > 0) { foreach (var fb in fallbacks) SkipBranchChain(instance, fb, allNodes, linksBySource, linksByTarget, formValues, new HashSet<long>(), depth); }
                    else CompleteInstance(instance);
                }
                return;
            }

            // 结束节点(3)：流程到达终点，不生成任务，直接完成实例。
            if (node.NodeType == (int)WfNodeType.End)
            {
                RemoveActiveNodeId(instance, node.NodeId);
                SyncActiveNodeId(instance);
                CompleteInstance(instance);
                return;
            }

            // 并行分叉网关(7)：本身不生成任务，fork 同时激活全部出边目标（多活动分支并发）。
            if (node.NodeType == (int)WfNodeType.ParallelFork)
            {
                RemoveActiveNodeId(instance, node.NodeId);
                var targets = ResolveNextNodes(node, allNodes, linksBySource, formValues);
                if (targets.Count == 0) { CompleteInstance(instance); return; }
                logger.Info($"并行分叉网关：InstanceId={instance.InstanceId} Fork={node.NodeName}({node.NodeId}) → {targets.Count} 条出边");
                foreach (var t in targets) ArriveNode(instance, t, allNodes, linksBySource, linksByTarget, formValues, depth: depth + 1);
                SyncActiveNodeId(instance);
                return;
            }

            // 并行汇聚网关(8)：本身不生成任务，等待所有入边分支均完成才继续（join）。
            if (node.NodeType == (int)WfNodeType.ParallelJoin)
            {
                var tasksByNode = LoadNodeTasks(instance.InstanceId);
                if (IsJoinComplete(instance, node, allNodes, linksByTarget, tasksByNode))
                {
                    logger.Info($"并行汇聚完成：InstanceId={instance.InstanceId} Join={node.NodeName}({node.NodeId}) 全部入边完成 → 继续推进");
                    RemoveActiveNodeId(instance, node.NodeId);
                    var after = ResolveNextNode(node, allNodes, linksBySource, linksByTarget, formValues);
                    ArriveOrComplete(instance, after, allNodes, linksBySource, linksByTarget, formValues, depth + 1);
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

            // 并行分支：首次到达该分组时，同时激活组内所有满足条件的节点。
            // 但管理员跳转（singleNodeOnly=true）到组内某成员时，跳过整组 fork，落到下方"非并行节点"分支只激活目标节点自身。
            if (node.ParallelGroup > 0 && !singleNodeOnly)
            {
                logger.Info($"并行分组进入：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId}) ParallelGroup={node.ParallelGroup}");
                var groupNodes = allNodes.Where(n => n.ParallelGroup == node.ParallelGroup).ToList();
                var groupNodeIds = groupNodes.Select(g => g.NodeId).ToList();
                // 分组是否"已激活"：仅当组内存在活跃（待审/排队）任务才算已 fork 过。
                // 若组内只剩已跳过/已审的旧任务（如 AdminJump 跳转到并行节点、或 Resubmit 回首并行节点后旧任务残留），
                // 不能视为已激活——否则会走下方 return（"分组已激活，避免重复生成"）而不生成任何新待办，
                // 导致流程停在"审核中"却无可审批任务（卡死）。此时应重新 fork 生成组内全部 Pending 待办。
                var groupActive = Context.Queryable<WfFlowTask>()
                    .Any(t => t.InstanceId == instance.InstanceId && groupNodeIds.Contains(t.NodeId)
                        && (t.Status == (int)WfTaskStatus.Pending || t.Status == (int)WfTaskStatus.Waiting));
                if (!groupActive)
                {
                    // 并行分组 fork：把组内「将活动」的成员（生成待办/抄送的节点）同时加入活动集 CurrentNodeIds，
                    // 使 CurrentNodeId（取活动集 Min）与活动集保持一致；条件不满足的成员不进活动集（视为已完成）。
                    // 成员"是否激活"改由「并行分叉网关(7) → 该成员」的出边 ConditionJson 决定（Edge 属性模型，对标 BPMN）：
                    // 命中 → 激活走正常审批；不满足 → 建 Skipped 留痕。找不到分叉出边或无条件 → 无条件并发激活。
                    var forkNode = allNodes.FirstOrDefault(n => n.NodeType == (int)WfNodeType.ParallelFork && n.ParallelGroup == node.ParallelGroup);
                    foreach (var g in groupNodes)
                    {
                        if (!ShouldActivateForkMember(forkNode, g, allNodes, linksBySource, formValues))
                        {
                            // 分支条件不满足：明确留痕 Skipped（区别于"从未激活"），使 IsNodeComplete 能区分"跳过"与"未走到"；
                            // 不加入活动集（Skipped 视为完成，且避免 CurrentNodeId 取到跳过节点）。
                            CreateAutoSkipTask(instance, g, "分支条件不满足，节点自动跳过");
                            continue;
                        }
                        if (g.NodeType == (int)WfNodeType.Cc)
                        {
                            // 抄送节点瞬时完成（Skipped），其「完成」由 IsNodeComplete 的「无 Pending 任务」判定，
                            // 不加入活动集：否则后续分组汇聚推进时它会残留活动集，导致 CurrentNodeId 取到抄送节点而非真正的下游节点。
                            CreateCcTask(instance, g, formValues);
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
                                BatchCreateTasks(instance.InstanceId, g.NodeId, g.NodeName, nodeApprovers, (int)WfTaskStatus.Pending, instance.ApplyUser, deadlineTime: ComputeDeadline(g, DateTime.Now));
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
                        // 组内所有分支条件均不满足：视为完成，直接汇聚到后续节点（出口按 Link 拓扑解析，不依赖 NodeOrder）
                        var exits = ResolveParallelGroupExit(node.ParallelGroup, allNodes, linksBySource, formValues);
                        if (exits.Count == 0) { CompleteInstance(instance); }
                        else foreach (var exitNode in exits) ArriveNode(instance, exitNode, allNodes, linksBySource, linksByTarget, formValues, depth: depth + 1);
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
                ArriveOrComplete(instance, ResolveNextNode(node, allNodes, linksBySource, linksByTarget, formValues), allNodes, linksBySource, linksByTarget, formValues, depth + 1);
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
                    ArriveOrComplete(instance, ResolveNextNode(node, allNodes, linksBySource, linksByTarget, formValues), allNodes, linksBySource, linksByTarget, formValues, depth + 1);
                    return;
                }
                // 兜底默认审批人生效：继续走下方正常待办生成逻辑（approvers 已被替换为默认审批人）
                logger.Info($"审批人为空 → 使用兜底默认审批人：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId}) DefaultApproverId={node.DefaultApproverId}");
            }
            AddActiveNodeId(instance, node.NodeId);
            // 审批节点：生成审批人待办（或签/会签同时激活；依次审批仅首位 Pending，其余 Waiting）
            var sequential = node.SignType == (int)WfSignType.Sequential;
            logger.Info($"生成审批待办：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId}) 审批人={approvers.Count} 签类型={(WfSignType)node.SignType}{(sequential ? " 依次审批" : "")}");
            BatchCreateTasks(instance.InstanceId, node.NodeId, node.NodeName, approvers, (int)WfTaskStatus.Pending, instance.ApplyUser, sequential: sequential, deadlineTime: ComputeDeadline(node, DateTime.Now));
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
        private void AdvanceToNext(WfFlowInstance instance, WfFlowNode completedNode, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<long, List<WfNodeLink>> linksByTarget, Dictionary<string, string> formValues, int depth = 0)
        {
            // 并发幂等保护：同一节点被多次并发完成（或签多待办同时点通过）时，
            // 先到的事务已把该节点移出活动集并 fork 了后续待办；后到的事务在事务内重新读取活动集，
            // 发现节点已不在，则跳过本次推进，避免重复 fork 子节点待办（并发竞态去重）。
            // 必须在事务内重新查库（而非用外层传入的 instance 内存副本），否则读不到已提交的并发修改。
            var freshActiveIds = GetActiveNodeIds(
                Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == instance.InstanceId));
            if (!freshActiveIds.Contains(completedNode.NodeId))
            {
                logger.Info($"并发去重：InstanceId={instance.InstanceId} CompletedNode={completedNode.NodeName}({completedNode.NodeId}) 已不在活动集 → 跳过重复推进");
                return;
            }

            logger.Info($"节点完成推进：InstanceId={instance.InstanceId} CompletedNode={completedNode.NodeName}({completedNode.NodeId})");
            RemoveActiveNodeId(instance, completedNode.NodeId);

            // 节点离开事件钩子（Webhook）：登记入队，事务提交后统一投递，失败不阻断流转
            QueueNodeHook(instance, completedNode, "leave", formValues);

            // 一次性加载实例全部任务供 join / 分组汇聚批量判定完成，避免逐节点查库（N+1）
            var tasksByNode = LoadNodeTasks(instance.InstanceId);

            if (completedNode.ParallelGroup > 0)
            {
                var groupNodes = allNodes.Where(n => n.ParallelGroup == completedNode.ParallelGroup).ToList();
                var groupDone = groupNodes.All(g => IsNodeComplete(tasksByNode, g));
                if (!groupDone) { logger.Info($"并行分组汇聚等待：InstanceId={instance.InstanceId} Group={completedNode.ParallelGroup} 仍有分支未完成 → 继续等待"); SyncActiveNodeId(instance); return; } // 等待组内其余分支
                // 汇聚出口按 Link 拓扑解析（组内成员连到组外的出边），不依赖 NodeOrder（MaxBy 会破坏 Link 唯一真相）
                var exits = ResolveParallelGroupExit(completedNode.ParallelGroup, allNodes, linksBySource, formValues);
                if (exits.Count == 0) { CompleteInstance(instance); return; }
                // 出口幂等保护：并行分组内若已有成员在 fork 阶段被 Skipped（条件不满足/审批人为空），其 Approve 会再次进入本汇聚分支；
                // 若出口节点已被前次汇聚激活（已有任务），跳过重复 fork，避免出口（如 Join 后续节点 C）生成多条待办。
                // 此处查询位于 Approve 的事务内（RunInTx），可同时防并发竞态重复 fork。
                foreach (var exitNode in exits)
                {
                    var exitActivated = Context.Queryable<WfFlowTask>()
                        .Any(t => t.InstanceId == instance.InstanceId && t.NodeId == exitNode.NodeId);
                    if (exitActivated) { logger.Info($"并行分组出口幂等：InstanceId={instance.InstanceId} Exit={exitNode.NodeName}({exitNode.NodeId}) 已激活 → 跳过重复 fork"); continue; }
                    ArriveNode(instance, exitNode, allNodes, linksBySource, linksByTarget, formValues, depth: depth + 1);
                }
                SyncActiveNodeId(instance);
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
                    if (IsJoinComplete(instance, next, allNodes, linksByTarget, tasksByNode))
                    {
                        RemoveActiveNodeId(instance, next.NodeId);
                        var after = ResolveNextNode(next, allNodes, linksBySource, linksByTarget, formValues);
                        ArriveOrComplete(instance, after, allNodes, linksBySource, linksByTarget, formValues, depth + 1);
                    }
                    else
                    {
                        logger.Info($"汇聚网关等待：InstanceId={instance.InstanceId} Join={next.NodeName}({next.NodeId}) 仍有入边分支未完成 → 保持活动集等待");
                        AddActiveNodeId(instance, next.NodeId);
                    }
                }
                else
                {
                    ArriveNode(instance, next, allNodes, linksBySource, linksByTarget, formValues, depth: depth + 1);
                }
            }
            SyncActiveNodeId(instance);
        }

        #region 管理员运维操作（P0：终止 / 挂起 / 恢复 / 改派 / 跳转）

        /// <summary>
        /// 管理员强制终止 / 作废流程（不可逆）。把所有未完成任务置为 Skipped，实例置 Terminated，
        /// 记一条终止记录并通知申请人/相关人。仅由 Controller 的权限过滤器保证只有管理员可调用。
        /// </summary>
        /// <param name="instanceId">流程实例Id</param>
        /// <param name="operatorId">操作管理员 userId</param>
        /// <param name="opinion">终止原因（可选）</param>
        public async Task AdminTerminate(long instanceId, long operatorId, string opinion)
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
        }

        /// <summary>
        /// 管理员挂起流程（暂停流转，等待恢复）。仅运行中实例可挂起；挂起期间普通审批操作应被前端隐藏，
        /// 本方法仅置状态，不改动任务。恢复请调 <see cref="AdminResume"/>。
        /// </summary>
        public async Task AdminSuspend(long instanceId, long operatorId, string opinion)
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
        }

        /// <summary>
        /// 管理员恢复被挂起的流程。仅 Suspended 态可恢复，恢复后回到 Approval 流转。
        /// </summary>
        public async Task AdminResume(long instanceId, long operatorId, string opinion)
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
        }

        /// <summary>
        /// 管理员改派：把指定节点的全部未完成任务（审批/抄送）重新分配给目标用户。
        /// 适用于审批人离职/失联，管理员需把卡住的待办改给其他人。节点不存在任务时抛异常。
        /// </summary>
        /// <param name="instanceId">流程实例Id</param>
        /// <param name="nodeId">目标节点（实例当前所处或任意未完成任务所属节点）</param>
        /// <param name="targetUserId">改派目标用户 userId</param>
        /// <param name="operatorId">操作管理员 userId</param>
        /// <param name="opinion">改派说明（可选）</param>
        public async Task AdminReassign(long instanceId, long nodeId, long targetUserId, long operatorId, string opinion)
        {
            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == instanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.Status != (int)WfInstanceStatus.Approval && instance.Status != (int)WfInstanceStatus.Suspended)
                throw new CustomException("仅运行中或挂起态的流程可改派");

            var op = LoadUser(operatorId);
            var target = LoadUser(targetUserId);
            var def = LoadActivatableDefinition(instance.FlowId);

            // 业务校验放 RunInTx 之前，确保异常消息能透传给调用方（RunInTx 会用 errorLabel 覆盖内部异常）
            var tasks = Context.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == instanceId && t.NodeId == nodeId
                    && t.Status != (int)WfTaskStatus.Done && t.Status != (int)WfTaskStatus.Skipped)
                .ToList();
            if (tasks.Count == 0)
                throw new CustomException("该节点无可改派的未完成任务");

            RunInTx(() =>
            {
                foreach (var t in tasks)
                {
                    t.AssigneeId = target.UserId;
                    t.Assignee = target.UserName;
                    t.AssigneeNickName = target.NickName;
                    t.DelegateId = null;
                    t.DelegateName = null;
                    t.IsRead = false;
                    Context.Updateable(t).ExecuteCommand();
                    AddRecord(instanceId, t.TaskId, nodeId, op, (int)WfAction.Reassign, $"改派给 {target.NickName}{(string.IsNullOrEmpty(opinion) ? "" : $"：{opinion}")}");
                }
            }, "AdminReassign");

            NotifyUser((long?)target.UserId, $"您有流程【{def.FlowName}】的待办已被管理员{op.NickName}改派给您");
        }

        /// <summary>
        /// 管理员跳转节点：把卡住的实例直接跳到指定节点（重新激活该节点，生成其待办/抄送），
        /// 清空当前活动集与未完成任务。用于流程设计变更后修复在途实例、或绕过异常节点。不可逆。
        /// </summary>
        /// <param name="instanceId">流程实例Id</param>
        /// <param name="targetNodeId">跳转目标节点（必须存在于该流程且非结束节点）</param>
        /// <param name="operatorId">操作管理员 userId</param>
        /// <param name="opinion">跳转说明（可选）</param>
        public async Task AdminJump(long instanceId, long targetNodeId, long operatorId, string opinion)
        {
            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == instanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.Status != (int)WfInstanceStatus.Approval && instance.Status != (int)WfInstanceStatus.Suspended)
                throw new CustomException("仅运行中或挂起态的流程可跳转");

            var op = LoadUser(operatorId);
            var def = LoadActivatableDefinition(instance.FlowId);
            var allNodes = LoadOrderedNodes(instance.FlowId);
            var target = allNodes.FirstOrDefault(n => n.NodeId == targetNodeId)
                ?? throw new CustomException("跳转目标节点不存在");

            var (linksBySource, linksByTarget) = LoadNodeLinks(instance.FlowId);
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
                ArriveNode(instance, target, allNodes, linksBySource, linksByTarget, formValues, singleNodeOnly: true);
                SyncActiveNodeId(instance);
            }, "AdminJump");

            var msg = $"流程【{def.FlowName}】已被管理员{op.NickName}跳转至节点【{target.NodeName}】";
            NotifyUser(instance.ApplyUserId, msg);
        }

        #endregion

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
        /// 解析并行分组（ParallelGroup&gt;0，无显式汇聚网关）整组完成后的汇聚出口。
        /// **link 为唯一串联事实**：出口 = 组内成员指向「组外节点」的出边目标集合，绝不依赖 NodeOrder。
        /// 前端 buildSaveLinks 保证组内成员彼此不连线，只有连到组外目标才构成汇聚出口；
        /// 故此处按拓扑扫描，避免 NodeOrder 与真实连线不符时（手写 FlowJSON / 导入 / 未续号）走错分支。
        /// 兜底：整个流程完全无 link（存量老数据）才退回「组内 NodeOrder 最大成员的出边」，与旧行为一致。
        /// </summary>
        private List<WfFlowNode> ResolveParallelGroupExit(int parallelGroup, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<string, string> formValues)
        {
            var groupNodes = allNodes.Where(n => n.ParallelGroup == parallelGroup).ToList();
            if (groupNodes.Count == 0) return new List<WfFlowNode>();
            var groupIds = groupNodes.Select(g => g.NodeId).ToHashSet();
            var exits = new List<WfFlowNode>();
            foreach (var g in groupNodes)
            {
                if (!linksBySource.TryGetValue(g.NodeId, out var outLinks) || outLinks.Count == 0) continue;
                foreach (var link in outLinks)
                {
                    if (groupIds.Contains(link.TargetNodeId)) continue; // 连到组内兄弟 → 非出口
                    var hit = allNodes.FirstOrDefault(n => n.NodeId == link.TargetNodeId);
                    if (hit != null && !exits.Contains(hit)) exits.Add(hit);
                }
            }
            // 有 link 数据但组成员均未连出：整组即流程终点（无汇聚后续）
            if (linksBySource.Count > 0) return exits;
            // 完全无 link（存量老数据）：退回组内 NodeOrder 最大成员的出边
            var last = groupNodes.MaxBy(g => g.NodeOrder);
            if (last == null) return exits;
            var legacy = ResolveNextNodes(last, allNodes, linksBySource, formValues);
            foreach (var n in legacy) if (!exits.Contains(n)) exits.Add(n);
            return exits;
        }

        /// <summary>
        /// 判断汇聚网关(8)是否已满足 join 条件：所有入边源节点（linksByTarget 中 SourceNodeId）均已"完成"。
        /// 任一入边源尚未完成 → 返回 false（继续等待）。
        /// </summary>
        private bool IsJoinComplete(WfFlowInstance instance, WfFlowNode joinNode, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksByTarget, Dictionary<long, List<WfFlowTask>> tasksByNode)
        {
            if (!linksByTarget.TryGetValue(joinNode.NodeId, out var inLinks) || inLinks.Count == 0) return true;
            foreach (var l in inLinks)
            {
                var src = allNodes.FirstOrDefault(n => n.NodeId == l.SourceNodeId);
                // 入边源节点完成判定：源节点若为网关(7/8/4)则视为瞬时完成（它们不生成任务，由流转自然跳过），
                // 实际并行分支的"完成"体现在分支末端的审批/抄送节点；这里只校验真实业务节点（审批/抄送）的完成。
                if (src != null && (src.NodeType == (int)WfNodeType.Audit || src.NodeType == (int)WfNodeType.Cc))
                {
                    if (!IsNodeComplete(tasksByNode, src)) return false;
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
        /// 一次性加载某实例的全部任务，并按 NodeId 分组成字典（供并行 join 批量判定完成，避免逐节点查库 N+1）。
        /// </summary>
        private Dictionary<long, List<WfFlowTask>> LoadNodeTasks(long instanceId)
        {
            return Context.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == instanceId)
                .ToList()
                .GroupBy(t => t.NodeId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// 判断节点是否完成（或签：任一已审；会签：全部已审）。
        /// 单节点查库版（串行主路径用）。
        /// </summary>
        private bool IsNodeComplete(long instanceId, WfFlowNode node)
            => IsNodeComplete(LoadNodeTasks(instanceId), node);

        /// <summary>
        /// 判断节点是否完成（或签：任一已审；会签：全部已审）——内存版，接收已按节点分组的任务字典，
        /// 供并行 join / 分组汇聚在一次性加载后批量判定，消除逐节点查库的 N+1。
        ///
        /// 「完成」三态语义（与并行 fork 时对条件不满足成员创建 Skipped 的规则配套）：
        /// - 无 Task     → 未激活（从未走到该节点）→ 未完成 ❌
        /// - Pending     → 未完成 ❌
        /// - Done        → 完成 ✅
        /// - Skipped     → 明确跳过 → 完成 ✅
        /// 依赖前提：并行 fork 保证组内每个"应激活"的成员都至少有一条任务（Pending 或 Skipped），
        /// 故"无 Task"只可能表示"该分支从未被 fork 激活"，绝不能视为完成，否则 Join/分组汇聚会提前放行。
        /// </summary>
        private bool IsNodeComplete(Dictionary<long, List<WfFlowTask>> tasksByNode, WfFlowNode node)
        {
            if (!tasksByNode.TryGetValue(node.NodeId, out var tasks) || tasks == null || tasks.Count == 0) return false; // 无 Task = 未激活 = 未完成
            // 抄送节点：任务生成即视为完成（状态 Skipped），无需审批；并行汇聚时依赖此判定
            if (node.NodeType == (int)WfNodeType.Cc)
                return !tasks.Any(t => t.Status == (int)WfTaskStatus.Pending);
            if (node.SignType == (int)WfSignType.And || node.SignType == (int)WfSignType.Sequential)
                // 会签/依次：已减签(Skipped)或被跳过(AutoSkip)的任务不阻塞完成判定，仅校验未跳过的任务是否全部 Done
                return tasks.Where(t => t.Status != (int)WfTaskStatus.Skipped).All(t => t.Status == (int)WfTaskStatus.Done);
            // 或签：已跳过(AutoSkip)的 Skipped 任务同样不阻塞（如审批人为空自动跳过）。
            // 若无未跳过任务（整节点被跳过），视为完成；否则要求未跳过的任务中任一 Done 即可。
            var effective = tasks.Where(t => t.Status != (int)WfTaskStatus.Skipped).ToList();
            return effective.Count == 0 || effective.Any(t => t.Status == (int)WfTaskStatus.Done);
        }

        /// <summary>
        /// 判断并行分组（ParallelGroup&gt;0）成员是否应被激活。条件模型已统一为「Edge 属性」：
        /// 并行分叉网关(7) → 该成员的出边 ConditionJson 命中才激活（对标 BPMN）。
        ///
        /// - 存在并行分叉网关(7)且有指向该成员的出边：按出边 ConditionJson 判定，命中才激活；
        ///   无条件出边 → 并发激活。
        /// - 找不到并行分叉网关（存量老数据，无显式 fork 节点，仅靠 ParallelGroup 表达并行）：
        ///   兼容回退到成员自身的节点级 ConditionField（旧模型），字段齐全才判定，避免存量并行组丢失条件语义。
        /// 条件不满足的成员由调用方建 Skipped 留痕（保持现有语义）。
        /// </summary>
        private bool ShouldActivateForkMember(WfFlowNode forkNode, WfFlowNode member, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<string, string> formValues)
        {
            if (forkNode != null && linksBySource.TryGetValue(forkNode.NodeId, out var outLinks) && outLinks.Count > 0)
            {
                var link = outLinks.FirstOrDefault(l => l.TargetNodeId == member.NodeId);
                if (link != null)
                {
                    if (string.IsNullOrWhiteSpace(link.ConditionJson)) return true; // 无条件出边：并发激活
                    return EvalLinkCondition(link.ConditionJson, formValues);
                }
            }
            // 存量兼容：无显式分叉网关或该成员无对应出边时，回退到成员自身的节点级条件（旧模型，供老数据）。
            // 新模型（分叉出边带条件）优先走上方分支；普通并行成员在新模型下不应再配节点级条件。
            if (!string.IsNullOrWhiteSpace(member.ConditionField) && member.ConditionOp != (int)WfConditionOp.None && !string.IsNullOrWhiteSpace(member.ConditionValue))
            {
                if (!formValues.TryGetValue(member.ConditionField, out var raw) || string.IsNullOrWhiteSpace(raw))
                    return false;
                return CompareValue((WfConditionOp)member.ConditionOp, raw, member.ConditionValue);
            }
            return true; // 无条件：并发激活
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
        /// 有效用户查询起点：未删除（DelFlag==0）且未停用（Status==0）。
        /// 引擎内所有取用户处统一复用，避免审批人解析/转办等场景选到已删除或停用的用户导致流程卡死。
        /// </summary>
        private ISugarQueryable<SysUser> ActiveUsers()
            => Context.Queryable<SysUser>().Where(u => u.DelFlag == 0 && u.Status == 0);

        /// <summary>
        /// 解析节点审批人列表，统一返回 (UserId, UserName, NickName)。
        /// 定义态存的是稳定标识：ApproverType=0/指定用户存 userId；
        /// =1 角色Id；=2 部门Id；=3 表单字段 key，字段值为逗号分隔的 userId；
        /// =4 部门负责人（ApproverId 存部门Id，取部门 LeaderIds）；=5 发起人主管（取流程发起人 LeaderId）。
        /// 所有分支最终都查 SysUser 得到 UserId，运行态任务/记录直接用 UserId，避免 userName 变更失效。
        /// 各类型解析逻辑由 <see cref="IApproverResolver"/> 策略实现，此处仅查表分发 + 统一去重兜底。
        /// </summary>
        private List<ResolvedApprover> ResolveApprovers(WfFlowNode node, Dictionary<string, string> formValues, long? applyUserId = null)
        {
            if (!_approverResolvers.TryGetValue((WfApproverType)node.ApproverType, out var resolver))
            {
                // 未知审批人类型：返回空并告警，避免误当指定用户解析
                logger.Warn($"审批人类型未知：Node={node.NodeName}({node.NodeId}) ApproverType={node.ApproverType} ApproverId={node.ApproverId}");
                return new List<ResolvedApprover>();
            }

            var users = resolver.Resolve(node, formValues, applyUserId);
            // 最终按 UserId 去重兜底，防止上游分支 Distinct 遗漏导致同一审批人重复
            return users
                .GroupBy(u => u.UserId)
                .Select(g => g.First())
                .Select(u => new ResolvedApprover(u.UserId, u.UserName, u.NickName))
                .ToList();
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
            return ActiveUsers().Where(u => ids.Contains(u.UserId))
                .Select(u => new ResolvedApprover(u.UserId, u.UserName, u.NickName))
                .ToList();
        }

        /// <summary>
        /// 审批人解析策略：按 WfApproverType 各自实现「从节点定义 + 表单值解析出有效用户列表」。
        /// 新增审批人类型时实现本接口并在构造函数注册即可，无需改动 ResolveApprovers 的分发逻辑。
        /// </summary>
        private interface IApproverResolver
        {
            List<SysUser> Resolve(WfFlowNode node, Dictionary<string, string> formValues, long? applyUserId);
        }

        /// <summary>解析策略基类：复用引擎的 ActiveUsers()（有效用户谓词）与 Context。</summary>
        private abstract class ApproverResolverBase : IApproverResolver
        {
            protected readonly WfEngineService Engine;

            protected ApproverResolverBase(WfEngineService engine) => Engine = engine;

            public abstract List<SysUser> Resolve(WfFlowNode node, Dictionary<string, string> formValues, long? applyUserId);

            /// <summary>ApproverId 逗号分隔解析为 long 列表（非法值丢弃）。</summary>
            protected static List<long> ParseIds(string approverId)
                => (approverId ?? "").SplitByComma()
                    .Select(s => long.TryParse(s, out var v) ? v : (long?)null)
                    .Where(v => v.HasValue)
                    .Select(v => v.Value)
                    .ToList();
        }

        /// <summary>指定用户：ApproverId 存 userId（逗号分隔，数字）。</summary>
        private sealed class UserApproverResolver : ApproverResolverBase
        {
            public UserApproverResolver(WfEngineService engine) : base(engine) { }

            public override List<SysUser> Resolve(WfFlowNode node, Dictionary<string, string> formValues, long? applyUserId)
            {
                var userIds = ParseIds(node.ApproverId).Where(id => id > 0).Distinct().ToList();
                if (userIds.Count == 0) return new List<SysUser>();
                return Engine.ActiveUsers().Where(u => userIds.Contains(u.UserId)).Distinct().ToList();
            }
        }

        /// <summary>指定角色：ApproverId 存角色Id（逗号分隔），取拥有这些角色的用户。</summary>
        private sealed class RoleApproverResolver : ApproverResolverBase
        {
            public RoleApproverResolver(WfEngineService engine) : base(engine) { }

            public override List<SysUser> Resolve(WfFlowNode node, Dictionary<string, string> formValues, long? applyUserId)
            {
                var roleIds = ParseIds(node.ApproverId);
                if (roleIds.Count == 0) return new List<SysUser>();
                return Engine.ActiveUsers()
                    .InnerJoin<SysUserRole>((u, ur) => u.UserId == ur.UserId)
                    .Where((u, ur) => roleIds.Contains(ur.RoleId))
                    .Distinct()
                    .ToList();
            }
        }

        /// <summary>指定部门：ApproverId 存部门Id（逗号分隔），取这些部门下所有有效用户。</summary>
        private sealed class DeptApproverResolver : ApproverResolverBase
        {
            public DeptApproverResolver(WfEngineService engine) : base(engine) { }

            public override List<SysUser> Resolve(WfFlowNode node, Dictionary<string, string> formValues, long? applyUserId)
            {
                var deptIds = ParseIds(node.ApproverId);
                if (deptIds.Count == 0) return new List<SysUser>();
                return Engine.ActiveUsers().Where(u => deptIds.Contains(u.DeptId)).Distinct().ToList();
            }
        }

        /// <summary>表单字段动态审批人：ApproverId 为表单字段 key，字段值为逗号分隔的 userId。</summary>
        private sealed class FormFieldApproverResolver : ApproverResolverBase
        {
            public FormFieldApproverResolver(WfEngineService engine) : base(engine) { }

            public override List<SysUser> Resolve(WfFlowNode node, Dictionary<string, string> formValues, long? applyUserId)
            {
                var key = node.ApproverId ?? "";
                if (string.IsNullOrWhiteSpace(key) || formValues == null || !formValues.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                    return new List<SysUser>();
                var userIds = raw.SplitByComma()
                    .Where(s => long.TryParse(s, out var id) && id > 0)
                    .Select(s => long.Parse(s))
                    .Distinct()
                    .ToList();
                if (userIds.Count == 0) return new List<SysUser>();
                return Engine.ActiveUsers().Where(u => userIds.Contains(u.UserId)).Distinct().ToList();
            }
        }

        /// <summary>部门负责人：ApproverId 存部门Id（逗号分隔），解析这些部门 LeaderIds 对应的有效用户。</summary>
        private sealed class DeptLeaderApproverResolver : ApproverResolverBase
        {
            public DeptLeaderApproverResolver(WfEngineService engine) : base(engine) { }

            public override List<SysUser> Resolve(WfFlowNode node, Dictionary<string, string> formValues, long? applyUserId)
            {
                var deptIds = ParseIds(node.ApproverId);
                if (deptIds.Count == 0) return new List<SysUser>();
                var leaderIdStrs = Engine.Context.Queryable<SysDept>()
                    .Where(d => deptIds.Contains(d.DeptId) && d.DelFlag == 0)
                    .Select(d => d.LeaderIds)
                    .ToList()
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .SelectMany(s => s.SplitByComma())
                    .Where(s => long.TryParse(s, out var id) && id > 0)
                    .Select(s => long.Parse(s))
                    .Distinct()
                    .ToList();
                if (leaderIdStrs.Count == 0) return new List<SysUser>();
                return Engine.ActiveUsers().Where(u => leaderIdStrs.Contains(u.UserId)).Distinct().ToList();
            }
        }

        /// <summary>发起人主管：ApproverId 为空，运行时取流程发起人 SysUser.LeaderId 对应的有效用户。</summary>
        private sealed class ApplyLeaderApproverResolver : ApproverResolverBase
        {
            public ApplyLeaderApproverResolver(WfEngineService engine) : base(engine) { }

            public override List<SysUser> Resolve(WfFlowNode node, Dictionary<string, string> formValues, long? applyUserId)
            {
                if (applyUserId == null || applyUserId <= 0) return new List<SysUser>();
                var leaderId = Engine.Context.Queryable<SysUser>()
                    .Where(u => u.UserId == applyUserId)
                    .Select(u => u.LeaderId)
                    .ToList()
                    .FirstOrDefault();
                if (leaderId == null || leaderId <= 0) return new List<SysUser>();
                return Engine.ActiveUsers().Where(u => u.UserId == leaderId).Distinct().ToList();
            }
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
        private void BatchCreateTasks(long instanceId, long nodeId, string nodeName, List<ResolvedApprover> assignees, int status, string createBy, DateTime? createTime = null, bool sequential = false, DateTime? deadlineTime = null)
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
                // 超时埋点：待办到达时间 + 截止时间（仅当节点配置了 TimeoutHours>0 时由调用方传入 deadlineTime）
                ArriveTime = now,
                DeadlineTime = deadlineTime,
                Create_time = now,
                Create_by = createBy
            }).ToList();
            Context.Insertable(tasks).ExecuteCommand();
        }

        /// <summary>
        /// 根据节点超时配置计算待办截止时间。TimeoutHours>0 时返回 ArriveTime + TimeoutHours（小时），
        /// 否则返回 null（无超时约束）。供 ArriveNode 生成审批待办时传入 BatchCreateTasks。
        /// </summary>
        private static DateTime? ComputeDeadline(WfFlowNode node, DateTime arriveTime)
            => node.TimeoutHours > 0 ? arriveTime.AddHours(node.TimeoutHours) : (DateTime?)null;

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
        /// 排他条件节点（或任意"条件不满足"节点）顺延跳过时，对**每条条件不满足的出边**沿下游链路级联建 Skipped 留痕。
        /// 目的：被跳过的分支若下游直接汇入汇聚网关(8)/并行分组出口，其末端业务节点（Audit/Cc）会因"从未被 ArriveNode"
        /// 而没有任何任务；IsNodeComplete 已规定"无 task = 未激活 = 未完成"，从而 Join 汇聚会傻等这个永远到不了的节点而卡死。
        /// 级联留痕后，这些节点的 IsNodeComplete 因有 Skipped → 返回完成，Join 正确放行，使"未到达 / 跳过 / 已完成"三态收敛为两态
        /// （激活态走正常判定；跳过态 Skipped→完成；无 task 只可能出现在"本就不该走到"的分支，Join 不会等待它）。
        /// 级联边界：遇 ParallelFork(7)/ParallelJoin(8)/流程终点停止，不跨汇聚网关污染其它分支；节点已存在任务（Pending/Done/Skipped）则跳过，避免重复留痕。
        /// </summary>
        private void SkipRejectedBranches(WfFlowInstance instance, WfFlowNode node, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<long, List<WfNodeLink>> linksByTarget, Dictionary<string, string> formValues, int depth)
        {
            if (!linksBySource.TryGetValue(node.NodeId, out var outLinks) || outLinks.Count == 0) return;
            foreach (var link in outLinks)
            {
                // 仅处理"条件不满足"的出边（无条件默认分支视为满足，已被 ResolveNextNode 顺延走到，不在此标）
                if (string.IsNullOrWhiteSpace(link.ConditionJson) || EvalLinkCondition(link.ConditionJson, formValues)) continue;
                var target = allNodes.FirstOrDefault(n => n.NodeId == link.TargetNodeId);
                if (target == null) continue;
                SkipBranchChain(instance, target, allNodes, linksBySource, linksByTarget, formValues, new HashSet<long>(), depth);
            }
        }

        /// <summary>
        /// 从 branchStart 出发沿出边 DFS 下游链路，把被跳过分支整条链"建 Skipped 留痕 + 激活下游汇聚点"。
        /// - Audit/Cc：建 Skipped（不建 Pending）；继续沿下游链递归（不调 ArriveNode，避免落入正常待办逻辑）。
        /// - 条件网关：自身不建留痕，但其"满足出边"应由调用方 ArriveNode，故此处仅对"不满足出边"递归（防重复时由 visited 去重）。
        /// - ParallelJoin(8)：不建留痕，但需 ArriveNode 激活汇聚网关（让它与其它真实分支一起等待 join），随后停止本链（不跨网关污染另一分支）。
        /// - ParallelFork(7)/流程终点：停止，不跨并行子图。
        /// 已存在任务（Pending/Done/Skipped）的节点跳过留痕，但仍继续向下游级联（如条件网关已留痕但下游分支还需标）。
        /// </summary>
        private void SkipBranchChain(WfFlowInstance instance, WfFlowNode branchStart, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<long, List<WfNodeLink>> linksByTarget, Dictionary<string, string> formValues, HashSet<long> visited, int depth)
        {
            if (branchStart == null || visited.Contains(branchStart.NodeId)) return;
            visited.Add(branchStart.NodeId);

            // 汇聚网关：激活它（等其它分支），不跨网关继续
            if (branchStart.NodeType == (int)WfNodeType.ParallelJoin)
            {
                ArriveNode(instance, branchStart, allNodes, linksBySource, linksByTarget, formValues, depth: depth);
                return;
            }
            // 分叉网关 / 终点：不进入并行子图，停止
            if (branchStart.NodeType == (int)WfNodeType.ParallelFork) return;

            // 真实业务节点（Audit/Cc）：建 Skipped 留痕（已存在任务则跳过），继续沿下游链级联
            if (branchStart.NodeType == (int)WfNodeType.Audit || branchStart.NodeType == (int)WfNodeType.Cc)
            {
                var existing = Context.Queryable<WfFlowTask>().Any(t => t.InstanceId == instance.InstanceId && t.NodeId == branchStart.NodeId);
                if (!existing) CreateAutoSkipTask(instance, branchStart, "上游条件不满足，分支自动跳过");
            }

            // 向下游继续级联（终点无出边自然停止）
            if (linksBySource.TryGetValue(branchStart.NodeId, out var outs) && outs.Count > 0)
            {
                foreach (var l in outs)
                {
                    var next = allNodes.FirstOrDefault(n => n.NodeId == l.TargetNodeId);
                    if (next != null) SkipBranchChain(instance, next, allNodes, linksBySource, linksByTarget, formValues, visited, depth);
                }
            }
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
            // 每个收件人落一条抄送记录并写入各自的 OperatorId（userId），便于按 userId 精确匹配（抄送给我/数据面板）。
            // 批量 Insertable 一次入库，避免逐条 ExecuteCommand 的多次往返。
            var now = DateTime.Now;
            Context.Insertable(ccList.Select(c => new WfFlowRecord
            {
                InstanceId = instance.InstanceId,
                TaskId = null,
                NodeId = node.NodeId,
                Operator = c.UserName,
                OperatorId = c.UserId,
                OperatorNickName = c.NickName,
                Action = (int)WfAction.Cc,
                Opinion = "抄送",
                Create_time = now,
                Create_by = c.UserName
            }).ToList()).ExecuteCommand();
            NotifyUsers(ccList, $"【审批抄送】{instance.Title}（{instance.FlowName}）抄送知会，请知悉。");
        }

        /// <summary>
        /// 统一创建流程操作记录。操作人以 <see cref="ResolvedApprover"/>（userId + 名称快照）传入，
        /// 调用方已持有完整身份，此处不再按登录名反查用户表。
        /// 落库后，对"审批类动作"异步生成 AI 摘要写回（不阻塞主流程，异常不影响主链路）。
        /// </summary>
        private void AddRecord(long instanceId, long? taskId, long? nodeId, ResolvedApprover op, int action, string opinion, DateTime? createTime = null)
        {
            var record = new WfFlowRecord
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
            };
            Context.Insertable(record).ExecuteCommand();

            // 提交后 AI 摘要（仅审批类动作：同意/驳回/转交/加签/减签/委托/管理员跳转/重新提交/撤回/催办）
            if (action != (int)WfAction.Submit && action != (int)WfAction.Cc && action != (int)WfAction.AutoSkip)
            {
                var nodeName = GetNodeNameSafe(nodeId);
                _ = GenerateRecordSummaryAsync(record.RecordId, instanceId, nodeName, opinion);
            }
        }

        /// <summary>
        /// 异步生成审批记录 AI 摘要并写回（fire-and-forget，异常吞掉不影响主流程）
        /// </summary>
        private async Task GenerateRecordSummaryAsync(long recordId, long instanceId, string nodeName, string opinion)
        {
            try
            {
                var formContent = Context.Queryable<WfFlowInstance>()
                    .Where(i => i.InstanceId == instanceId)
                    .Select(i => i.FormContent)
                    .First();
                var summary = await _aiService.SummarizeApprovalAsync(string.Empty, nodeName, opinion, formContent);
                if (!string.IsNullOrWhiteSpace(summary?.Summary))
                {
                    Context.Updateable<WfFlowRecord>()
                        .SetColumns(r => r.Summary == summary.Summary)
                        .Where(r => r.RecordId == recordId)
                        .ExecuteCommand();
                }
            }
            catch (Exception ex)
            {
                // AI 摘要失败不应影响主流程，仅记录日志
                logger.Warn(ex, $"生成审批记录 AI 摘要失败 recordId={recordId}");
            }
        }

        private string GetNodeNameSafe(long? nodeId)
        {
            if (!nodeId.HasValue) return string.Empty;
            return Context.Queryable<WfFlowNode>().Where(n => n.NodeId == nodeId.Value).Select(n => n.NodeName).First() ?? string.Empty;
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
