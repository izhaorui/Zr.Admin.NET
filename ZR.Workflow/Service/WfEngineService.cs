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
    /// </summary>
    [AppService(ServiceType = typeof(IWfEngineService))]
    public class WfEngineService : BaseService<WfFlowInstance>, IWfEngineService
    {
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
            var (def, allNodes, linksBySource, firstNode) = PrepareStartFlow(instance);

            RunInTx(() =>
            {
                var now = DateTime.Now;

                instance.Status = (int)WfInstanceStatus.Approval;
                instance.CurrentNodeId = firstNode?.NodeId;
                instance = InsertReturnEntity(instance) ?? throw new CustomException("发起申请失败");

                AddRecord(instance.InstanceId, null, null, instance.ApplyUser, (int)WfAction.Submit, "发起申请");

                var formValues = ParseFormValues(instance);
                if (firstNode == null)
                {
                    instance.Status = (int)WfInstanceStatus.Approved;
                    Context.Updateable(instance).UpdateColumns(i => new { i.Status }).ExecuteCommand();
                }
                else
                {
                    ArriveNode(instance, firstNode, allNodes, linksBySource, formValues);
                }
            }, "发起申请失败");

            return instance.InstanceId;
        }

        /// <summary>
        /// 通过
        /// </summary>
        public void Approve(long taskId, string opinion, string operatorName)
        {
            var (task, instance) = LoadPendingTaskAndInstance(taskId, operatorName);
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("流程状态异常，无法审批");

            var node = Context.Queryable<WfFlowNode>().First(n => n.NodeId == task.NodeId);
            var allNodes = LoadOrderedNodes(instance.FlowId);
            var linksBySource = LoadNodeLinks(instance.FlowId);

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
                        Update_by = operatorName
                    })
                    .Where(t => t.TaskId == taskId).ExecuteCommand();

                AddRecord(instance.InstanceId, taskId, task.NodeId, operatorName, (int)WfAction.Approve, opinion);

                if (!IsNodeComplete(instance.InstanceId, node)) return;

                NotifyUsers(new[] { instance.ApplyUser }, $"【审批进度】{instance.Title} 的「{node.NodeName}」节点已通过。");

                // 本节点已完成：跳过同节点其余待办，避免或签/并发下重复流转下一节点
                Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask { Status = (int)WfTaskStatus.Skipped })
                    .Where(t => t.InstanceId == instance.InstanceId && t.NodeId == node.NodeId && t.Status == (int)WfTaskStatus.Pending)
                    .ExecuteCommand();

                var formValues = ParseFormValues(instance);
                AdvanceToNext(instance, node, allNodes, linksBySource, formValues);
            }, "审批失败");
        }

        /// <summary>
        /// 驳回
        /// </summary>
        public void Reject(long taskId, string opinion, string operatorName)
        {
            var (task, instance) = LoadPendingTaskAndInstance(taskId, operatorName);

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
                        Update_by = operatorName
                    })
                    .Where(t => t.TaskId == taskId).ExecuteCommand();

                AddRecord(instance.InstanceId, taskId, task.NodeId, operatorName, (int)WfAction.Reject, opinion);

                NotifyUsers(new[] { instance.ApplyUser }, $"【审批驳回】{instance.Title} 被 {operatorName} 驳回{(string.IsNullOrEmpty(opinion) ? "" : "：" + opinion)}");

                instance.Status = (int)WfInstanceStatus.Rejected;
                Context.Updateable(instance).UpdateColumns(i => new { i.Status }).ExecuteCommand();

                Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask { Status = (int)WfTaskStatus.Skipped })
                    .Where(t => t.InstanceId == instance.InstanceId && t.Status == (int)WfTaskStatus.Pending)
                    .ExecuteCommand();
            }, "驳回失败");
        }

        /// <summary>
        /// 重新提交：驳回后由申请人修改内容再次发起，实例回到首节点重新审批。
        /// 历史审批任务与记录保留作为轨迹；仅当实例处于驳回状态时可操作。
        /// </summary>
        public void Resubmit(long instanceId, string formContent, string attachment, string title, string operatorName)
        {
            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == instanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.ApplyUser != operatorName)
                throw new CustomException("仅申请人可重新提交");
            if (instance.Status != (int)WfInstanceStatus.Rejected)
                throw new CustomException("当前状态不可重新提交");

            var (def, allNodes, linksBySource, firstNode) = PrepareStartFlow(instance);

            RunInTx(() =>
            {
                var now = DateTime.Now;
                instance.Status = (int)WfInstanceStatus.Approval;
                instance.CurrentNodeId = firstNode?.NodeId;
                instance.FormContent = formContent;
                instance.Attachment = attachment;
                if (!string.IsNullOrEmpty(title)) instance.Title = title;
                instance.Update_time = now;
                instance.Update_by = operatorName;
                Context.Updateable(instance)
                    .UpdateColumns(i => new { i.Status, i.CurrentNodeId, i.FormContent, i.Attachment, i.Title, i.Update_time, i.Update_by })
                    .ExecuteCommand();

                AddRecord(instanceId, null, null, operatorName, (int)WfAction.Resubmit, "重新提交");

                var formValues = ParseFormValues(instance);
                if (firstNode == null)
                {
                    instance.Status = (int)WfInstanceStatus.Approved;
                    Context.Updateable(instance).UpdateColumns(i => new { i.Status }).ExecuteCommand();
                }
                else
                {
                    ArriveNode(instance, firstNode, allNodes, linksBySource, formValues);
                }
            }, "重新提交失败");
        }

        /// <summary>
        /// 撤回
        /// </summary>
        public void Withdraw(long instanceId, string operatorName)
        {
            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == instanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.ApplyUser != operatorName)
                throw new CustomException("仅申请人可撤回");
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("当前状态不可撤回");

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
                var pendingAssignees = Context.Queryable<WfFlowTask>()
                    .Where(t => t.InstanceId == instanceId && t.Status == (int)WfTaskStatus.Pending)
                    .Select(t => t.Assignee)
                    .ToList();

                Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask { Status = (int)WfTaskStatus.Skipped })
                    .Where(t => t.InstanceId == instanceId && t.Status == (int)WfTaskStatus.Pending)
                    .ExecuteCommand();

                AddRecord(instanceId, null, null, operatorName, (int)WfAction.Withdraw, "撤回申请");

                NotifyUsers(pendingAssignees, $"【审批撤回】{instance.Title} 已被申请人撤回。");

                instance.Status = (int)WfInstanceStatus.Withdrawn;
                Context.Updateable(instance).UpdateColumns(i => new { i.Status }).ExecuteCommand();
            }, "撤回失败");
        }

        /// <summary>
        /// 转办：将当前待办转移给目标用户（节点不变，由目标用户接手）
        /// </summary>
        public void Transfer(long taskId, string targetUser, string opinion, string operatorName)
        {
            if (string.IsNullOrEmpty(targetUser)) throw new CustomException("请选择转办人");
            if (targetUser == operatorName) throw new CustomException("不能转办给自己");

            var (task, instance) = LoadPendingTaskAndInstance(taskId, operatorName);
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("流程状态异常，无法转办");

            var targetUserEntity = Context.Queryable<SysUser>().First(u => u.UserName == targetUser);
            var targetUserId = targetUserEntity?.UserId;
            var targetNickName = targetUserEntity?.NickName;
            RunInTx(() =>
            {
                var now = DateTime.Now;
                Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask
                    {
                        Assignee = targetUser,
                        AssigneeId = targetUserId,
                        AssigneeNickName = targetNickName,
                        Opinion = opinion,
                        Action = (int)WfAction.Transfer,
                        Update_time = now,
                        Update_by = operatorName
                    })
                    .Where(t => t.TaskId == taskId).ExecuteCommand();

                var recordOpinion = "转办给 " + targetUser + (string.IsNullOrEmpty(opinion) ? "" : "：" + opinion);
                AddRecord(instance.InstanceId, taskId, task.NodeId, operatorName, (int)WfAction.Transfer, recordOpinion);

                NotifyUsers(new[] { targetUser }, $"【审批转办】{instance.Title} 由 {operatorName} 转办给您处理。");
            }, "转办失败");
        }

        /// <summary>
        /// 加签：在当前审批节点追加额外审批人，新增待办纳入节点完成判定
        /// </summary>
        public void AddSign(long taskId, List<string> users, string opinion, string operatorName)
        {
            if (users == null || users.Count == 0) throw new CustomException("请选择加签人");

            var (task, instance) = LoadPendingTaskAndInstance(taskId, operatorName);
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("流程状态异常，无法加签");

            var existing = Context.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == task.InstanceId && t.NodeId == task.NodeId)
                .Select(t => t.Assignee)
                .ToList();
            var toAdd = users.Where(u => !string.IsNullOrEmpty(u) && !existing.Contains(u))
                .Distinct().ToList();
            if (toAdd.Count == 0) throw new CustomException("加签人已在该节点审批人中");

            // 加签人前端传 userName，统一解析为 ResolvedApprover（带 UserId）再落库
            var toAddApprovers = ResolveByUserNames(toAdd);

            RunInTx(() =>
            {
                BatchCreateTasks(task.InstanceId, task.NodeId, task.NodeName, toAddApprovers, (int)WfTaskStatus.Pending, operatorName);

                NotifyUsers(toAddApprovers, $"【审批加签】{instance.Title} 由 {operatorName} 邀请您加签审批。");

                var recordOpinion = "加签：" + string.Join(",", toAdd) + (string.IsNullOrEmpty(opinion) ? "" : "：" + opinion);
                AddRecord(instance.InstanceId, taskId, task.NodeId, operatorName, (int)WfAction.AddSign, recordOpinion);
            }, "加签失败");
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
        /// </summary>
        private (WfFlowTask task, WfFlowInstance instance) LoadPendingTaskAndInstance(long taskId, string operatorName)
        {
            var task = Context.Queryable<WfFlowTask>().First(t => t.TaskId == taskId)
                ?? throw new CustomException("审批任务不存在");
            if (task.Status != (int)WfTaskStatus.Pending)
                throw new CustomException("该任务已处理");
            if (task.Assignee != operatorName)
                throw new CustomException("无审批权限");

            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == task.InstanceId)
                ?? throw new CustomException("流程实例不存在");
            return (task, instance);
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
        /// Start / Resubmit 共同的 pre-flight：取定义、校验、查节点全集、取首节点、加载节点连线。
        /// 调用方在事务体内完成各自的 Insert / Update 持久化差异。
        /// </summary>
        private (WfFlowDefinition def, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, WfFlowNode firstNode) PrepareStartFlow(WfFlowInstance instance)
        {
            var def = LoadActivatableDefinition(instance.FlowId);
            if (string.IsNullOrEmpty(instance.FlowName)) instance.FlowName = def.FlowName;
            var allNodes = LoadOrderedNodes(instance.FlowId);
            var linksBySource = LoadNodeLinks(instance.FlowId);
            var firstNode = allNodes.FirstOrDefault(n => IsAuditableNode(n.NodeType));
            return (def, allNodes, linksBySource, firstNode);
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
        private void ArriveNode(WfFlowInstance instance, WfFlowNode node, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<string, string> formValues)
        {
            // 条件网关（菱形，NodeType=4）：本身不生成任务，到达后按出边 ConditionJson 选一路继续。
            // 条件在连线（link）上表达，无需节点级 EvalCondition；无条件出边作为默认分支。
            if (node.NodeType == (int)WfNodeType.Condition)
            {
                var next = ResolveNextNode(node, allNodes, linksBySource, formValues);
                if (next == null)
                {
                    instance.Status = (int)WfInstanceStatus.Approved;
                    Context.Updateable(instance).UpdateColumns(i => new { i.Status, i.CurrentNodeId }).ExecuteCommand();
                }
                else
                {
                    ArriveNode(instance, next, allNodes, linksBySource, formValues);
                }
                return;
            }

            // 排他跳过：条件不满足则顺延到下一节点（递归）；全部不满足则流程直接通过
            if (!EvalCondition(node, formValues))
            {
                var next = ResolveNextNode(node, allNodes, linksBySource, formValues);
                if (next == null)
                {
                    instance.Status = (int)WfInstanceStatus.Approved;
                    Context.Updateable(instance).UpdateColumns(i => new { i.Status, i.CurrentNodeId }).ExecuteCommand();
                }
                else
                {
                    ArriveNode(instance, next, allNodes, linksBySource, formValues);
                }
                return;
            }

            // 并行分支：首次到达该分组时，同时激活组内所有满足条件的节点
            if (node.ParallelGroup > 0)
            {
                var groupNodes = allNodes.Where(n => n.ParallelGroup == node.ParallelGroup).ToList();
                var groupNodeIds = groupNodes.Select(g => g.NodeId).ToList();
                var groupActive = Context.Queryable<WfFlowTask>()
                    .Any(t => t.InstanceId == instance.InstanceId && groupNodeIds.Contains(t.NodeId));
                if (!groupActive)
                {
                    instance.CurrentNodeId = groupNodes.Min(g => g.NodeId);
                    Context.Updateable(instance).UpdateColumns(i => new { i.CurrentNodeId }).ExecuteCommand();

                    foreach (var g in groupNodes)
                    {
                        if (!EvalCondition(g, formValues)) continue; // 分支条件不满足：不生成待办，视为已完成（包容网关）
                        if (g.NodeType == (int)WfNodeType.Cc)
                        {
                            CreateCcTask(instance, g, formValues);
                        }
                        else
                        {
                            var nodeApprovers = ResolveApprovers(g, formValues);
                            BatchCreateTasks(instance.InstanceId, g.NodeId, g.NodeName, nodeApprovers, (int)WfTaskStatus.Pending, instance.ApplyUser);
                            NotifyUsers(nodeApprovers, $"【审批待办】{instance.Title}（{instance.FlowName}），节点「{g.NodeName}」待您审批。");
                        }
                    }

                    // 分组内无任何待办（条件均不满足 / 全为抄送）：视为已完成，直接汇聚
                    var hasPending = Context.Queryable<WfFlowTask>()
                        .Any(t => t.InstanceId == instance.InstanceId && groupNodeIds.Contains(t.NodeId) && t.Status == (int)WfTaskStatus.Pending);
                    if (!hasPending)
                    {
                        // 组内所有分支条件均不满足：视为完成，直接汇聚到后续节点
                        var after = ResolveNextNode(groupNodes.MaxBy(g => g.NodeOrder)!, allNodes, linksBySource, formValues);
                        if (after == null)
                        {
                            instance.Status = (int)WfInstanceStatus.Approved;
                            Context.Updateable(instance).UpdateColumns(i => new { i.Status, i.CurrentNodeId }).ExecuteCommand();
                        }
                        else
                        {
                            ArriveNode(instance, after, allNodes, linksBySource, formValues);
                        }
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

                var next = ResolveNextNode(node, allNodes, linksBySource, formValues);
                if (next == null)
                {
                    instance.Status = (int)WfInstanceStatus.Approved;
                    Context.Updateable(instance).UpdateColumns(i => new { i.Status, i.CurrentNodeId }).ExecuteCommand();
                }
                else
                {
                    ArriveNode(instance, next, allNodes, linksBySource, formValues);
                }
                return;
            }

            // 审批节点
            instance.CurrentNodeId = node.NodeId;
            Context.Updateable(instance).UpdateColumns(i => new { i.CurrentNodeId }).ExecuteCommand();

            var approvers = ResolveApprovers(node, formValues);
            BatchCreateTasks(instance.InstanceId, node.NodeId, node.NodeName, approvers, (int)WfTaskStatus.Pending, instance.ApplyUser);
            NotifyUsers(approvers, $"【审批待办】{instance.Title}（{instance.FlowName}），节点「{node.NodeName}」待您审批。");
        }

        /// <summary>
        /// 节点完成后推进：并行分组内需整组完成才汇聚到后续节点；否则取下一节点。
        ///
        /// 流转图：
        /// <code>
        ///   AdvanceToNext
        ///        │
        ///        ├─ 并行分组未全部完成 → 等待
        ///        │
        ///        ├─ 下一节点为空 → 置通过
        ///        │
        ///        └─ 存在下一节点 → ArriveNode(next)
        /// </code>
        /// </summary>
        private void AdvanceToNext(WfFlowInstance instance, WfFlowNode completedNode, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<string, string> formValues)
        {
            if (completedNode.ParallelGroup > 0)
            {
                var groupNodes = allNodes.Where(n => n.ParallelGroup == completedNode.ParallelGroup).ToList();
                var groupDone = groupNodes.All(g => IsNodeComplete(instance.InstanceId, g));
                if (!groupDone) return; // 等待组内其余分支
                var lastInGroup = groupNodes.MaxBy(g => g.NodeOrder);
                var after = lastInGroup == null ? null : ResolveNextNode(lastInGroup, allNodes, linksBySource, formValues);
                if (after == null)
                {
                    instance.Status = (int)WfInstanceStatus.Approved;
                    Context.Updateable(instance).UpdateColumns(i => new { i.Status, i.CurrentNodeId }).ExecuteCommand();
                }
                else
                {
                    ArriveNode(instance, after, allNodes, linksBySource, formValues);
                }
                return;
            }

            var next = ResolveNextNode(completedNode, allNodes, linksBySource, formValues);
            if (next == null)
            {
                instance.Status = (int)WfInstanceStatus.Approved;
                Context.Updateable(instance).UpdateColumns(i => new { i.Status, i.CurrentNodeId }).ExecuteCommand();
            }
            else
            {
                ArriveNode(instance, next, allNodes, linksBySource, formValues);
            }
        }

        /// <summary>
        /// 解析当前节点的下一节点：**link 为唯一串联事实，NodeOrder 仅作兜底/展示排序**。
        /// 前端应始终为每条边（含直线）生成一条 WfNodeLink（直线 ConditionJson 留空），
        /// 故生产流程下每个非结束节点都有出边，引擎走下方"连线分支"通道。
        /// - 当前节点存在出边（linksBySource 命中 SourceNodeId）：按连线 + ConditionJson 选边（条件命中走该边，
        ///   无任一边命中且有默认分支[ConditionJson 为空]则走默认分支；仍无则视为流程结束）。
        /// - 当前节点无出边（数据缺失兜底）：fallback 到 NodeOrder 升序取下一审批/抄送节点，避免运行态卡死。
        /// 仅筛选审批/抄送节点作为可达目标（开始/结束节点不计入"下一节点"）。
        /// </summary>
        private WfFlowNode ResolveNextNode(WfFlowNode current, List<WfFlowNode> allNodes, Dictionary<long, List<WfNodeLink>> linksBySource, Dictionary<string, string> formValues)
        {
            if (linksBySource.TryGetValue(current.NodeId, out var outLinks) && outLinks.Count > 0)
            {
                WfNodeLink defaultLink = null;
                foreach (var link in outLinks) // 已按 Sort 升序
                {
                    if (string.IsNullOrWhiteSpace(link.ConditionJson))
                    {
                        defaultLink ??= link; // 第一条无条件出边作为默认分支
                        continue;
                    }
                    if (EvalLinkCondition(link.ConditionJson, formValues))
                    {
                        var hit = allNodes.FirstOrDefault(n => n.NodeId == link.TargetNodeId);
                        if (hit != null) return hit;
                    }
                }
                // 无任一条件分支命中：走默认分支（若有）
                if (defaultLink != null)
                {
                    var def = allNodes.FirstOrDefault(n => n.NodeId == defaultLink.TargetNodeId);
                    if (def != null) return def;
                }
                return null; // 有出边但无可达目标：视为流程结束
            }

            // 无出边：fallback 到 NodeOrder 串联（存量通道）
            return GetNextAuditNode(allNodes, current.NodeOrder);
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
            if (node.SignType == (int)WfSignType.And)
                return tasks.All(t => t.Status == (int)WfTaskStatus.Done);
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
        /// =1 角色Id；=2 部门Id；=3 表单字段 key，字段值为逗号分隔的 userId。
        /// 所有分支最终都查 SysUser 得到 UserId，运行态任务/记录直接用 UserId，避免 userName 变更失效。
        /// </summary>
        private List<ResolvedApprover> ResolveApprovers(WfFlowNode node, Dictionary<string, string> formValues)
        {
            var ids = (node.ApproverId ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim())
                .Where(a => !string.IsNullOrEmpty(a))
                .Distinct()
                .ToList();

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
                        var userIds = raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => long.TryParse(s, out var id) && id > 0)
                            .Select(s => long.Parse(s))
                            .Distinct()
                            .ToList();
                        if (userIds.Count == 0) return new List<ResolvedApprover>();
                        users = Context.Queryable<SysUser>().Where(u => userIds.Contains(u.UserId)).Distinct().ToList();
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
        /// 按 userName 列表解析为 ResolvedApprover（加签等以 userName 传入的场景）
        /// </summary>
        private List<ResolvedApprover> ResolveByUserNames(List<string> userNames)
        {
            if (userNames == null || userNames.Count == 0) return new List<ResolvedApprover>();
            var names = userNames.Where(n => !string.IsNullOrEmpty(n)).Select(n => n.Trim()).Distinct().ToList();
            if (names.Count == 0) return new List<ResolvedApprover>();
            return Context.Queryable<SysUser>().Where(u => names.Contains(u.UserName))
                .Select(u => new ResolvedApprover(u.UserId, u.UserName, u.NickName))
                .ToList();
        }

        /// <summary>
        /// 批量创建任务（待办/抄送），替代逐条 ExecuteCommand 以减少数据库往返
        /// </summary>
        private void BatchCreateTasks(long instanceId, long nodeId, string nodeName, List<ResolvedApprover> assignees, int status, string createBy, DateTime? createTime = null)
        {
            if (assignees == null || assignees.Count == 0) return;
            var now = createTime ?? DateTime.Now;
            var tasks = assignees.Select(a => new WfFlowTask
            {
                InstanceId = instanceId,
                NodeId = nodeId,
                NodeName = nodeName,
                Assignee = a.UserName,
                AssigneeId = a.UserId,
                AssigneeNickName = a.NickName,
                Status = status,
                Create_time = now,
                Create_by = createBy
            }).ToList();
            Context.Insertable(tasks).ExecuteCommand();
        }

        /// <summary>
        /// 生成抄送任务并落库抄送记录、推送通知；审批人昵称一并快照。
        /// </summary>
        private void CreateCcTask(WfFlowInstance instance, WfFlowNode node, Dictionary<string, string> formValues)
        {
            var ccList = ResolveApprovers(node, formValues);
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
                AddRecord(instance.InstanceId, null, node.NodeId, c.UserName, (int)WfAction.Cc, "抄送", null, c.UserId, c.NickName);
            }
            NotifyUsers(ccList, $"【审批抄送】{instance.Title}（{instance.FlowName}）抄送知会，请知悉。");
        }

        /// <summary>
        /// 统一创建流程操作记录
        /// </summary>
        private void AddRecord(long instanceId, long? taskId, long? nodeId, string operatorName, int action, string opinion, DateTime? createTime = null, long? operatorId = null, string operatorNick = null)
        {
            // 未显式提供时按登录名反查用户表取 Id/昵称；已提供（如抄送多收件人）则保留快照值
            if (!operatorId.HasValue || string.IsNullOrEmpty(operatorNick))
            {
                var op = Context.Queryable<SysUser>().First(u => u.UserName == operatorName);
                if (!operatorId.HasValue) operatorId = op?.UserId;
                if (string.IsNullOrEmpty(operatorNick)) operatorNick = op?.NickName;
            }
            Context.Insertable(new WfFlowRecord
            {
                InstanceId = instanceId,
                TaskId = taskId,
                NodeId = nodeId,
                Operator = operatorName,
                OperatorId = operatorId,
                OperatorNickName = operatorNick,
                Action = action,
                Opinion = opinion,
                Create_time = createTime ?? DateTime.Now,
                Create_by = operatorName
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
        /// 按用户名批量通知（用于通知发起人、转办/加签目标等以 userName 标识的场景）
        /// </summary>
        private void NotifyUsers(IEnumerable<string> userNames, string content)
        {
            if (userNames == null) return;
            var names = userNames
                .SelectMany(n => (n ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(n => n.Trim())
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToList();
            if (names.Count == 0) return;
            var ids = Context.Queryable<SysUser>().Where(u => names.Contains(u.UserName)).Select(u => u.UserId).ToList();
            foreach (var id in ids) Notify(id, content);
        }

        #endregion
    }
}
