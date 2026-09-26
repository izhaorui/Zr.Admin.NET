using ZR.Workflow.Service.Engine;

namespace ZR.Workflow.Service
{
    public partial class WfEngineService
    {
        #region 审批人解析与通知

        /// <summary>
        /// 解析后的审批人（直接用 UserId 落库，不依赖 userName 反查）。
        /// </summary>
        private sealed record ResolvedApprover(long UserId, string UserName, string NickName);

        /// <summary>
        /// 有效用户查询起点：未删除（DelFlag=Normal）且未停用（Status==0）。
        /// 引擎内所有取用户处统一复用，避免审批人解析/转办等场景选到已删除或停用的用户导致流程卡死。
        /// </summary>
        private ISugarQueryable<SysUser> ActiveUsers()
            => Context.Queryable<SysUser>().Where(u => u.DelFlag == DelFlagEnum.Normal && u.Status == 0);

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

        /// <summary>表单字段动态审批人：ApproverId 为表单字段 key，字段值为逗号分隔的 userId（兼容 "userId:userName" 格式，取冒号前的数字部分）。</summary>
        private sealed class FormFieldApproverResolver : ApproverResolverBase
        {
            public FormFieldApproverResolver(WfEngineService engine) : base(engine) { }

            public override List<SysUser> Resolve(WfFlowNode node, Dictionary<string, string> formValues, long? applyUserId)
            {
                var key = node.ApproverId ?? "";
                if (string.IsNullOrWhiteSpace(key) || formValues == null || !formValues.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                    return new List<SysUser>();
                var userIds = raw.SplitByComma()
                    .Select(s => {
                        // 兼容 "userId:userName" 格式：取冒号前的纯数字部分
                        var idx = s.IndexOf(':');
                        var numStr = idx > 0 ? s.Substring(0, idx) : s;
                        return long.TryParse(numStr, out var id) && id > 0 ? id : 0;
                    })
                    .Where(id => id > 0)
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
                    .Where(d => deptIds.Contains(d.DeptId) && d.DelFlag == DelFlagEnum.Normal)
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
            // 幂等保护：同一节点已存在任何任务（Pending/Done/Skipped）则不再建第二条 Skipped 留痕。
            // 重复来源：同一多入边/并行分叉节点可能同时被两条路径处理——
            //   ① SkipBranchChain 级联（"上游条件不满足，分支自动跳过"）
            //   ② ArriveNode 实际到达（"审批人为空，节点自动跳过" / 并行分组 fork）
            // 两条路径在同一事务内先后执行，任一路径先建任务后，另一路径必须跳过，否则同一节点出现两条不同原因的 AutoSkip 记录。
            var existed = Context.Queryable<WfFlowTask>().Any(t => t.InstanceId == instance.InstanceId && t.NodeId == node.NodeId);
            if (existed) return;
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
        private void SkipRejectedBranches(WfFlowInstance instance, WfFlowNode node, WorkflowTopology topo, Dictionary<string, string> formValues, int depth)
        {
            var outLinks = topo.GetOutLinks(node.NodeId);
            if (outLinks.Count == 0) return;
            foreach (var link in outLinks)
            {
                // 仅处理"条件不满足"的出边（无条件默认分支视为满足，已被 ResolveNextNode 顺延走到，不在此标）
                if (!link.HasCondition || WfFormValueHelper.EvalParsedCondition(link, formValues)) continue;
                var target = topo.GetNode(link.TargetNodeId);
                if (target == null) continue;
                SkipBranchChain(instance, target, topo, formValues, new HashSet<long>(), depth);
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
        private void SkipBranchChain(WfFlowInstance instance, WfFlowNode branchStart, WorkflowTopology topo, Dictionary<string, string> formValues, HashSet<long> visited, int depth)
        {
            if (branchStart == null || visited.Contains(branchStart.NodeId)) return;
            visited.Add(branchStart.NodeId);

            // 汇聚网关：激活它（等其它分支），不跨网关继续
            if (branchStart.NodeType == (int)WfNodeType.ParallelJoin)
            {
                ArriveNode(instance, branchStart, topo, formValues, depth: depth);
                return;
            }
            // 分叉网关 / 终点：不进入并行子图，停止
            if (branchStart.NodeType == (int)WfNodeType.ParallelFork) return;

            // 真实业务节点（Audit/Cc）：建 Skipped 留痕（CreateAutoSkipTask 内部已做"已存在任务则跳过"的幂等保护），继续沿下游链级联
            if (branchStart.NodeType == (int)WfNodeType.Audit || branchStart.NodeType == (int)WfNodeType.Cc)
            {
                CreateAutoSkipTask(instance, branchStart, "上游条件不满足，分支自动跳过");
            }

            // 向下游继续级联（终点无出边自然停止）
            foreach (var l in topo.GetOutLinks(branchStart.NodeId))
            {
                var next = topo.GetNode(l.TargetNodeId);
                if (next != null) SkipBranchChain(instance, next, topo, formValues, visited, depth);
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
                var tenantId = BackgroundDbHelper.CaptureTenantId();
                _ = GenerateRecordSummaryAsync(record.RecordId, instanceId, nodeName, opinion, tenantId, op.UserId, op.UserName);
            }
        }

        /// <summary>
        /// 异步生成审批记录 AI 摘要并写回（fire-and-forget，异常吞掉不影响主流程）。
        /// 注意：本方法在 <see cref="RunInTx"/> 事务内被调用，后台任务不能复用请求级 scoped <c>Context</c>——
        /// 复用会与主流程共用同一 MySqlConnection 并发执行 SQL，抛 "This MySqlConnection is already in use"；
        /// 且请求结束后 scoped 连接释放、租户上下文丢失。参照 FillAttachmentParsedAsync 的做法：
        /// 请求上下文内先捕获租户 Id，任务内用 CopyNew() 独立连接并按租户路由。
        /// </summary>
        private async Task GenerateRecordSummaryAsync(
            long recordId, long instanceId, string nodeName, string opinion,
            string tenantId, long operatorId, string operatorName)
        {
            using var actorScope = new global::Infrastructure.AI.AiCallActorScope(tenantId, operatorId, operatorName);
            try
            {
                // 后台线程不能复用请求级 Context：调用方（AddRecord）此时通常仍在 RunInTx 事务内、
                // 请求线程持有同一连接的打开状态，并发使用同一连接会抛
                // "已有打开的与此 Connection 相关联的 DataReader，必须首先将它关闭"，
                // 并把外层事务提交一起带崩（表现为 500）。
                // 与 WfFlowInstanceService.FillAttachmentParsedAsync 一致：统一用 BackgroundDbHelper
                // 建独立连接（CopyNew）并按租户路由到对应租户库。
                var db = BackgroundDbHelper.CreateBackgroundDb(tenantId);

                var inst = await db.Queryable<WfFlowInstance>()
                    .Where(i => i.InstanceId == instanceId)
                    .FirstAsync();
                if (inst == null) return;
                var formItems = await db.Queryable<WfFlowDefinition>()
                    .Where(d => d.FlowId == inst.FlowId)
                    .Select(d => d.FormItems)
                    .FirstAsync();
                // 表单字段技术名翻译为中文label，避免 input_1 等暴露给 AI/用户
                var formText = ZR.Workflow.Helper.WfFormTextHelper.TranslateToText(inst.FormContent, formItems) ?? inst.FormContent;
                var summary = await _aiService.SummarizeApprovalAsync(string.Empty, nodeName, opinion, formText);
                if (!string.IsNullOrWhiteSpace(summary?.Summary))
                {
                    await db.Updateable<WfFlowRecord>()
                        .SetColumns(r => r.Summary == summary.Summary)
                        .Where(r => r.RecordId == recordId)
                        .ExecuteCommandAsync();
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
        /// 站内信通知：落库并 SignalR 实时推送（异常不影响主流程）。
        /// 行动类通知（待办/催办/转办/委托/加签/改派等需用户处理的消息）额外按 Workflow:Notify 开关
        /// 分发邮件/短信，外部发送走后台线程，不阻塞流程事务。
        /// </summary>
        private void Notify(long userId, string content)
        {
            try { _msgService.AddSysUserMsg(userId, content, UserMsgType.WORKFLOW); }
            catch { /* 通知失败不影响流程主逻辑 */ }

            try
            {
                if (IsActionRequiredNotify(content)) DispatchExternalNotify(userId, content);
            }
            catch { /* 外部通知失败不影响流程主逻辑 */ }
        }

        /// <summary>行动类通知前缀：此类消息需要用户登录系统处理，值得发邮件/短信。新增通知点时按需补充。</summary>
        private static readonly string[] ActionRequiredPrefixes =
            { "【审批待办】", "【待审批】", "【审批催办】", "【审批转办】", "【审批委托】", "【审批加签】", "【审批改派】" };

        private static bool IsActionRequiredNotify(string content)
            => !string.IsNullOrEmpty(content) && ActionRequiredPrefixes.Any(p => content.StartsWith(p));

        /// <summary>
        /// 外部通知分发：主线程查一次用户邮箱/手机号（与引擎同一 SqlSugar 上下文），
        /// 随后在后台线程做 SMTP/HTTP 外部 IO，避免拉长流程事务。开关未开或用户未配置联系方式时静默跳过。
        /// </summary>
        private void DispatchExternalNotify(long userId, string content)
        {
            var notify = App.OptionsSetting?.Workflow?.Notify;
            if (notify == null || (!notify.EmailEnabled && !notify.SmsEnabled)) return;

            var contact = Context.Queryable<SysUser>()
                .Where(u => u.UserId == userId)
                .Select(u => new { u.Email, u.Phonenumber })
                .First();
            if (contact == null) return;

            if (notify.EmailEnabled && !string.IsNullOrWhiteSpace(contact.Email))
                Task.Run(() => SendNotifyMail(notify, contact.Email, content));
            if (notify.SmsEnabled && !string.IsNullOrWhiteSpace(contact.Phonenumber))
                Task.Run(() => SendNotifySms(userId, contact.Phonenumber, content));
        }

        private void SendNotifyMail(WorkflowNotifyOptions notify, string toEmail, string content)
        {
            try
            {
                var mailList = App.OptionsSetting?.MailOptions;
                if (mailList == null || mailList.Count == 0) return;
                var mailOptions = mailList.FirstOrDefault(m => m.FromName == notify.MailFromName) ?? mailList[0];
                if (string.IsNullOrWhiteSpace(mailOptions.FromEmail)) return;

                var subject = content.Length > 60 ? content[..60] : content;
                var result = new MailHelper(mailOptions).SendMail(toEmail, subject, content);
                if (result == "fail") logger.Warn($"工作流邮件通知发送失败 To={ZR.Infrastructure.Helper.MaskUtil.MaskEmail(toEmail)} Content={content}");
                else logger.Info($"工作流邮件通知已发送 To={ZR.Infrastructure.Helper.MaskUtil.MaskEmail(toEmail)} Content={content}");
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"工作流邮件通知异常 Content={content}");
            }
        }

        private void SendNotifySms(long userId, string phone, string content)
        {
            try
            {
                var smsOptions = App.OptionsSetting?.SmsOptions;
                string templateCode = null;
                smsOptions?.Templates?.TryGetValue("workflow", out templateCode);
                if (string.IsNullOrWhiteSpace(templateCode))
                {
                    logger.Warn($"工作流短信通知跳过：未配置 SmsOptions:Templates:workflow 模板编号");
                    return;
                }

                var result = _smsSender.Send(new SmsMessage
                {
                    PhoneNum = phone,
                    TemplateCode = templateCode,
                    TemplateParams = new Dictionary<string, string> { { "content", content } },
                    SendType = 6
                });
                if (result.Success) logger.Info($"工作流短信通知已发送 userId={userId} Simulated={result.Simulated}");
                else logger.Warn($"工作流短信通知发送失败 userId={userId} ErrorCode={result.ErrorCode} ErrorMsg={result.ErrorMsg}");
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"工作流短信通知异常 userId={userId}");
            }
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
