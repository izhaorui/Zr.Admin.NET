using Newtonsoft.Json;
using SqlSugar;
using ZR.Workflow.Enum;
using ZR.Workflow.Model.Dto;

namespace ZR.Workflow.Model.Topology
{
    /// <summary>
    /// 工作流静态拓扑构建器。纯函数逻辑（读库 → 建不可变索引），引擎每次操作现构建一次。
    ///
    /// 职责：
    /// - 一次性查节点 + 连线两张表，构建 <see cref="WorkflowTopology"/> 的 O(1) 索引
    ///   （nodeById / nextOf / prevOf / nodeKind / parallelRegions / forkByGroup）。
    /// - 对每条带条件的出边做"静态配置预解析与校验"（JSON 反序列化 + field/op/value 缺失检查），
    ///   把 JSON 解析移出引擎热路径；配置错误记录到 <see cref="ResolvedOutLink.ConditionError"/>，
    ///   由引擎运行时在事务内抛出。
    /// </summary>
    public static class WfWorkflowTopologyBuilder
    {
        /// <summary>
        /// 读取某 FlowId 的节点与连线并构建不可变拓扑。每次操作调用一次，不做缓存。
        /// </summary>
        public static WorkflowTopology Build(ISqlSugarClient db, long flowId)
        {
            var nodes = db.Queryable<WfFlowNode>()
                .Where(n => n.FlowId == flowId)
                .OrderBy(n => n.NodeOrder)
                .ToList();

            var links = db.Queryable<WfNodeLink>()
                .Where(l => l.FlowId == flowId)
                .OrderBy(l => l.Sort)
                .ToList();

            return BuildTopology(nodes, links);
        }

        /// <summary>
        /// 由已加载的节点 + 连线列表构建不可变拓扑（测试可直接构造数据调用，无需连库）。
        /// </summary>
        public static WorkflowTopology BuildTopology(IEnumerable<WfFlowNode> nodes, IEnumerable<WfNodeLink> links)
        {
            var nodeList = nodes?.OrderBy(n => n.NodeOrder).ToList() ?? new List<WfFlowNode>();
            var linkList = links?.ToList() ?? new List<WfNodeLink>();

            var nodeById = new Dictionary<long, WfFlowNode>(nodeList.Count);
            var nodeKind = new Dictionary<long, WfNodeType>(nodeList.Count);
            foreach (var n in nodeList)
            {
                nodeById[n.NodeId] = n;
                nodeKind[n.NodeId] = (WfNodeType)n.NodeType;
            }

            // nextOf：源节点 → 预解析出边（保留 Sort 升序）
            var nextOf = new Dictionary<long, IReadOnlyList<ResolvedOutLink>>();
            foreach (var g in linkList.GroupBy(l => l.SourceNodeId))
            {
                var outLinks = g
                    .OrderBy(l => l.Sort)
                    .Select(l => ResolveOutLink(l))
                    .ToList();
                nextOf[g.Key] = outLinks;
            }

            // prevOf：目标节点 → 入边源节点 Id（去重）
            var prevOf = new Dictionary<long, IReadOnlyList<long>>();
            foreach (var g in linkList.GroupBy(l => l.TargetNodeId))
            {
                prevOf[g.Key] = g.Select(l => l.SourceNodeId).Distinct().ToList();
            }

            // parallelRegions / forkByGroup：并行分组索引
            var parallelRegions = new Dictionary<int, IReadOnlyList<WfFlowNode>>();
            var forkByGroup = new Dictionary<int, WfFlowNode>();
            foreach (var g in nodeList.Where(n => n.ParallelGroup > 0).GroupBy(n => n.ParallelGroup))
            {
                var members = g.OrderBy(n => n.NodeOrder).ToList();
                parallelRegions[g.Key] = members;
                forkByGroup[g.Key] = members.FirstOrDefault(n => n.NodeType == (int)WfNodeType.ParallelFork);
            }

            // joinInBranches：ParallelJoin(8) → 入边业务分支节点（Audit/Cc，去重）。
            // 网关源(4/7/8)不生成任务、由流转自然跳过，视为瞬时完成不计入 join 完成判定。
            var joinInBranches = new Dictionary<long, IReadOnlyList<WfFlowNode>>();
            foreach (var kv in prevOf)
            {
                var target = nodeById.TryGetValue(kv.Key, out var t) ? t : null;
                if (target == null || target.NodeType != (int)WfNodeType.ParallelJoin) continue;
                var branches = kv.Value
                    .Select(id => nodeById.TryGetValue(id, out var n) ? n : null)
                    .Where(n => n != null && (n.NodeType == (int)WfNodeType.Audit || n.NodeType == (int)WfNodeType.Cc))
                    .Distinct()
                    .ToList();
                joinInBranches[kv.Key] = branches;
            }

            // forkMemberLinks：ParallelFork(7) → 出边 target → ResolvedOutLink 映射
            // （供并行分组 fork 判断"分叉网关 → 成员"的条件命中，O(1) 取边）
            var forkMemberLinks = new Dictionary<long, IReadOnlyDictionary<long, ResolvedOutLink>>();
            foreach (var kv in nextOf)
            {
                var src = nodeById.TryGetValue(kv.Key, out var s) ? s : null;
                if (src == null || src.NodeType != (int)WfNodeType.ParallelFork) continue;
                var map = new Dictionary<long, ResolvedOutLink>(kv.Value.Count);
                foreach (var link in kv.Value) map[link.TargetNodeId] = link;
                forkMemberLinks[kv.Key] = map;
            }

            // groupExits：ParallelGroup → 组内成员指向组外节点的出边目标（去重）。
            // link 为唯一串联事实，出口 = 组内成员连到组外目标，绝不依赖 NodeOrder。
            var groupExits = new Dictionary<int, IReadOnlyList<WfFlowNode>>();
            foreach (var kv in parallelRegions)
            {
                var groupIds = kv.Value.Select(n => n.NodeId).ToHashSet();
                var exits = new List<WfFlowNode>();
                foreach (var g in kv.Value)
                {
                    if (!nextOf.TryGetValue(g.NodeId, out var outLinks)) continue;
                    foreach (var link in outLinks)
                    {
                        if (groupIds.Contains(link.TargetNodeId)) continue; // 连到组内兄弟 → 非出口
                        var hit = nodeById.TryGetValue(link.TargetNodeId, out var h) ? h : null;
                        if (hit != null && !exits.Contains(hit)) exits.Add(hit);
                    }
                }
                groupExits[kv.Key] = exits;
            }

            return new WorkflowTopology(
                nodeById,
                nextOf,
                prevOf,
                nodeKind,
                parallelRegions,
                forkByGroup,
                joinInBranches,
                forkMemberLinks,
                groupExits,
                nodeList);
        }

        /// <summary>
        /// 预解析单条连线为 <see cref="ResolvedOutLink"/>。
        /// 带条件（ConditionJson 非空）时做静态配置解析与校验；**配置错误仅记录不抛错**
        /// （由引擎运行时 <see cref="Service.WfEngineService.EvalParsedCondition"/> 在事务内抛出，保证回滚 + 消息包装）。
        /// 对应既有 <see cref="Service.WfEngineService.EvalLinkCondition"/> 的"配置错误"语义，仅把 JSON 解析移出热路径。
        /// </summary>
        private static ResolvedOutLink ResolveOutLink(WfNodeLink link)
        {
            var hasCondition = !string.IsNullOrWhiteSpace(link.ConditionJson);
            WfLinkCondition cond = null;
            string error = null;
            if (hasCondition)
            {
                (cond, error) = ParseAndValidate(link.ConditionJson);
            }
            return new ResolvedOutLink
            {
                TargetNodeId = link.TargetNodeId,
                HasCondition = hasCondition,
                Condition = cond,
                ConditionError = error
            };
        }

        /// <summary>
        /// 解析 + 静态校验连线条件。返回 (通过校验的条件, 配置错误信息)。
        /// 与既有收紧语义（2026-08-20）一致：JSON 解析失败 / field 缺失 / op 缺失或无效 / value 缺失均视为配置错误。
        /// 校验通过时错误信息为 null；校验失败时条件为 null、错误信息为带"条件配置错误"前缀的可抛文案。
        /// </summary>
        private static (WfLinkCondition cond, string error) ParseAndValidate(string conditionJson)
        {
            WfLinkCondition cond;
            try
            {
                cond = JsonConvert.DeserializeObject<WfLinkCondition>(conditionJson);
            }
            catch (Exception ex)
            {
                return (null, $"条件配置错误：连线条件 JSON 解析失败，{ex.Message}");
            }
            if (cond == null)
                return (null, "条件配置错误：连线条件 JSON 内容为空");
            var err = ValidateCondition(cond);
            if (err != null) return (null, err);
            return (cond, null);
        }

        /// <summary>递归静态校验条件（叶子或组合），返回错误文案；合法返回 null。</summary>
        private static string ValidateCondition(WfLinkCondition c)
        {
            if (c == null)
                return "条件配置错误：连线条件内容为空";
            // 组合条件：含子条件数组 → 递归校验每个子条件
            if (c.Conditions != null && c.Conditions.Count > 0)
            {
                var logic = (c.Logic ?? string.Empty).ToLowerInvariant();
                if (logic != "and" && logic != "or")
                    return $"条件配置错误：组合条件 logic 仅支持 and/or，当前为“{c.Logic}”";
                for (var i = 0; i < c.Conditions.Count; i++)
                {
                    var sub = ValidateCondition(c.Conditions[i]);
                    if (sub != null) return sub;
                }
                return null;
            }
            // 叶子条件：field + op + value 必填
            if (string.IsNullOrWhiteSpace(c.Field))
                return "条件配置错误：连线条件缺少条件字段 field";
            if (!c.Op.HasValue)
                return $"条件配置错误：连线条件[{c.Field}]缺少运算符 op";
            var op = (WfConditionOp)c.Op.Value;
            if (op == WfConditionOp.None || !System.Enum.IsDefined(typeof(WfConditionOp), c.Op.Value))
                return $"条件配置错误：连线条件[{c.Field}]运算符 op={c.Op.Value} 无效";
            if (string.IsNullOrWhiteSpace(c.Value))
                return $"条件配置错误：连线条件[{c.Field}]缺少比较值 value";
            return null;
        }
    }
}
