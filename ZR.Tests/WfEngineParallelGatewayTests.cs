using System;
using System.Linq;
using Infrastructure;
using Moq;
using Xunit;
using ZR.ServiceCore.Services;
using ZR.Workflow.Enum;
using ZR.Workflow.Model;
using ZR.Workflow.Service;

namespace ZR.Tests
{
    /// <summary>
    /// 并行网关（分叉 7 + 汇聚 8）流转测试。
    ///
    /// 设计：分叉(7)本身不生成任务，fork 同时激活全部出边目标（多活动分支并发）；
    /// 汇聚(8)本身不生成任务，等待所有入边分支（真实审批/抄送节点）均完成才继续。
    /// 实例活动节点集合存于 WfFlowInstance.CurrentNodeIds（JSON 数组），
    /// 并行期间多个分支同时处于活动态，测试里通过比对 CurrentNodeIds / 任务状态验证。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineParallelGatewayTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineParallelGatewayTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("alice", "a", "b", "c", "cc1", "cc2");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
        }

        private WfFlowTask GetTask(long instanceId, long nodeId, string assignee) =>
            _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == instanceId && t.NodeId == nodeId && t.Assignee == assignee);

        private WfFlowInstance GetInstance(long instanceId) =>
            _db.Db.Queryable<WfFlowInstance>().InSingle(instanceId);

        private System.Collections.Generic.List<long> GetActiveIds(long instanceId)
        {
            var raw = GetInstance(instanceId).CurrentNodeIds;
            if (string.IsNullOrWhiteSpace(raw)) return new System.Collections.Generic.List<long>();
            return Newtonsoft.Json.JsonConvert.DeserializeObject<long[]>(raw).ToList();
        }

        /// <summary>
        /// 分叉(7) → A、B 两个审批分支 → 汇聚(8) → 末审批 C。
        /// 发起后应立即 fork：A、B 两分支同时有待办；仅 A 通过不汇聚；A、B 都通过才到 C。
        /// </summary>
        [Fact]
        public void ParallelGateway_分叉后两分支并发_均完成才经汇聚到末节点()
        {
            var flowId = _db.AddDefinition("PG1", "并行网关");
            var fork = _db.AddNode(flowId, "分叉", (int)WfNodeType.ParallelFork, 0, "", 1);
            var a = _db.AddNode(flowId, "分支A", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("a"), 2);
            var b = _db.AddNode(flowId, "分支B", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("b"), 3);
            var join = _db.AddNode(flowId, "汇聚", (int)WfNodeType.ParallelJoin, 0, "", 4);
            var c = _db.AddNode(flowId, "末审批C", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("c"), 5);

            // 分叉 → A / 分叉 → B；A → 汇聚 / B → 汇聚；汇聚 → C
            _db.AddLink(flowId, fork, a, null, 1);
            _db.AddLink(flowId, fork, b, null, 2);
            _db.AddLink(flowId, a, join, null, 1);
            _db.AddLink(flowId, b, join, null, 2);
            _db.AddLink(flowId, join, c, null, 1);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });

            // 发起后分叉瞬时fork：A、B 同时有 Pending 待办
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, a, "a").Status);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, b, "b").Status);
            // 活动集应包含 A、B（分叉/汇聚为瞬时网关，不驻留活动集）
            var active = GetActiveIds(id);
            Assert.Contains(a, active);
            Assert.Contains(b, active);

            // 仅 A 通过：B 仍 Pending，汇聚(8)未触发，不推进到 C
            _engine.Approve(GetTask(id, a, "a").TaskId, "同意", "a");
            var inst = GetInstance(id);
            Assert.Equal((int)WfInstanceStatus.Approval, inst.Status);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, b, "b").Status);
            Assert.Empty(_db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id && t.NodeId == c).ToList());

            // B 通过：两分支均完成 → 汇聚(8)放行 → 到 C
            _engine.Approve(GetTask(id, b, "b").TaskId, "同意", "b");
            inst = GetInstance(id);
            Assert.Equal(c, inst.CurrentNodeId);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, c, "c").Status);

            _engine.Approve(GetTask(id, c, "c").TaskId, "同意", "c");
            Assert.Equal((int)WfInstanceStatus.Approved, GetInstance(id).Status);
        }

        /// <summary>
        /// 分叉(7) 无任何出边 → 视为流程直接结束（无分支可 fork）。
        /// </summary>
        [Fact]
        public void ParallelGateway_分叉无出边_直接结束()
        {
            var flowId = _db.AddDefinition("PG2", "空分叉");
            var fork = _db.AddNode(flowId, "分叉", (int)WfNodeType.ParallelFork, 0, "", 1);
            // 分叉无任何出边

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });

            var inst = GetInstance(id);
            Assert.Equal((int)WfInstanceStatus.Approved, inst.Status);
            Assert.Null(inst.CurrentNodeId);
            Assert.Empty(_db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id).ToList());
        }

        /// <summary>
        /// 汇聚(8)入边来自三分支（A/B/C），仅 A、B 完成、C 未完 → 汇聚保持等待，不推进到后续 D。
        /// </summary>
        [Fact]
        public void ParallelGateway_汇聚等待所有入边分支完成()
        {
            var flowId = _db.AddDefinition("PG3", "三分支汇聚");
            var fork = _db.AddNode(flowId, "分叉", (int)WfNodeType.ParallelFork, 0, "", 1);
            var a = _db.AddNode(flowId, "A", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("a"), 2);
            var b = _db.AddNode(flowId, "B", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("b"), 3);
            var c = _db.AddNode(flowId, "C", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("c"), 4);
            var join = _db.AddNode(flowId, "汇聚", (int)WfNodeType.ParallelJoin, 0, "", 5);
            var d = _db.AddNode(flowId, "D", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("c"), 6);

            _db.AddLink(flowId, fork, a, null, 1);
            _db.AddLink(flowId, fork, b, null, 2);
            _db.AddLink(flowId, fork, c, null, 3);
            _db.AddLink(flowId, a, join, null, 1);
            _db.AddLink(flowId, b, join, null, 2);
            _db.AddLink(flowId, c, join, null, 3);
            _db.AddLink(flowId, join, d, null, 1);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });

            // 三分支并发
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, a, "a").Status);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, b, "b").Status);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, c, "c").Status);

            // A、B 完成，C 未完：汇聚不触发
            _engine.Approve(GetTask(id, a, "a").TaskId, "ok", "a");
            _engine.Approve(GetTask(id, b, "b").TaskId, "ok", "b");
            var inst = GetInstance(id);
            Assert.Equal((int)WfInstanceStatus.Approval, inst.Status);
            Assert.Empty(_db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id && t.NodeId == d).ToList());

            // C 完成 → 汇聚放行 → 到 D
            _engine.Approve(GetTask(id, c, "c").TaskId, "ok", "c");
            inst = GetInstance(id);
            Assert.Equal(d, inst.CurrentNodeId);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, d, "c").Status);

            _engine.Approve(GetTask(id, d, "c").TaskId, "ok", "c");
            Assert.Equal((int)WfInstanceStatus.Approved, GetInstance(id).Status);
        }

        /// <summary>
        /// 分叉(7) → 审批分支 + 抄送分支 → 汇聚(8)：抄送节点视为瞬时完成（Skipped），
        /// 审批分支完成后即满足 join 条件。
        /// </summary>
        [Fact]
        public void ParallelGateway_抄送分支计入汇聚完成判定()
        {
            var flowId = _db.AddDefinition("PG4", "审批加抄送并行");
            var fork = _db.AddNode(flowId, "分叉", (int)WfNodeType.ParallelFork, 0, "", 1);
            var a = _db.AddNode(flowId, "审批A", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("a"), 2);
            var cc = _db.AddNode(flowId, "抄送B", (int)WfNodeType.Cc, (int)WfApproverType.User, $"{_db.Uids("cc1")},{_db.Uids("cc2")}", 3);
            var join = _db.AddNode(flowId, "汇聚", (int)WfNodeType.ParallelJoin, 0, "", 4);
            var c = _db.AddNode(flowId, "末审批C", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("c"), 5);

            _db.AddLink(flowId, fork, a, null, 1);
            _db.AddLink(flowId, fork, cc, null, 2);
            _db.AddLink(flowId, a, join, null, 1);
            _db.AddLink(flowId, cc, join, null, 2);
            _db.AddLink(flowId, join, c, null, 1);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });

            // 审批分支待审 + 抄送分支已落 Skipped 任务
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, a, "a").Status);
            var ccTasks = _db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id && t.NodeId == cc).ToList();
            Assert.Single(ccTasks);
            Assert.Equal((int)WfTaskStatus.Skipped, ccTasks[0].Status);

            // 仅审批分支完成 → 汇聚判定（抄送已 Skipped 视为完成）即放行 → 到 C
            _engine.Approve(GetTask(id, a, "a").TaskId, "ok", "a");
            var inst = GetInstance(id);
            Assert.Equal(c, inst.CurrentNodeId);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, c, "c").Status);

            _engine.Approve(GetTask(id, c, "c").TaskId, "ok", "c");
            Assert.Equal((int)WfInstanceStatus.Approved, GetInstance(id).Status);
        }
    }
}
