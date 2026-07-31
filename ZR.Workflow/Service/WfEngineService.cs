using ZR.ServiceCore.Services;

namespace ZR.Workflow.Service
{
    /// <summary>
    /// 工作流流转引擎实现（SqlSugar 事务）
    /// </summary>
    [AppService(ServiceType = typeof(IWfEngineService))]
    public class WfEngineService : BaseService<WfFlowInstance>, IWfEngineService
    {
        private readonly ISysUserMsgService _msgService;

        public WfEngineService(ISysUserMsgService msgService)
        {
            _msgService = msgService;
        }

        /// <summary>
        /// 发起申请
        /// </summary>
        public long Start(WfFlowInstance instance)
        {
            var def = Context.Queryable<WfFlowDefinition>().First(d => d.FlowId == instance.FlowId);
            if (def == null) throw new CustomException("流程定义不存在");
            // 标准版：草稿(IsDraft=1)或停用(Status=0)或已删除的定义不可发起，需发布并设为现行
            if (def.IsDraft == 1) throw new CustomException("该流程版本为草稿态，暂不可发起，请先发布");
            if (def.Status != 1) throw new CustomException("该流程版本已停用，暂不可发起");
            if (def.IsDelete == 1) throw new CustomException("该流程定义已删除，不可发起");
            if (string.IsNullOrEmpty(instance.FlowName)) instance.FlowName = def.FlowName;

            var allNodes = Context.Queryable<WfFlowNode>()
                .Where(n => n.FlowId == instance.FlowId)
                .OrderBy(n => n.NodeOrder)
                .ToList();

            var firstNode = allNodes
                .Where(n => n.NodeType == (int)WfNodeType.Audit || n.NodeType == (int)WfNodeType.Cc)
                .FirstOrDefault();

            var result = UseTran(() =>
            {
                var now = DateTime.Now;
                var applyUser = Context.Queryable<SysUser>().First(u => u.UserId == instance.ApplyUserId);
                if (applyUser != null)
                {
                    instance.ApplyNickName = applyUser.NickName;
                }

                instance.Status = (int)WfInstanceStatus.Approval;
                instance.CurrentNodeId = firstNode?.NodeId;
                instance.Create_time = now;
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
                    ArriveNode(instance, firstNode, allNodes, formValues);
                }
            });

            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "发起申请失败", result.ErrorMessage);
            return instance.InstanceId;
        }

        /// <summary>
        /// 通过
        /// </summary>
        public void Approve(long taskId, string opinion, string operatorName)
        {
            var task = Context.Queryable<WfFlowTask>().First(t => t.TaskId == taskId)
                ?? throw new CustomException("审批任务不存在");
            if (task.Status != (int)WfTaskStatus.Pending)
                throw new CustomException("该任务已处理");
            if (task.Assignee != operatorName)
                throw new CustomException("无审批权限");

            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == task.InstanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("流程状态异常，无法审批");

            var node = Context.Queryable<WfFlowNode>().First(n => n.NodeId == task.NodeId);
            var allNodes = Context.Queryable<WfFlowNode>()
                .Where(n => n.FlowId == instance.FlowId)
                .OrderBy(n => n.NodeOrder).ToList();

            var result = UseTran(() =>
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
                AdvanceToNext(instance, node, allNodes, formValues);
            });

            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "审批失败", result.ErrorMessage);
        }

        /// <summary>
        /// 驳回
        /// </summary>
        public void Reject(long taskId, string opinion, string operatorName)
        {
            var task = Context.Queryable<WfFlowTask>().First(t => t.TaskId == taskId)
                ?? throw new CustomException("审批任务不存在");
            if (task.Status != (int)WfTaskStatus.Pending)
                throw new CustomException("该任务已处理");
            if (task.Assignee != operatorName)
                throw new CustomException("无审批权限");

            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == task.InstanceId)
                ?? throw new CustomException("流程实例不存在");

            var result = UseTran(() =>
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
            });

            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "驳回失败", result.ErrorMessage);
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

            var def = Context.Queryable<WfFlowDefinition>().First(d => d.FlowId == instance.FlowId);
            if (def == null) throw new CustomException("流程定义不存在");
            // 标准版：草稿(IsDraft=1)或停用(Status=0)或已删除的定义不可发起，需发布并设为现行
            if (def.IsDraft == 1) throw new CustomException("该流程版本为草稿态，暂不可发起，请先发布");
            if (def.Status != 1) throw new CustomException("该流程版本已停用，暂不可发起");
            if (def.IsDelete == 1) throw new CustomException("该流程定义已删除，不可发起");

            var allNodes = Context.Queryable<WfFlowNode>()
                .Where(n => n.FlowId == instance.FlowId)
                .OrderBy(n => n.NodeOrder).ToList();
            var firstNode = allNodes
                .Where(n => n.NodeType == (int)WfNodeType.Audit || n.NodeType == (int)WfNodeType.Cc)
                .FirstOrDefault();

            var result = UseTran(() =>
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
                    ArriveNode(instance, firstNode, allNodes, formValues);
                }
            });

            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "重新提交失败", result.ErrorMessage);
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
            // 放在事务外做预检，使业务校验异常直接抛出（不会被包裹成通用的“撤回失败”）。
            var currentNodeHandled = Context.Queryable<WfFlowTask>()
                .Any(t => t.InstanceId == instanceId
                         && t.NodeId == instance.CurrentNodeId
                         && t.Status == (int)WfTaskStatus.Done);
            if (currentNodeHandled)
                throw new CustomException("当前节点已审批，无法撤回");

            var result = UseTran(() =>
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
            });

            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "撤回失败", result.ErrorMessage);
        }

        /// <summary>
        /// 转办：将当前待办转移给目标用户（节点不变，由目标用户接手）
        /// </summary>
        public void Transfer(long taskId, string targetUser, string opinion, string operatorName)
        {
            if (string.IsNullOrEmpty(targetUser)) throw new CustomException("请选择转办人");
            if (targetUser == operatorName) throw new CustomException("不能转办给自己");

            var task = Context.Queryable<WfFlowTask>().First(t => t.TaskId == taskId)
                ?? throw new CustomException("审批任务不存在");
            if (task.Status != (int)WfTaskStatus.Pending)
                throw new CustomException("该任务已处理");
            if (task.Assignee != operatorName)
                throw new CustomException("无审批权限");

            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == task.InstanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("流程状态异常，无法转办");

            var targetUserEntity = Context.Queryable<SysUser>().First(u => u.UserName == targetUser);
            var targetUserId = targetUserEntity?.UserId;
            var targetNickName = targetUserEntity?.NickName;
            var result = UseTran(() =>
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
            });

            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "转办失败", result.ErrorMessage);
        }

        /// <summary>
        /// 加签：在当前审批节点追加额外审批人，新增待办纳入节点完成判定
        /// </summary>
        public void AddSign(long taskId, List<string> users, string opinion, string operatorName)
        {
            if (users == null || users.Count == 0) throw new CustomException("请选择加签人");

            var task = Context.Queryable<WfFlowTask>().First(t => t.TaskId == taskId)
                ?? throw new CustomException("审批任务不存在");
            if (task.Status != (int)WfTaskStatus.Pending)
                throw new CustomException("该任务已处理");
            if (task.Assignee != operatorName)
                throw new CustomException("无审批权限");

            var instance = Context.Queryable<WfFlowInstance>().First(i => i.InstanceId == task.InstanceId)
                ?? throw new CustomException("流程实例不存在");
            if (instance.Status != (int)WfInstanceStatus.Approval)
                throw new CustomException("流程状态异常，无法加签");

            var existing = Context.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == task.InstanceId && t.NodeId == task.NodeId)
                .Select(t => t.Assignee)
                .ToList();
            var toAdd = users.Where(u => !string.IsNullOrEmpty(u) && !existing.Contains(u))
                .Distinct().ToList();
            if (toAdd.Count == 0) throw new CustomException("加签人已在该节点审批人中");

            var result = UseTran(() =>
            {
                BatchCreateTasks(task.InstanceId, task.NodeId, task.NodeName, toAdd, (int)WfTaskStatus.Pending, operatorName);

                NotifyUsers(toAdd, $"【审批加签】{instance.Title} 由 {operatorName} 邀请您加签审批。");

                var recordOpinion = "加签：" + string.Join(",", toAdd) + (string.IsNullOrEmpty(opinion) ? "" : "：" + opinion);
                AddRecord(instance.InstanceId, taskId, task.NodeId, operatorName, (int)WfAction.AddSign, recordOpinion);
            });

            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "加签失败", result.ErrorMessage);
        }

        #region 内部流转辅助

        #region 基础辅助

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
        /// 批量通知一组用户名（支持逗号分隔串，自动拆分去重）
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

        /// <summary>
        /// 批量解析用户名 -> (UserId, NickName)，用于任务/记录审批人昵称快照
        /// </summary>
        private Dictionary<string, SysUser> GetUserMap(IEnumerable<string> userNames)
        {
            var names = userNames.Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            if (names.Count == 0) return new Dictionary<string, SysUser>();
            return Context.Queryable<SysUser>().Where(u => names.Contains(u.UserName)).ToList()
                .ToDictionary(u => u.UserName, u => u);
        }

        /// <summary>
        /// 生成抄送任务并落库抄送记录、推送通知；审批人昵称一并快照。
        /// </summary>
        private void CreateCcTask(WfFlowInstance instance, WfFlowNode node)
        {
            var ccList = ResolveApprovers(node);
            var users = GetUserMap(ccList);
            var ccUsers = string.Join(",", ccList);
            var ccNick = string.Join(",", ccList.Select(c => users.TryGetValue(c, out var u) ? u.NickName : c));
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
            // 每个收件人落一条抄送记录并写入各自的 OperatorId，便于按 userId 精确匹配（抄送给我/数据面板），无需反查用户表
            foreach (var c in ccList)
            {
                var u = users.TryGetValue(c, out var su) ? su : null;
                AddRecord(instance.InstanceId, null, node.NodeId, c, (int)WfAction.Cc, "抄送", null, u?.UserId, u?.NickName);
            }
            NotifyUsers(ccList, $"【审批抄送】{instance.Title}（{instance.FlowName}）抄送知会，请知悉。");
        }

        /// <summary>
        /// 批量创建任务（待办/抄送），替代逐条 ExecuteCommand 以减少数据库往返
        /// </summary>
        private void BatchCreateTasks(long instanceId, long nodeId, string nodeName, List<string> assignees, int status, string createBy, DateTime? createTime = null)
        {
            if (assignees == null || assignees.Count == 0) return;
            var now = createTime ?? DateTime.Now;
            var userMap = GetUserMap(assignees);
            var tasks = assignees.Select(a => new WfFlowTask
            {
                InstanceId = instanceId,
                NodeId = nodeId,
                NodeName = nodeName,
                Assignee = a,
                AssigneeId = userMap.TryGetValue(a, out var u) ? u.UserId : (long?)null,
                AssigneeNickName = u?.NickName ?? a,
                Status = status,
                Create_time = now,
                Create_by = createBy
            }).ToList();
            Context.Insertable(tasks).ExecuteCommand();
        }

        #endregion

        /// <summary>
        /// 解析节点审批人列表。
        /// ApproverType=0 直接返回用户名；=1 按角色Id查该角色下所有用户；=2 按部门Id取该部门下所有用户。
        /// </summary>
        private List<string> ResolveApprovers(WfFlowNode node)
        {
            var ids = (node.ApproverId ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim())
                .Where(a => !string.IsNullOrEmpty(a))
                .Distinct()
                .ToList();

            switch (node.ApproverType)
            {
                case (int)WfApproverType.Role:
                    {
                        var roleIds = ids.Select(s => long.TryParse(s, out var v) ? v : (long?)null).Where(v => v.HasValue).Select(v => v.Value).ToList();
                        if (roleIds.Count == 0) return new List<string>();
                        return Context.Queryable<SysUser>()
                            .InnerJoin<SysUserRole>((u, ur) => u.UserId == ur.UserId)
                            .Where((u, ur) => roleIds.Contains(ur.RoleId))
                            .Select(u => u.UserName)
                            .Distinct()
                            .ToList();
                    }
                case (int)WfApproverType.Dept:
                    {
                        var deptIds = ids.Select(s => long.TryParse(s, out var v) ? v : (long?)null).Where(v => v.HasValue).Select(v => v.Value).ToList();
                        if (deptIds.Count == 0) return new List<string>();
                        return Context.Queryable<SysUser>()
                            .Where(u => deptIds.Contains(u.DeptId) && u.Status == 0)
                            .Select(u => u.UserName)
                            .Distinct()
                            .ToList();
                    }
                default: // 指定用户
                    return ids;
            }
        }

        /// <summary>
        /// 到达某节点：按条件排他跳过；并行分组则同时激活组内节点(fork)；
        /// 审批节点生成待办并等待；抄送节点生成抄送记录并继续；结束则通过。
        /// </summary>
        private void ArriveNode(WfFlowInstance instance, WfFlowNode node, List<WfFlowNode> allNodes, Dictionary<string, string> formValues)
        {
            // 排他跳过：条件不满足则顺延到下一节点（递归）；全部不满足则流程直接通过
            if (!EvalCondition(node, formValues))
            {
                var next = GetNextAuditNode(allNodes, node.NodeOrder);
                if (next == null)
                {
                    instance.Status = (int)WfInstanceStatus.Approved;
                    Context.Updateable(instance).UpdateColumns(i => new { i.Status, i.CurrentNodeId }).ExecuteCommand();
                }
                else
                {
                    ArriveNode(instance, next, allNodes, formValues);
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
                        if (!EvalCondition(g, formValues)) continue; // 分支条件不满足：不生成待办，视为已完成(包容网关)
                        if (g.NodeType == (int)WfNodeType.Cc)
                        {
                            CreateCcTask(instance, g);
                        }
                        else
                        {
                            var nodeApprovers = ResolveApprovers(g);
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
                        var after = GetNextAuditNode(allNodes, groupNodes.Max(g => g.NodeOrder));
                        if (after == null)
                        {
                            instance.Status = (int)WfInstanceStatus.Approved;
                            Context.Updateable(instance).UpdateColumns(i => new { i.Status, i.CurrentNodeId }).ExecuteCommand();
                        }
                        else
                        {
                            ArriveNode(instance, after, allNodes, formValues);
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
                CreateCcTask(instance, node);

                var next = GetNextAuditNode(allNodes, node.NodeOrder);
                if (next == null)
                {
                    instance.Status = (int)WfInstanceStatus.Approved;
                    Context.Updateable(instance).UpdateColumns(i => new { i.Status, i.CurrentNodeId }).ExecuteCommand();
                }
                else
                {
                    ArriveNode(instance, next, allNodes, formValues);
                }
                return;
            }

            // 审批节点
            instance.CurrentNodeId = node.NodeId;
            Context.Updateable(instance).UpdateColumns(i => new { i.CurrentNodeId }).ExecuteCommand();

            var approvers = ResolveApprovers(node);
            BatchCreateTasks(instance.InstanceId, node.NodeId, node.NodeName, approvers, (int)WfTaskStatus.Pending, instance.ApplyUser);
            NotifyUsers(approvers, $"【审批待办】{instance.Title}（{instance.FlowName}），节点「{node.NodeName}」待您审批。");
        }

        /// <summary>
        /// 节点完成后推进：并行分组内需整组完成才汇聚到后续节点；否则取下一节点。
        /// </summary>
        private void AdvanceToNext(WfFlowInstance instance, WfFlowNode completedNode, List<WfFlowNode> allNodes, Dictionary<string, string> formValues)
        {
            if (completedNode.ParallelGroup > 0)
            {
                var groupNodes = allNodes.Where(n => n.ParallelGroup == completedNode.ParallelGroup).ToList();
                var groupDone = groupNodes.All(g => IsNodeComplete(instance.InstanceId, g));
                if (!groupDone) return; // 等待组内其余分支
                var after = GetNextAuditNode(allNodes, groupNodes.Max(g => g.NodeOrder));
                if (after == null)
                {
                    instance.Status = (int)WfInstanceStatus.Approved;
                    Context.Updateable(instance).UpdateColumns(i => new { i.Status, i.CurrentNodeId }).ExecuteCommand();
                }
                else
                {
                    ArriveNode(instance, after, allNodes, formValues);
                }
                return;
            }

            var next = GetNextAuditNode(allNodes, completedNode.NodeOrder);
            if (next == null)
            {
                instance.Status = (int)WfInstanceStatus.Approved;
                Context.Updateable(instance).UpdateColumns(i => new { i.Status, i.CurrentNodeId }).ExecuteCommand();
            }
            else
            {
                ArriveNode(instance, next, allNodes, formValues);
            }
        }

        /// <summary>
        /// 将 FormContent(JSON) 解析为 字段->值 字典（值均为字符串）。解析失败返回空字典。
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
            catch (Exception ex) { /* JSON 解析失败（格式错误或类型不匹配），视为无条件；排查时可记录 ex.Message */ }
            return dict;
        }

        /// <summary>
        /// 评估节点条件：字段/运算符/值三者齐全才生效，任一缺失视为无条件（返回 true）。
        /// 数值可解析时按数值比较，否则按字符串比较；字段缺失或无值视为条件不满足（保守跳过）。
        /// </summary>
        private bool EvalCondition(WfFlowNode node, Dictionary<string, string> formValues)
        {
            if (string.IsNullOrWhiteSpace(node.ConditionField)) return true;
            if (node.ConditionOp == (int)WfConditionOp.None) return true;
            if (string.IsNullOrWhiteSpace(node.ConditionValue)) return true;
            if (!formValues.TryGetValue(node.ConditionField, out var raw) || string.IsNullOrWhiteSpace(raw))
                return false;

            var target = node.ConditionValue;
            var leftOk = double.TryParse(raw, out var left);
            var rightOk = double.TryParse(target, out var right);
            var bothNum = leftOk && rightOk;
            switch ((WfConditionOp)node.ConditionOp)
            {
                case WfConditionOp.Lt: return bothNum ? left < right : string.CompareOrdinal(raw, target) < 0;
                case WfConditionOp.Le: return bothNum ? left <= right : string.CompareOrdinal(raw, target) <= 0;
                case WfConditionOp.Gt: return bothNum ? left > right : string.CompareOrdinal(raw, target) > 0;
                case WfConditionOp.Ge: return bothNum ? left >= right : string.CompareOrdinal(raw, target) >= 0;
                case WfConditionOp.Eq: return string.Equals(raw, target, StringComparison.OrdinalIgnoreCase);
                case WfConditionOp.Ne: return !string.Equals(raw, target, StringComparison.OrdinalIgnoreCase);
                default: return true;
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
        /// 取下一审批/抄送节点（跳过开始/结束）
        /// </summary>
        private WfFlowNode GetNextAuditNode(List<WfFlowNode> allNodes, int currentOrder)
        {
            return allNodes
                .Where(n => n.NodeOrder > currentOrder &&
                            (n.NodeType == (int)WfNodeType.Audit || n.NodeType == (int)WfNodeType.Cc))
                .OrderBy(n => n.NodeOrder)
                .FirstOrDefault();
        }

        #endregion
    }
}
