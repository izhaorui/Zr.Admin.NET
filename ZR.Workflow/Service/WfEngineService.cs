using ZR.ServiceCore.Services;
using ZR.Workflow.Service.Engine;

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
    /// 文件拆分（partial，位于 Service/Engine/）：Simulate / Webhook / Admin / Approvers。表单值见 <see cref="WfFormValueHelper"/>。
    /// </summary>
    [AppService(ServiceType = typeof(IWfEngineService))]
    public partial class WfEngineService : BaseService<WfFlowInstance>, IWfEngineService
    {
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly ISysUserMsgService _msgService;
        private readonly IWfWebhookService _webhookService;
        private readonly IWfAiService _aiService;
        private readonly ISmsSender _smsSender;

        /// <summary>
        /// 审批人解析策略注册表：WfApproverType → IApproverResolver。
        /// 新增审批人类型时只需「加一个 resolver 类 + 一行注册」，无需再改 switch 分支。
        /// </summary>
        private readonly Dictionary<WfApproverType, IApproverResolver> _approverResolvers;

        public WfEngineService(ISysUserMsgService msgService, IWfWebhookService webhookService, IWfAiService aiService, ISmsSender smsSender)
        {
            _msgService = msgService;
            _webhookService = webhookService;
            _aiService = aiService;
            _smsSender = smsSender;

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
            var (def, topo, firstNode) = PrepareStartFlow(instance);
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
                ArriveOrComplete(instance, firstNode, topo, formValues);
            }, "发起申请失败");

            return instance.InstanceId;
        }

        /// <summary>
        /// 通过
        /// </summary>
        public void Approve(long taskId, string opinion, long operatorId, string formContent = null)
        {
            var (task, instance) = LoadPendingTaskAndInstance(taskId, operatorId);
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("流程状态异常，无法审批");

            var op = LoadUser(operatorId);
            // 委托代审：操作人实为代审人，记录标注"代 X 审批"（X=原审批人昵称）
            var delegatedNote = IsDelegatedOperator(task, operatorId) ? $"（代 {task.AssigneeNickName} 审批）" : "";
            logger.Info($"审批通过：InstanceId={instance.InstanceId} TaskId={taskId} Node={task.NodeName}({task.NodeId}) 操作人={op.NickName}({operatorId}){delegatedNote}");

            var node = Context.Queryable<WfFlowNode>().First(n => n.NodeId == task.NodeId);
            var topo = LoadTopology(instance.FlowId);
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

                // 审批人编辑字段回写：仅当提交了 formContent 时按当前节点 FieldPermission 校验并更新实例表单，
                // 回写后持久化到库，后续节点条件/展示基于新值。无权字段被修改则抛异常回滚本事务。
                if (!string.IsNullOrWhiteSpace(formContent))
                {
                    ApplyApproveFormEdit(instance, node, formContent, op.UserName, now);
                }

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
                AdvanceToNext(instance, node, topo, formValues);
            }, "审批失败");
        }

        /// <summary>
        /// 驳回。节点策略为审批人自选时，按本次提交的策略/目标回退；其它策略忽略请求覆盖。
        /// </summary>
        public void Reject(long taskId, string opinion, long operatorId, int? rejectStrategy = null, long? rejectTargetNodeId = null)
        {
            var (task, instance) = LoadPendingTaskAndInstance(taskId, operatorId);
            var op = LoadUser(operatorId);
            var delegatedNote = IsDelegatedOperator(task, operatorId) ? $"（代 {task.AssigneeNickName} 审批）" : "";
            logger.Info($"审批驳回：InstanceId={instance.InstanceId} TaskId={taskId} Node={task.NodeName}({task.NodeId}) 操作人={op.NickName}({operatorId}){delegatedNote}");
            var node = Context.Queryable<WfFlowNode>().First(n => n.NodeId == task.NodeId);
            var topo = LoadTopology(instance.FlowId);
            var nodeStrategy = node == null ? WfRejectStrategy.ToApplicant : (WfRejectStrategy)node.RejectStrategy;
            // 自选策略在事务外先校验，非法选择直接失败，不把任务标成已处理
            WfFlowNode selectedTarget = null;
            WfRejectStrategy? runtimeStrategy = null;
            if (nodeStrategy == WfRejectStrategy.ApproverSelect)
            {
                selectedTarget = ResolveApproverSelectedTarget(node, topo, rejectStrategy, rejectTargetNodeId);
                runtimeStrategy = (WfRejectStrategy)rejectStrategy.GetValueOrDefault();
            }

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

                var targetNode = runtimeStrategy.HasValue
                    ? selectedTarget
                    : ResolveRejectTarget(node, topo);
                var usedStrategy = runtimeStrategy ?? nodeStrategy;

                if (targetNode == null)
                {
                    if (usedStrategy == WfRejectStrategy.ToApplicant)
                        logger.Info($"驳回发起人：InstanceId={instance.InstanceId} 策略={usedStrategy}");
                    else
                        logger.Info($"驳回退化：InstanceId={instance.InstanceId} 无回退目标节点 → 直接驳回发起人（策略={usedStrategy}）");
                    instance.Status = (int)WfInstanceStatus.Rejected;
                    Context.Updateable(instance).UpdateColumns(i => new { i.Status }).ExecuteCommand();
                    Context.Updateable<WfFlowTask>()
                        .SetColumns(t => new WfFlowTask { Status = (int)WfTaskStatus.Skipped })
                        .Where(t => t.InstanceId == instance.InstanceId && t.Status == (int)WfTaskStatus.Pending)
                        .ExecuteCommand();
                    return;
                }

                // 回退到目标节点重新审批：清掉目标及之后所有任务（轨迹保留在 WfFlowRecord），重置活动集并重新进入目标节点
                logger.Info($"驳回回退：InstanceId={instance.InstanceId} 回退到节点 {targetNode.NodeName}({targetNode.NodeId})（策略={usedStrategy}）");
                RollbackToNode(instance, targetNode, topo);
            }, "驳回失败");
        }

        /// <summary>
        /// 按节点驳回策略解析回退目标。策略 0、策略 3（无运行时选择）或找不到合法审批节点时返回 null（调用方驳回发起人）。
        /// 策略 2 存量数据只要目标仍是本流程审批节点即回退，不因 NodeOrder 在当前节点之后而拒绝。
        /// </summary>
        private WfFlowNode ResolveRejectTarget(WfFlowNode node, WorkflowTopology topo)
        {
            if (node == null || topo == null) return null;
            var strategy = (WfRejectStrategy)node.RejectStrategy;
            if (strategy == WfRejectStrategy.ApproverSelect)
            {
                logger.Info($"驳回策略为审批人自选且无运行时选择 → 退化为驳回发起人 Node={node.NodeName}({node.NodeId})");
                return null;
            }
            if (strategy == WfRejectStrategy.ToPrevNode)
            {
                return topo.OrderedNodes
                    .Where(n => n.NodeType == (int)WfNodeType.Audit && n.NodeOrder < node.NodeOrder)
                    .OrderByDescending(n => n.NodeOrder)
                    .FirstOrDefault();
            }
            if (strategy == WfRejectStrategy.ToSpecifiedNode && node.RejectTargetNodeId.HasValue && node.RejectTargetNodeId.Value != 0)
            {
                var target = topo.GetNode(node.RejectTargetNodeId.Value);
                if (target == null)
                {
                    logger.Info($"驳回目标不存在 NodeId={node.RejectTargetNodeId} → 退化为驳回发起人");
                    return null;
                }
                if (target.NodeType != (int)WfNodeType.Audit)
                {
                    logger.Info($"驳回目标不是审批节点 Node={target.NodeName}({target.NodeId}) type={target.NodeType} → 退化为驳回发起人");
                    return null;
                }
                if (target.NodeId == node.NodeId)
                {
                    logger.Info($"驳回目标不能是自身 Node={node.NodeName}({node.NodeId}) → 退化为驳回发起人");
                    return null;
                }
                return target;
            }
            return null;
        }

        /// <summary>
        /// 审批人自选：按本次提交的 0/1/2 解析目标。非法选择抛业务异常，不静默退化。
        /// 策略 0 返回 null（驳回发起人）；策略 1 取 NodeOrder 上一个审批节点；策略 2 校验指定目标在当前节点之前。
        /// </summary>
        private WfFlowNode ResolveApproverSelectedTarget(WfFlowNode node, WorkflowTopology topo, int? rejectStrategy, long? rejectTargetNodeId)
        {
            if (node == null || topo == null) throw new CustomException("驳回失败");
            if (!rejectStrategy.HasValue
                || rejectStrategy.Value < (int)WfRejectStrategy.ToApplicant
                || rejectStrategy.Value > (int)WfRejectStrategy.ToSpecifiedNode)
                throw new CustomException("请选择驳回方式");

            var strategy = (WfRejectStrategy)rejectStrategy.Value;
            if (strategy == WfRejectStrategy.ToApplicant) return null;

            if (strategy == WfRejectStrategy.ToPrevNode)
            {
                var prev = topo.OrderedNodes
                    .Where(n => n.NodeType == (int)WfNodeType.Audit && n.NodeOrder < node.NodeOrder)
                    .OrderByDescending(n => n.NodeOrder)
                    .FirstOrDefault();
                if (prev == null) throw new CustomException("没有可回退的上一审批节点");
                return prev;
            }

            if (!rejectTargetNodeId.HasValue || rejectTargetNodeId.Value == 0)
                throw new CustomException("请选择驳回目标节点");
            var target = topo.GetNode(rejectTargetNodeId.Value);
            if (target == null) throw new CustomException("驳回目标不在本流程中");
            if (target.NodeType != (int)WfNodeType.Audit) throw new CustomException("驳回目标必须是审批节点");
            if (target.NodeId == node.NodeId) throw new CustomException("驳回目标不能是自身");
            if (target.NodeOrder >= node.NodeOrder) throw new CustomException("驳回目标须为当前节点之前的审批节点");
            return target;
        }

        /// <summary>
        /// 将流程回退到指定节点重新审批（可配置驳回策略：驳回到上一步 / 指定节点）。
        /// 清掉目标节点及其之后所有任务，重置活动集为目标节点，重新触发该节点的进入/任务生成。
        /// 历史审批轨迹由 WfFlowRecord 保留，任务仅作为"当前待办"快照被清理。
        /// </summary>
        private void RollbackToNode(WfFlowInstance instance, WfFlowNode targetNode, WorkflowTopology topo)
        {
            var cleanupIds = topo.OrderedNodes
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
            ArriveNode(instance, targetNode, topo, formValues);
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
            var (def, topo, firstNode) = PrepareStartFlow(instance);
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
                ArriveOrComplete(instance, firstNode, topo, formValues);
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
            var topo = LoadTopology(instance.FlowId);
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
                AdvanceToNext(instance, node, topo, formValues);
            }, "超时自动通过失败");
        }

        /// <summary>
        /// 超时自动驳回：以申请人名义将待办置为驳回，按节点驳回策略回退（复用 Reject 事务体）。
        /// </summary>
        private void AutoRejectTask(WfFlowTask task, WfFlowInstance instance, WfFlowNode node)
        {
            var topo = LoadTopology(instance.FlowId);
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

                var targetNode = ResolveRejectTarget(node, topo);

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

                RollbackToNode(instance, targetNode, topo);
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
                var topo = LoadTopology(instance.FlowId);
                var formValues = ParseFormValues(instance);
                AdvanceToNext(instance, node, topo, formValues);
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
        /// <see cref="ZR.Repository.BaseRepository{T}.UseTran(Action)"/> + 失败包装的统一入口。
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
        /// 加载某 FlowId 的完整静态拓扑（节点 + 连线构建的 O(1) 索引 + 出边条件预解析）。
        /// 每次操作现构建一次，不做缓存；构建时对带条件的出边做静态配置校验（发起前暴露配置错误）。
        /// 替代旧的三件套 <c>allNodes + linksBySource + linksByTarget</c>。
        /// </summary>
        private WorkflowTopology LoadTopology(long flowId)
            => WfWorkflowTopologyBuilder.Build(Context, flowId);

        /// <summary>
        /// Start / Resubmit 共同的 pre-flight：取定义、校验、构建静态拓扑、取首节点。
        /// 调用方在事务体内完成各自的 Insert / Update 持久化差异。
        /// </summary>
        private (WfFlowDefinition def, WorkflowTopology topo, WfFlowNode firstNode) PrepareStartFlow(WfFlowInstance instance)
        {
            var def = LoadActivatableDefinition(instance.FlowId);
            if (string.IsNullOrEmpty(instance.FlowName)) instance.FlowName = def.FlowName;
            var topo = LoadTopology(instance.FlowId);
            // 首节点须包含条件网关（NodeType=4）与并行分叉网关（NodeType=7）：网关可作为流程的第一个节点（发起后立即分流/分叉）。
            // 若这里沿用 IsAuditableNode（只认 Audit/Cc），会直接跳过网关落到 NodeOrder 上的第一个审批节点，
            // 导致分支条件从未被评估、始终走"第一条分支"。ArriveNode 内部会对 Condition/ParallelFork 做透传处理。
            var firstNode = topo.OrderedNodes.FirstOrDefault(n => WfFormValueHelper.IsAuditableNode(n.NodeType) || n.NodeType == (int)WfNodeType.Condition || n.NodeType == (int)WfNodeType.ParallelFork);
            return (def, topo, firstNode);
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
        private void ArriveOrComplete(WfFlowInstance instance, WfFlowNode next, WorkflowTopology topo, Dictionary<string, string> formValues, int depth = 0)
        {
            if (next == null) CompleteInstance(instance);
            else ArriveNode(instance, next, topo, formValues, depth: depth);
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
        /// 将 FormContent(JSON) 解析为 字段-&gt;值 字典（值均为字符串）。整体格式错误返回空字典。
        /// 逐字段容错：数组/对象值（如明细表 table 的行数组）序列化为 JSON 字符串存储，
        /// 单字段类型不符不再导致整个表单解析失败（旧逻辑 Dictionary&lt;string,string&gt; 遇数组整体炸成空 dict，条件字段全丢）。
        /// </summary>
        private Dictionary<string, string> ParseFormValues(WfFlowInstance instance)
            => Engine.WfFormValueHelper.ParseObject(instance.FormContent);

        /// <summary>
        /// 审批人编辑字段回写：按当前节点 <see cref="WfFlowNode.FieldPermission"/> 校验并持久化更新实例表单。
        /// 仅允许更新 perm=0（可编辑）的字段；perm=1（只读）/perm=2（隐藏）/未在列表中声明但节点已配置其它字段 的字段被修改则抛异常。
        /// 节点完全未配置 FieldPermission 时，按"未配置字段默认可编辑"处理，允许审批人提交变更。
        /// 回写成功后同时更新数据库 FormContent 与审计字段，后续节点条件/展示使用新值。
        /// </summary>
        /// <param name="instance">流程实例（事务内内存对象，回写 FormContent）</param>
        /// <param name="node">当前审批节点</param>
        /// <param name="formContent">审批人提交的表单变更（JSON，字段-&gt;值，值均为字符串）</param>
        /// <param name="updateBy">当前操作人用户名（写入 Update_by）</param>
        /// <param name="updateTime">当前操作时间（写入 Update_time）</param>
        private void ApplyApproveFormEdit(WfFlowInstance instance, WfFlowNode node, string formContent, string updateBy, DateTime updateTime)
        {
            var submitted = JsonConvert.DeserializeObject<Dictionary<string, string>>(formContent)
                ?? throw new CustomException("表单数据格式错误");
            if (submitted.Count == 0) return;

            // 节点未配置任何字段权限 → 全部字段默认可编辑
            if (string.IsNullOrWhiteSpace(node.FieldPermission))
            {
                MergeFormContent(instance, submitted, updateBy, updateTime);
                return;
            }

            // 解析节点字段权限：perm=0 可编辑；perm=1 只读；perm=2 隐藏；其它值按 0 处理。
            // 未在列表中声明的字段默认可编辑（与详情过滤侧语义一致）。
            var restrictedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = JsonConvert.DeserializeObject<List<WfFieldPermissionItem>>(node.FieldPermission) ?? new List<WfFieldPermissionItem>();
            foreach (var it in items)
            {
                if (string.IsNullOrWhiteSpace(it.Field)) continue;
                if (it.Perm == 1 || it.Perm == 2) restrictedFields.Add(it.Field);
            }

            // 越权校验：提交的字段若被显式声明为 perm=1（只读）或 perm=2（隐藏）则拒绝；未声明字段默认可编辑。
            foreach (var f in submitted.Keys)
            {
                if (restrictedFields.Contains(f))
                    throw new CustomException($"无权限修改表单字段「{f}」");
            }

            MergeFormContent(instance, submitted, updateBy, updateTime);
        }

        /// <summary>
        /// 合并审批人提交的字段变更到实例表单并持久化。
        /// </summary>
        private void MergeFormContent(WfFlowInstance instance, Dictionary<string, string> submitted, string updateBy, DateTime updateTime)
        {
            var current = string.IsNullOrWhiteSpace(instance.FormContent)
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : (JsonConvert.DeserializeObject<Dictionary<string, string>>(instance.FormContent)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            foreach (var f in submitted)
                current[f.Key] = f.Value;
            instance.FormContent = JsonConvert.SerializeObject(current);
            instance.Update_time = updateTime;
            instance.Update_by = updateBy;

            // 持久化到库：本次审批人对有权字段的修改必须与审批操作原子提交
            Context.Updateable(instance)
                .UpdateColumns(i => new { i.FormContent, i.Update_time, i.Update_by })
                .ExecuteCommand();
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
        private void ArriveNode(WfFlowInstance instance, WfFlowNode node, WorkflowTopology topo, Dictionary<string, string> formValues, bool singleNodeOnly = false, int depth = 0)
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
            var nodeKind = topo.NodeKind.TryGetValue(node.NodeId, out var kind) ? kind : (WfNodeType)node.NodeType;
            logger.Info($"进入节点：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId}) Type={nodeKind}{(node.ParallelGroup > 0 ? $" ParallelGroup={node.ParallelGroup}" : "")}");

            // 按节点类型分派：网关/终端节点（Condition/End/ParallelFork/ParallelJoin）各自处理；
            // 其余（Audit/Cc/并行分组）落到下方统一业务节点处理。
            switch (nodeKind)
            {
                // 条件网关（菱形，NodeType=4）：本身不生成任务，到达后按出边 ConditionJson 选一路继续。
                // 条件在连线（link）上表达，无需节点级 EvalCondition；无条件出边作为默认分支。
                case WfNodeType.Condition:
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
                    var satisfiedNext = ResolveNextNode(node, topo, formValues);
                    SkipRejectedBranches(instance, node, topo, formValues, depth);
                    if (satisfiedNext != null)
                    {
                        ArriveOrComplete(instance, satisfiedNext, topo, formValues, depth + 1);
                    }
                    else
                    {
                        // 无满足分支也无默认出边：把"不满足出边"的下游链作为被跳过分支激活（建 Skipped 并推进到汇聚点），避免流程误判完成
                        var outLinks = topo.GetOutLinks(node.NodeId);
                        var fallbacks = outLinks.Count > 0
                            ? outLinks.Where(l => l.HasCondition && !WfFormValueHelper.EvalParsedCondition(l, formValues))
                                  .Select(l => topo.GetNode(l.TargetNodeId)).Where(n => n != null).ToList()
                            : new List<WfFlowNode>();
                        if (fallbacks.Count > 0) { foreach (var fb in fallbacks) SkipBranchChain(instance, fb, topo, formValues, new HashSet<long>(), depth); }
                        else CompleteInstance(instance);
                    }
                    return;
                }

                // 结束节点(3)：流程到达终点，不生成任务，直接完成实例。
                case WfNodeType.End:
                {
                    RemoveActiveNodeId(instance, node.NodeId);
                    SyncActiveNodeId(instance);
                    CompleteInstance(instance);
                    return;
                }

                // 并行分叉网关(7)：本身不生成任务，fork 同时激活全部出边目标（多活动分支并发）。
                case WfNodeType.ParallelFork:
                {
                    RemoveActiveNodeId(instance, node.NodeId);
                    // 并行发散：返回全部无条件/命中条件分支并发激活，与排他选路（ResolveNextNode）语义不同
                    var targets = ResolveParallelTargets(node, topo, formValues);
                    if (targets.Count == 0) { CompleteInstance(instance); return; }
                    logger.Info($"并行分叉网关：InstanceId={instance.InstanceId} Fork={node.NodeName}({node.NodeId}) → {targets.Count} 条出边");
                    foreach (var t in targets) ArriveNode(instance, t, topo, formValues, depth: depth + 1);
                    SyncActiveNodeId(instance);
                    return;
                }

                // 并行汇聚网关(8)：本身不生成任务，等待所有入边分支均完成才继续（join）。
                case WfNodeType.ParallelJoin:
                {
                    // 仅加载该 Join 入边分支节点（Audit/Cc）的任务供完成判定，避免全量拉取实例全部任务
                    // （含已结束分支 / 其它并行组）造成的重复查询与数据量浪费。
                    var tasksByNode = LoadJoinBranchTasks(instance.InstanceId, node, topo);
                    if (IsJoinComplete(instance, node, topo, tasksByNode))
                    {
                        logger.Info($"并行汇聚完成：InstanceId={instance.InstanceId} Join={node.NodeName}({node.NodeId}) 全部入边完成 → 继续推进");
                        RemoveActiveNodeId(instance, node.NodeId);
                        var after = ResolveNextNode(node, topo, formValues);
                        ArriveOrComplete(instance, after, topo, formValues, depth + 1);
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
            }

            // 并行分支：首次到达该分组时，同时激活组内所有满足条件的节点。
            // 但管理员跳转（singleNodeOnly=true）到组内某成员时，跳过整组 fork，落到下方"非并行节点"分支只激活目标节点自身。
            // ⚠️ 若该分组存在并行分叉网关(7)，则并行激活已由 ParallelFork 分支 fork 其出边目标完成，
            //    成员到达不再走"整组 fork"（那是纯并行分组无网关时的机制）——否则全成员 AutoSkip（不加活动集）
            //    会使 groupActive 恒为 false，ParallelFork 对每个出边目标各触发一次整组 fork，导致成员重复 AutoSkip。
            if (node.ParallelGroup > 0 && !singleNodeOnly
                && !(topo.ForkByGroup.TryGetValue(node.ParallelGroup, out var forkGw) && forkGw != null))
            {
                logger.Info($"并行分组进入：InstanceId={instance.InstanceId} Node={node.NodeName}({node.NodeId}) ParallelGroup={node.ParallelGroup}");
                var groupNodes = topo.GetGroupNodes(node.ParallelGroup);
                var groupNodeIds = groupNodes.Select(g => g.NodeId).ToList();
                // 分组是否"已激活"（已 fork 过）：以「活动集 CurrentNodeIds」为唯一权威判断依据，而非 Task 状态。
                // ⚠️ 原则（2026-08-20）：CurrentNodeIds = 运行时活动状态；WfFlowTask = 节点执行历史/人工任务状态。
                //   Task.Pending 不能反过来作为"流程是否激活"的判断依据——AdminJump/超时/重新提交/异常恢复都会改任务与活动集，
                //   若用 Task 反推会导致两套状态真相漂移（例：组内只剩 Skipped 旧任务但活动集已无成员，Task 判断会误判"已激活"而不再 fork）。
                // 并行分组成员 fork 时经 AddActiveNodeId 加入活动集，故"活动集中是否含组内任一成员"即代表"分组是否已 fork"，
                // 与 CurrentNodeIds 天然同步；首节点为分叉网关时活动集已在 ParallelFork 分支移除网关自身，此处判空会正确触发首次 fork。
                var groupActive = GetActiveNodeIds(instance).Any(gid => groupNodeIds.Contains(gid));
                if (!groupActive)
                {
                    // 并行分组 fork：把组内「将活动」的成员（生成待办/抄送的节点）同时加入活动集 CurrentNodeIds，
                    // 使 CurrentNodeId（取活动集 Min）与活动集保持一致；条件不满足的成员不进活动集（视为已完成）。
                    // 成员"是否激活"改由「并行分叉网关(7) → 该成员」的出边 ConditionJson 决定（Edge 属性模型，对标 BPMN）：
                    // 命中 → 激活走正常审批；不满足 → 建 Skipped 留痕。找不到分叉出边或无条件 → 无条件并发激活。
                    var forkNode = topo.ForkByGroup.TryGetValue(node.ParallelGroup, out var fk) ? fk : null;
                    foreach (var g in groupNodes)
                    {
                        // 并行分叉/汇聚网关(7/8) 由 switch 分派各自处理（ParallelFork 已 fork 出边、ParallelJoin 负责汇聚），
                        // 不作为并行分组业务成员 fork——否则网关会被当普通节点"审批人为空自动跳过"并重复留痕。
                        if (g.NodeType == (int)WfNodeType.ParallelFork || g.NodeType == (int)WfNodeType.ParallelJoin) continue;
                        if (!ShouldActivateForkMember(forkNode, g, topo, formValues))
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
                        var exits = ResolveParallelGroupExit(node.ParallelGroup, topo);
                        if (exits.Count == 0) { CompleteInstance(instance); }
                        else foreach (var exitNode in exits) ArriveNode(instance, exitNode, topo, formValues, depth: depth + 1);
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
                ArriveOrComplete(instance, ResolveNextNode(node, topo, formValues), topo, formValues, depth + 1);
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
                    ArriveOrComplete(instance, ResolveNextNode(node, topo, formValues), topo, formValues, depth + 1);
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
        private void AdvanceToNext(WfFlowInstance instance, WfFlowNode completedNode, WorkflowTopology topo, Dictionary<string, string> formValues, int depth = 0)
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
                var groupNodes = topo.GetGroupNodes(completedNode.ParallelGroup);
                var groupDone = groupNodes.All(g => IsNodeComplete(tasksByNode, g));
                if (!groupDone) { logger.Info($"并行分组汇聚等待：InstanceId={instance.InstanceId} Group={completedNode.ParallelGroup} 仍有分支未完成 → 继续等待"); SyncActiveNodeId(instance); return; } // 等待组内其余分支
                // 汇聚出口按 Link 拓扑解析（组内成员连到组外的出边），不依赖 NodeOrder（MaxBy 会破坏 Link 唯一真相）
                var exits = ResolveParallelGroupExit(completedNode.ParallelGroup, topo);
                if (exits.Count == 0) { CompleteInstance(instance); return; }
                // 出口幂等保护：并行分组内若已有成员在 fork 阶段被 Skipped（条件不满足/审批人为空），其 Approve 会再次进入本汇聚分支；
                // 若出口节点已被前次汇聚激活（已有任务），跳过重复 fork，避免出口（如 Join 后续节点 C）生成多条待办。
                // 此处查询位于 Approve 的事务内（RunInTx），可同时防并发竞态重复 fork。
                foreach (var exitNode in exits)
                {
                    var exitActivated = Context.Queryable<WfFlowTask>()
                        .Any(t => t.InstanceId == instance.InstanceId && t.NodeId == exitNode.NodeId);
                    if (exitActivated) { logger.Info($"并行分组出口幂等：InstanceId={instance.InstanceId} Exit={exitNode.NodeName}({exitNode.NodeId}) 已激活 → 跳过重复 fork"); continue; }
                    ArriveNode(instance, exitNode, topo, formValues, depth: depth + 1);
                }
                SyncActiveNodeId(instance);
                return;
            }

            // 排他选路：此处仅普通节点（Audit/Cc）完成推进，条件出边排他选第一条命中的条件边，无命中则走默认分支（严格 fallback）
            var next = ResolveNextNode(completedNode, topo, formValues);
            if (next == null) { CompleteInstance(instance); return; }

            // 出边目标是汇聚网关(8)：仅当所有入边分支均完成时，才激活 8 的后续；否则 8 入活动集等待
            if (next.NodeType == (int)WfNodeType.ParallelJoin)
            {
                if (IsJoinComplete(instance, next, topo, tasksByNode))
                {
                    RemoveActiveNodeId(instance, next.NodeId);
                    var after = ResolveNextNode(next, topo, formValues);
                    ArriveOrComplete(instance, after, topo, formValues, depth + 1);
                }
                else
                {
                    logger.Info($"汇聚网关等待：InstanceId={instance.InstanceId} Join={next.NodeName}({next.NodeId}) 仍有入边分支未完成 → 保持活动集等待");
                    AddActiveNodeId(instance, next.NodeId);
                }
            }
            else
            {
                ArriveNode(instance, next, topo, formValues, depth: depth + 1);
            }
            SyncActiveNodeId(instance);
        }

        /// <summary>
        /// 解析当前节点的全部下一节点（多目标，仅供并行分叉网关 fork 用）。
        /// **并行发散语义**：无条件出边全部分支、条件出边中命中的全部分支均作为目标并发返回——
        /// 只做"条件过滤"，不做"排他选一"。并行分叉分支必须无条件并发（模板/AI 生成的模型①约束），
        /// 此处按条件过滤是为兼容交互创建模型②（fork 与成员同 parallelGroup 的条件分支）。
        /// 出边条件已在构建拓扑时预解析并静态校验，此处只做运行时评估。
        /// 与排他选路的 <see cref="ResolveNextNode"/> 语义完全不同，勿混用。
        /// </summary>
        private List<WfFlowNode> ResolveParallelTargets(WfFlowNode current, WorkflowTopology topo, Dictionary<string, string> formValues)
        {
            var result = new List<WfFlowNode>();
            var outLinks = topo.GetOutLinks(current.NodeId);
            if (outLinks.Count == 0)
            {
                // 无出边：仅整图无任何 link 时按 NodeOrder 兜底；有 link 则无出边即终点，绝不顺延到兄弟支。
                if (topo.NextOf != null && topo.NextOf.Count > 0) return result;
                var fb = GetNextAuditNode(topo, current.NodeOrder);
                if (fb != null) result.Add(fb);
                return result;
            }
            foreach (var link in outLinks) // 已按 Sort 升序
            {
                if (link.HasCondition && !WfFormValueHelper.EvalParsedCondition(link, formValues)) continue;
                var hit = topo.GetNode(link.TargetNodeId);
                if (hit != null && !result.Contains(hit)) result.Add(hit);
            }
            return result;
        }

        /// <summary>
        /// 解析并行分组（ParallelGroup&gt;0，无显式汇聚网关）整组完成后的汇聚出口。
        /// **link 为唯一串联事实**：出口 = 组内成员指向「组外节点」的出边目标集合，绝不依赖 NodeOrder。
        /// 前端 buildSaveLinks 保证组内成员彼此不连线，只有连到组外目标才构成汇聚出口；
        /// 故此处按拓扑扫描，避免 NodeOrder 与真实连线不符时（手写 FlowJSON / 导入 / 未续号）走错分支。
        /// </summary>
        private List<WfFlowNode> ResolveParallelGroupExit(int parallelGroup, WorkflowTopology topo)
        {
            // 汇聚出口由拓扑预计算（groupExits）：组内成员指向组外节点的出边目标，link 为唯一串联事实。
            return topo.GetGroupExits(parallelGroup).ToList();
        }

        /// <summary>
        /// 判断汇聚网关(8)是否已满足 join 条件：所有入边源节点（入边中 SourceNodeId）均已"完成"。
        /// 任一入边源尚未完成 → 返回 false（继续等待）。
        /// </summary>
        private bool IsJoinComplete(WfFlowInstance instance, WfFlowNode joinNode, WorkflowTopology topo, Dictionary<long, List<WfFlowTask>> tasksByNode)
        {
            // 入边业务分支节点（Audit/Cc，由拓扑预计算）：网关源(7/8/4)视为瞬时完成，不计入 join 判定。
            // 实际并行分支的"完成"体现在分支末端的审批/抄送节点；任一业务分支未完成 → 继续等待。
            var branches = topo.GetJoinInBranches(joinNode.NodeId);
            if (branches.Count == 0) return true;
            foreach (var src in branches)
            {
                if (!IsNodeComplete(tasksByNode, src)) return false;
            }
            return true;
        }

        /// <summary>
        /// 解析当前节点的下一节点（单目标，**排他选路**：只选一路）。
        /// **link 为唯一串联事实，NodeOrder 仅作展示排序 / 存量数据兜底**。
        /// 前端为每条边（含直线）生成一条 WfNodeLink（直线 ConditionJson 留空），
        /// 分支终点 / 末节点则**不生成出边**——即"无出边 = 流程终点"，与 ValidateLinks 的口径一致。
        ///
        /// **排他选路正确语义（2026-08-20 修正）**——默认分支是严格 fallback，而非与命中条件并列后取第一个：
        /// <code>
        ///   1. 遍历所有「条件出边」，取第一条命中（EvalParsedCondition == true）的目标 → 立即返回；
        ///   2. 若无条件边命中，则落入「默认分支」（无条件出边）的目标 → 返回；
        ///   3. 若仍无（既无条件命中也无默认分支）→ 返回 null（流程结束）。
        /// </code>
        /// 该语义同时覆盖：排他条件网关(4)选路、普通节点（Audit/Cc）后接条件分支的排他流转、
        /// 汇聚网关(8)/并行分组出口的后续推进。并行并发发散由 <see cref="ResolveParallelTargets"/> 负责，勿混用。
        ///
        /// - 当前节点无出边：此节点就是终点 → 返回 null（流程结束）。
        ///   ⚠️ 有 link 数据时**绝不能** fallback 到 NodeOrder（条件分支叶子节点天然无出边，顺延会错误流入另一分支）。
        ///   fallback 仅在**流程完全无 link**（早期 NodeOrder 串联流程）时触发。
        /// </summary>
        private WfFlowNode ResolveNextNode(WfFlowNode current, WorkflowTopology topo, Dictionary<string, string> formValues)
        {
            var outLinks = topo.GetOutLinks(current.NodeId);
            if (outLinks.Count == 0)
            {
                // 仅当整图没有任何连线时，才按 NodeOrder 兜底（早期无 link 流程）。
                // 有 link 时无出边 = 本支路终点；若仍按 NodeOrder 顺延，条件分支叶子会串进兄弟支（如审批人5→审批人6）。
                if (topo.NextOf != null && topo.NextOf.Count > 0) return null;
                return GetNextAuditNode(topo, current.NodeOrder);
            }

            WfFlowNode defaultTarget = null;
            WfFlowNode siblingBypass = null;
            foreach (var link in outLinks) // 已按 Sort 升序
            {
                // 忽略「同一条件网关两个分支列头之间」的错连（并行链扁平化曾把 5→6 存进 link）。
                // 存量数据若只有这条错连，则改跳到兄弟列头的后继（通常是 JOIN），避免卡死且不经过兄弟节点。
                if (WfFormValueHelper.IsSiblingConditionBranchLink(current.NodeId, link.TargetNodeId, topo))
                {
                    siblingBypass ??= WfFormValueHelper.FirstNonSiblingOutTarget(link.TargetNodeId, topo);
                    continue;
                }
                if (!link.HasCondition)
                {
                    // 无条件出边 = 默认分支，记录首个作兜底（正常模型至多一条）
                    if (defaultTarget == null) defaultTarget = topo.GetNode(link.TargetNodeId);
                    continue;
                }
                // 第一条命中的条件出边立即返回：排他，不再考虑默认分支（默认分支是"无任何条件命中"才走的 fallback）
                if (WfFormValueHelper.EvalParsedCondition(link, formValues))
                {
                    var hit = topo.GetNode(link.TargetNodeId);
                    if (hit != null) return hit;
                }
            }
            // 无任何条件命中 → 落入默认分支（无条件出边）；再没有则用兄弟错连的汇合后继兜底
            return defaultTarget ?? siblingBypass;
        }

        /// <summary>
        /// 取下一审批/抄送节点（跳过开始/结束），NodeOrder fallback 通道使用（兼容无 link 连线的早期流程）。
        /// </summary>
        private WfFlowNode GetNextAuditNode(WorkflowTopology topo, int currentOrder)
        {
            return topo.OrderedNodes
                .Where(n => n.NodeOrder > currentOrder && WfFormValueHelper.IsAuditableNode(n.NodeType))
                .OrderBy(n => n.NodeOrder)
                .FirstOrDefault();
        }

        /// <summary>
        /// 一次性加载某实例的全部任务，并按 NodeId 分组成字典（供并行 join 批量判定完成，避免逐节点查库 N+1）。
        /// 供需要"实例全量任务"的场景使用（如 AdvanceToNext 同时判定并行分组汇聚与多个 join）。
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
        /// 仅加载某 Join 汇聚网关入边"业务分支"节点（Audit/Cc）的任务，按 NodeId 分组成字典。
        /// 相比 <see cref="LoadNodeTasks"/> 的全量实例任务加载，只查询 join 判定所需的入边分支节点，
        /// 避免"到达 join 即全量拉取该实例所有任务"（含已结束分支 / 其它并行组）造成的数据量与重复查询。
        /// 网关节点（4/7/8）本身不生成任务、由流转自然跳过，无需加载；<see cref="IsJoinComplete"/> 也只会对
        /// Audit/Cc 分支节点做完成判定，故此处仅加载这些分支节点即可与 join 判定口径一致。
        /// </summary>
        private Dictionary<long, List<WfFlowTask>> LoadJoinBranchTasks(long instanceId, WfFlowNode joinNode, WorkflowTopology topo)
        {
            // 入边业务分支节点（Audit/Cc）由拓扑预计算：网关源(4/7/8)不生成任务，无需加载。
            var branchNodeIds = topo.GetJoinInBranches(joinNode.NodeId).Select(n => n.NodeId).ToList();
            if (branchNodeIds.Count == 0) return new Dictionary<long, List<WfFlowTask>>();
            return Context.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == instanceId && branchNodeIds.Contains(t.NodeId))
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
            if (node.SignType == (int)WfSignType.Percent)
            {
                // 比例会签：未跳过的任务中 Done 数 / 总数 ≥ PassRatio（默认 1=全数）即完成；
                // 跳过(Skipped)的任务不参与分母，与或签/会签的"已跳过不阻塞"语义一致。
                var effective = tasks.Where(t => t.Status != (int)WfTaskStatus.Skipped).ToList();
                if (effective.Count == 0) return true; // 整节点被跳过视为完成
                var done = effective.Count(t => t.Status == (int)WfTaskStatus.Done);
                var ratio = node.PassRatio ?? 1m;
                return (decimal)done / effective.Count >= ratio;
            }
            // 或签：已跳过(AutoSkip)的 Skipped 任务同样不阻塞（如审批人为空自动跳过）。
            // 若无未跳过任务（整节点被跳过），视为完成；否则要求未跳过的任务中任一 Done 即可。
            var nonSkipped = tasks.Where(t => t.Status != (int)WfTaskStatus.Skipped).ToList();
            return nonSkipped.Count == 0 || nonSkipped.Any(t => t.Status == (int)WfTaskStatus.Done);
        }

        /// <summary>
        /// 判断并行分组（ParallelGroup&gt;0）成员是否应被激活。条件模型统一为「Edge 属性」：
        /// 并行分叉网关(7) → 该成员的出边 ConditionJson 命中才激活（对标 BPMN）。
        ///
        /// 存在并行分叉网关(7)且有指向该成员的出边：按出边 ConditionJson 判定，命中才激活；
        /// 无条件出边 / 无分叉出边 → 并发激活。
        /// 条件不满足的成员由调用方建 Skipped 留痕（保持现有语义）。
        /// </summary>
        private bool ShouldActivateForkMember(WfFlowNode forkNode, WfFlowNode member, WorkflowTopology topo, Dictionary<string, string> formValues)
        {
            if (forkNode != null)
            {
                var link = topo.GetForkMemberLink(forkNode.NodeId, member.NodeId);
                if (link != null)
                {
                    if (!link.HasCondition) return true; // 无条件出边：并发激活
                    return WfFormValueHelper.EvalParsedCondition(link, formValues);
                }
            }
            return true; // 无分叉出边：并发激活
        }

        #endregion

    }
}
