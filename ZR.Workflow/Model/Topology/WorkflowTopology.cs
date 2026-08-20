using ZR.Workflow.Enum;
using ZR.Workflow.Model.Dto;

namespace ZR.Workflow.Model.Topology
{
    /// <summary>
    /// 已解析的连线条件（<see cref="WfLinkCondition"/> 预反序列化结果 + 静态配置校验结论）。
    /// 每次流转不再重复反序列化同一份 ConditionJson，仅做运行时"表单取字段值 + 比较"。
    ///
    /// 静态配置错误（JSON 解析失败 / field 缺失 / op 缺失或无效 / value 缺失）在预解析时**记录**到
    /// <see cref="ConditionError"/>（不立即抛错），由引擎运行时 <see cref="WfEngineService.EvalParsedCondition"/>
    /// 在**事务内**抛出（保证事务回滚 + 消息经 RunInTx 包装），与既有收紧语义完全一致。
    /// </summary>
    public sealed class ResolvedOutLink
    {
        /// <summary>目标节点 Id</summary>
        public long TargetNodeId { get; init; }

        /// <summary>是否带条件（ConditionJson 非空）。无条件的出边为默认分支。</summary>
        public bool HasCondition { get; init; }

        /// <summary>预反序列化并静态校验通过的条件；解析失败/配置错误时为 null。</summary>
        public WfLinkCondition Condition { get; init; }

        /// <summary>预解析/静态校验失败的原因；为 null 表示条件配置合法。</summary>
        public string ConditionError { get; init; }
    }

    /// <summary>
    /// 流程静态拓扑（不可变）。由 <see cref="WfWorkflowTopologyBuilder.Build"/> 每次操作现构建，
    /// 把引擎热路径上的 O(n) 线性查找（FirstOrDefault/Where）收敛为 O(1) 索引访问。
    ///
    /// 仅承载"定义态静态结构"，不含任何运行态（任务/活动集）状态；多实例并发安全。
    ///
    /// 字段说明：
    /// - <see cref="NodeById"/>：nodeId → 节点，替代散落的 <c>allNodes.FirstOrDefault(n => n.NodeId==x)</c>。
    /// - <see cref="NextOf"/>：源节点 → 预解析出边（含默认分支排序），替代 <c>linksBySource</c>。
    /// - <see cref="PrevOf"/>：目标节点 → 入边源节点列表，替代 <c>linksByTarget</c>（Join 判定用）。
    /// - <see cref="NodeKind"/>：nodeId → 节点类型强类型视图，替代散落的 <c>(WfNodeType)node.NodeType</c> 比较。
    /// - <see cref="ParallelRegions"/>：ParallelGroup → 组内节点列表，替代 <c>allNodes.Where(n => n.ParallelGroup==x)</c>。
    /// - <see cref="ForkByGroup"/>：ParallelGroup → 该组的并行分叉网关(7)节点。
    ///
    /// **Join / Parallel 语义（2026-08-20 补齐）**：
    /// - <see cref="JoinInBranches"/>：ParallelJoin(8) → 入边**业务分支节点**（Audit/Cc，去重）。
    ///   把 Join 完成判定里"只校验审批/抄送分支、网关源视为瞬时完成"的语义预计算为索引，
    ///   替代引擎 <c>IsJoinComplete</c>/<c>LoadJoinBranchTasks</c> 中重复的 <c>src.NodeType==Audit||Cc</c> 过滤。
    /// - <see cref="ForkMemberLinks"/>：ParallelFork(7) → 其出边 target 到 <see cref="ResolvedOutLink"/> 的映射。
    ///   供并行分组 fork 时判断"分叉网关 → 成员"的条件命中（<c>ShouldActivateForkMember</c>），O(1) 取边。
    /// - <see cref="GroupExits"/>：ParallelGroup → 组内成员指向**组外节点**的出边目标（去重）。
    ///   即并行分组整组完成后的汇聚出口，link 为唯一串联事实，替代引擎 <c>ResolveParallelGroupExit</c>。
    /// </summary>
    public sealed class WorkflowTopology
    {
        /// <summary>nodeId → 节点</summary>
        public IReadOnlyDictionary<long, WfFlowNode> NodeById { get; }

        /// <summary>源节点 → 预解析出边（按 Sort 升序）</summary>
        public IReadOnlyDictionary<long, IReadOnlyList<ResolvedOutLink>> NextOf { get; }

        /// <summary>目标节点 → 入边源节点 Id 列表</summary>
        public IReadOnlyDictionary<long, IReadOnlyList<long>> PrevOf { get; }

        /// <summary>nodeId → 节点类型强类型视图</summary>
        public IReadOnlyDictionary<long, WfNodeType> NodeKind { get; }

        /// <summary>ParallelGroup → 组内节点列表</summary>
        public IReadOnlyDictionary<int, IReadOnlyList<WfFlowNode>> ParallelRegions { get; }

        /// <summary>ParallelGroup → 该组的并行分叉网关(7)节点（可能为 null：存量数据无显式 fork）</summary>
        public IReadOnlyDictionary<int, WfFlowNode> ForkByGroup { get; }

        /// <summary>ParallelJoin(8) → 入边业务分支节点（Audit/Cc，去重）</summary>
        public IReadOnlyDictionary<long, IReadOnlyList<WfFlowNode>> JoinInBranches { get; }

        /// <summary>ParallelFork(7) → 出边 target → ResolvedOutLink 映射</summary>
        public IReadOnlyDictionary<long, IReadOnlyDictionary<long, ResolvedOutLink>> ForkMemberLinks { get; }

        /// <summary>ParallelGroup → 组内成员指向组外节点的出边目标（去重）</summary>
        public IReadOnlyDictionary<int, IReadOnlyList<WfFlowNode>> GroupExits { get; }

        /// <summary>按 NodeOrder 升序的节点全集（保留给首节点查找与展示排序）</summary>
        public IReadOnlyList<WfFlowNode> OrderedNodes { get; }

        public WorkflowTopology(
            IReadOnlyDictionary<long, WfFlowNode> nodeById,
            IReadOnlyDictionary<long, IReadOnlyList<ResolvedOutLink>> nextOf,
            IReadOnlyDictionary<long, IReadOnlyList<long>> prevOf,
            IReadOnlyDictionary<long, WfNodeType> nodeKind,
            IReadOnlyDictionary<int, IReadOnlyList<WfFlowNode>> parallelRegions,
            IReadOnlyDictionary<int, WfFlowNode> forkByGroup,
            IReadOnlyDictionary<long, IReadOnlyList<WfFlowNode>> joinInBranches,
            IReadOnlyDictionary<long, IReadOnlyDictionary<long, ResolvedOutLink>> forkMemberLinks,
            IReadOnlyDictionary<int, IReadOnlyList<WfFlowNode>> groupExits,
            IReadOnlyList<WfFlowNode> orderedNodes)
        {
            NodeById = nodeById;
            NextOf = nextOf;
            PrevOf = prevOf;
            NodeKind = nodeKind;
            ParallelRegions = parallelRegions;
            ForkByGroup = forkByGroup;
            JoinInBranches = joinInBranches;
            ForkMemberLinks = forkMemberLinks;
            GroupExits = groupExits;
            OrderedNodes = orderedNodes;
        }

        /// <summary>按 nodeId 取节点，不存在返回 null。</summary>
        public WfFlowNode GetNode(long nodeId) => NodeById.TryGetValue(nodeId, out var n) ? n : null;

        /// <summary>取某源节点的出边列表，无出边返回空列表（不返回 null）。</summary>
        public IReadOnlyList<ResolvedOutLink> GetOutLinks(long nodeId)
            => NextOf.TryGetValue(nodeId, out var ls) ? ls : Array.Empty<ResolvedOutLink>();

        /// <summary>取某节点的入边源节点 Id 列表，无入边返回空列表。</summary>
        public IReadOnlyList<long> GetInSourceIds(long nodeId)
            => PrevOf.TryGetValue(nodeId, out var ls) ? ls : Array.Empty<long>();

        /// <summary>取并行分组内节点，无该组返回空列表。</summary>
        public IReadOnlyList<WfFlowNode> GetGroupNodes(int parallelGroup)
            => ParallelRegions.TryGetValue(parallelGroup, out var ls) ? ls : Array.Empty<WfFlowNode>();

        /// <summary>
        /// 是否业务节点（审批/抄送）。Join 汇聚完成判定只校验业务分支节点；
        /// 网关（4/7/8）不生成任务、由流转自然跳过，视为瞬时完成不计入 join 判定。
        /// </summary>
        public bool IsBusinessNode(WfFlowNode node)
            => node != null && (node.NodeType == (int)WfNodeType.Audit || node.NodeType == (int)WfNodeType.Cc);

        /// <summary>取 Join(8) 入边的业务分支节点（Audit/Cc，去重），无入边或非 Join 返回空列表。</summary>
        public IReadOnlyList<WfFlowNode> GetJoinInBranches(long joinNodeId)
            => JoinInBranches.TryGetValue(joinNodeId, out var ls) ? ls : Array.Empty<WfFlowNode>();

        /// <summary>
        /// 取并行分叉网关(7) → 指定成员节点的出边；无该出边返回 null。
        /// 供并行分组 fork 判断"成员分支条件是否命中"（对标 BPMN Edge 属性模型）。
        /// </summary>
        public ResolvedOutLink GetForkMemberLink(long forkNodeId, long memberNodeId)
            => ForkMemberLinks.TryGetValue(forkNodeId, out var map) && map.TryGetValue(memberNodeId, out var link)
                ? link : null;

        /// <summary>取并行分组整组完成后的汇聚出口节点（组内成员指向组外节点的出边目标，去重），无该组返回空列表。</summary>
        public IReadOnlyList<WfFlowNode> GetGroupExits(int parallelGroup)
            => GroupExits.TryGetValue(parallelGroup, out var ls) ? ls : Array.Empty<WfFlowNode>();
    }
}
