using ZR.Workflow.Model;
using ZR.Workflow.Model.Dto;
using ZR.Workflow.Service.IService;

namespace ZR.Workflow.Service
{
    /// <summary>
    /// 工作流流转引擎实现（SqlSugar 事务）
    /// </summary>
    [AppService(ServiceType = typeof(IWfEngineService))]
    public class WfEngineService : BaseService<WfFlowInstance>, IWfEngineService
    {
        /// <summary>
        /// 发起申请
        /// </summary>
        public long Start(WfFlowInstance instance)
        {
            var def = Context.Queryable<WfFlowDefinition>().First(d => d.FlowId == instance.FlowId);
            if (def == null) throw new CustomException("流程定义不存在");
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
                instance.Status = (int)WfInstanceStatus.Approval;
                instance.CurrentNodeId = firstNode?.NodeId;
                instance.Create_time = DateTime.Now;
                instance = InsertReturnEntity(instance) ?? throw new CustomException("发起申请失败");

                Context.Insertable(new WfFlowRecord
                {
                    InstanceId = instance.InstanceId,
                    Operator = instance.ApplyUser,
                    Action = (int)WfAction.Submit,
                    Opinion = "发起申请",
                    Create_time = DateTime.Now,
                    Create_by = instance.ApplyUser
                }).ExecuteCommand();

                if (firstNode == null)
                {
                    instance.Status = (int)WfInstanceStatus.Approved;
                    Context.Updateable(instance).UpdateColumns(i => new { i.Status }).ExecuteCommand();
                }
                else
                {
                    ArriveNode(instance, firstNode, allNodes);
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
                Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask
                    {
                        Status = (int)WfTaskStatus.Done,
                        Action = (int)WfAction.Approve,
                        Opinion = opinion,
                        HandleTime = DateTime.Now,
                        Update_time = DateTime.Now,
                        Update_by = operatorName
                    })
                    .Where(t => t.TaskId == taskId).ExecuteCommand();

                Context.Insertable(new WfFlowRecord
                {
                    TaskId = taskId,
                    InstanceId = instance.InstanceId,
                    NodeId = task.NodeId,
                    Operator = operatorName,
                    Action = (int)WfAction.Approve,
                    Opinion = opinion,
                    Create_time = DateTime.Now,
                    Create_by = operatorName
                }).ExecuteCommand();

                if (!IsNodeComplete(instance.InstanceId, node)) return;

                // 本节点已完成：跳过同节点其余待办，避免或签/并发下重复流转下一节点
                Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask { Status = (int)WfTaskStatus.Skipped })
                    .Where(t => t.InstanceId == instance.InstanceId && t.NodeId == node.NodeId && t.Status == (int)WfTaskStatus.Pending)
                    .ExecuteCommand();

                var next = GetNextAuditNode(allNodes, node.NodeOrder);
                if (next == null)
                {
                    instance.Status = (int)WfInstanceStatus.Approved;
                    Context.Updateable(instance).UpdateColumns(i => new { i.Status, i.CurrentNodeId }).ExecuteCommand();
                }
                else
                {
                    ArriveNode(instance, next, allNodes);
                }
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
                Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask
                    {
                        Status = (int)WfTaskStatus.Done,
                        Action = (int)WfAction.Reject,
                        Opinion = opinion,
                        HandleTime = DateTime.Now,
                        Update_time = DateTime.Now,
                        Update_by = operatorName
                    })
                    .Where(t => t.TaskId == taskId).ExecuteCommand();

                Context.Insertable(new WfFlowRecord
                {
                    TaskId = taskId,
                    InstanceId = instance.InstanceId,
                    NodeId = task.NodeId,
                    Operator = operatorName,
                    Action = (int)WfAction.Reject,
                    Opinion = opinion,
                    Create_time = DateTime.Now,
                    Create_by = operatorName
                }).ExecuteCommand();

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
                Context.Updateable<WfFlowTask>()
                    .SetColumns(t => new WfFlowTask { Status = (int)WfTaskStatus.Skipped })
                    .Where(t => t.InstanceId == instanceId && t.Status == (int)WfTaskStatus.Pending)
                    .ExecuteCommand();

                Context.Insertable(new WfFlowRecord
                {
                    InstanceId = instanceId,
                    Operator = operatorName,
                    Action = (int)WfAction.Withdraw,
                    Opinion = "撤回申请",
                    Create_time = DateTime.Now,
                    Create_by = operatorName
                }).ExecuteCommand();

                instance.Status = (int)WfInstanceStatus.Withdrawn;
                Context.Updateable(instance).UpdateColumns(i => new { i.Status }).ExecuteCommand();
            });

            if (!result.IsSuccess)
                throw new CustomException(ResultCode.CUSTOM_ERROR, "撤回失败", result.ErrorMessage);
        }

        #region 内部流转辅助

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
                        var roleIds = ids.Where(s => long.TryParse(s, out _)).Select(long.Parse).ToList();
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
                        var deptIds = ids.Where(s => long.TryParse(s, out _)).Select(long.Parse).ToList();
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
        /// 到达某节点：审批节点生成待办并等待；抄送节点跳过并继续；结束则通过
        /// </summary>
        private void ArriveNode(WfFlowInstance instance, WfFlowNode node, List<WfFlowNode> allNodes)
        {
            if (node.NodeType == (int)WfNodeType.Cc)
            {
                var ccUsers = string.Join(",", ResolveApprovers(node));
                Context.Insertable(new WfFlowTask
                {
                    InstanceId = instance.InstanceId,
                    NodeId = node.NodeId,
                    NodeName = node.NodeName,
                    Assignee = ccUsers,
                    Status = (int)WfTaskStatus.Skipped,
                    Create_time = DateTime.Now,
                    Create_by = instance.ApplyUser
                }).ExecuteCommand();

                Context.Insertable(new WfFlowRecord
                {
                    InstanceId = instance.InstanceId,
                    NodeId = node.NodeId,
                    Operator = ccUsers,
                    Action = (int)WfAction.Submit,
                    Opinion = "抄送",
                    Create_time = DateTime.Now,
                    Create_by = instance.ApplyUser
                }).ExecuteCommand();

                var next = GetNextAuditNode(allNodes, node.NodeOrder);
                if (next == null)
                {
                    instance.Status = (int)WfInstanceStatus.Approved;
                    Context.Updateable(instance).UpdateColumns(i => new { i.Status, i.CurrentNodeId }).ExecuteCommand();
                }
                else
                {
                    ArriveNode(instance, next, allNodes);
                }
                return;
            }

            // 审批节点
            instance.CurrentNodeId = node.NodeId;
            Context.Updateable(instance).UpdateColumns(i => new { i.CurrentNodeId }).ExecuteCommand();

            var approvers = ResolveApprovers(node);

            foreach (var approver in approvers)
            {
                Context.Insertable(new WfFlowTask
                {
                    InstanceId = instance.InstanceId,
                    NodeId = node.NodeId,
                    NodeName = node.NodeName,
                    Assignee = approver,
                    Status = (int)WfTaskStatus.Pending,
                    Create_time = DateTime.Now,
                    Create_by = instance.ApplyUser
                }).ExecuteCommand();
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
