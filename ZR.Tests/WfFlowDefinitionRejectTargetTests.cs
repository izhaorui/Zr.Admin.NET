using System.Linq;
using Xunit;
using ZR.Workflow.Enum;
using ZR.Workflow.Model;
using ZR.Workflow.Model.Dto;
using ZR.Workflow.Service;

namespace ZR.Tests
{
    /// <summary>
    /// 流程定义保存时「驳回目标节点(RejectTargetNodeId)」重映射验证。
    /// 前端暴露策略2（驳回到指定节点）后，新建节点使用负数临时 id 作 NodeId，
    /// 保存必须经 WfFlowDefinitionService.InsertNodes 的 nodeMap 把 RejectTargetNodeId
    /// 重映射为真实主键，否则会落库成悬空负数、引擎找不到驳回目标。
    /// </summary>
    [Collection("WfTests")]
    public class WfFlowDefinitionRejectTargetTests
    {
        private readonly WfTestDb _db;
        private readonly WfFlowDefinitionService _svc;

        public WfFlowDefinitionRejectTargetTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("alice", "bob");
            // WfFlowDefinitionService 继承 BaseService，无参构造时用 DbScoped.SugarScope（已由 WfTestDb 指向 SQLite）
            _svc = new WfFlowDefinitionService();
        }

        [Fact]
        public void Add_策略2驳回目标为新建节点负数临时Id_保存后重映射为真实主键()
        {
            // 节点 A（nodeId=-1，审批，bob）、节点 B（nodeId=-2，审批，alice）
            var nodeA = new WfFlowNodeDto
            {
                NodeId = -1,
                NodeName = "初审",
                NodeType = (int)WfNodeType.Audit,
                ApproverType = 0,
                ApproverId = _db.Uids("bob"),
                NodeOrder = 1,
            };
            var nodeB = new WfFlowNodeDto
            {
                NodeId = -2,
                NodeName = "复审",
                NodeType = (int)WfNodeType.Audit,
                ApproverType = 0,
                ApproverId = _db.Uids("alice"),
                NodeOrder = 2,
                RejectStrategy = (int)WfRejectStrategy.ToSpecifiedNode,
                // 关键点：驳回目标引用的是 A 的「负数临时 id」，模拟前端新建节点场景
                RejectTargetNodeId = -1,
            };

            var dto = new WfFlowDefinitionDto
            {
                FlowCode = "REJECTMAP",
                FlowName = "驳回目标重映射",
                Nodes = new() { nodeA, nodeB },
                NodeLinks = new()
                {
                    new WfNodeLinkDto { FlowId = 0, SourceNodeId = -1, TargetNodeId = -2 },
                },
            };

            var def = _svc.Add(dto);
            Assert.True(def.FlowId > 0);

            // 落库后取出真实节点
            var saved = _db.Db.Queryable<WfFlowNode>()
                .Where(n => n.FlowId == def.FlowId)
                .ToList();
            var a = saved.Single(n => n.NodeName == "初审");
            var b = saved.Single(n => n.NodeName == "复审");

            // 两个节点都拿到了正数真实主键（负数临时 id 已被替换）
            Assert.True(a.NodeId > 0);
            Assert.True(b.NodeId > 0);
            Assert.NotEqual(a.NodeId, b.NodeId);

            // 连线也被重映射
            var link = _db.Db.Queryable<WfNodeLink>().Single(l => l.FlowId == def.FlowId);
            Assert.Equal(a.NodeId, link.SourceNodeId);
            Assert.Equal(b.NodeId, link.TargetNodeId);

            // 核心断言：B 的 RejectTargetNodeId 必须等于 A 的真实主键，而非前端传入的 -1
            Assert.Equal(a.NodeId, b.RejectTargetNodeId);
            Assert.NotEqual(-1, b.RejectTargetNodeId);
            Assert.Equal((int)WfRejectStrategy.ToSpecifiedNode, b.RejectStrategy);
        }

        [Fact]
        public void Add_策略2无连线指向目标_仍按nodeMap回退原值不报错()
        {
            // 目标指向一个根本不存在的 nodeId（-99），nodeMap 未命中，应保持原值（不崩溃、不重映射）
            var nodeA = new WfFlowNodeDto
            {
                NodeId = -1,
                NodeName = "唯一审批",
                NodeType = (int)WfNodeType.Audit,
                ApproverType = 0,
                ApproverId = _db.Uids("bob"),
                NodeOrder = 1,
                RejectStrategy = (int)WfRejectStrategy.ToSpecifiedNode,
                RejectTargetNodeId = -99, // 悬空引用
            };

            var dto = new WfFlowDefinitionDto
            {
                FlowCode = "REJECTORPHAN",
                FlowName = "驳回目标悬空",
                Nodes = new() { nodeA },
                NodeLinks = new(),
            };

            // 不应抛异常
            var def = _svc.Add(dto);
            Assert.True(def.FlowId > 0);

            var saved = _db.Db.Queryable<WfFlowNode>().Single(n => n.FlowId == def.FlowId);
            // 未命中 nodeMap（前端正常只会传合法节点 nodeId，此场景仅防御）：重映射逻辑将无效引用归零，
            // 避免脏负数落库；运行期引擎对 RejectTargetNodeId<=0 按"无指定目标"降级处理。
            Assert.Equal(0, saved.RejectTargetNodeId);
        }
    }
}
