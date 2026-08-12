using System.Linq;
using Moq;
using Xunit;
using Infrastructure;
using ZR.ServiceCore.Services;
using ZR.Workflow.Enum;
using ZR.Workflow.Model;
using ZR.Workflow.Service;

namespace ZR.Tests
{
    /// <summary>
    /// 引擎审查修复（2026-08-12）回归验证：
    /// 1) 并行阶段（活动节点 >1）一律禁止撤回——避免撤回破坏已产生/进行中的分支轨迹；
    /// 2) 串行阶段保持"当前节点未处理才可撤回"（前序节点已审批不阻断撤回）。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineEngineFixTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineEngineFixTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("applier", "boss", "colleague", "proxyB");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
        }

        private WfFlowTask GetTask(long instanceId, long nodeId, string assignee) =>
            _db.Db.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == instanceId && t.NodeId == nodeId && t.Assignee == assignee)
                .OrderByDescending(t => t.TaskId)
                .First();

        /// <summary>
        /// 并行分叉(7)阶段：即使全部并行分支都未审批，活动集也有多个节点 → 一律禁止撤回。
        /// （并行阶段整单撤回会破坏分支轨迹，故直接拒绝。）
        /// </summary>
        [Fact]
        public void 并行阶段未审批也禁止撤回()
        {
            var flowId = _db.AddDefinition("FIX_WD_PAR", "并行撤回");
            var fork = _db.AddNode(flowId, "分叉", (int)WfNodeType.ParallelFork, (int)WfApproverType.User, _db.Uids("boss"), 1);
            var branchA = _db.AddNode(flowId, "分支A", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 2);
            var branchB = _db.AddNode(flowId, "分支B", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("colleague"), 3);
            var join = _db.AddNode(flowId, "汇聚", (int)WfNodeType.ParallelJoin, (int)WfApproverType.User, _db.Uids("boss"), 4);
            var end = _db.AddNode(flowId, "总监", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("proxyB"), 5);
            _db.AddLink(flowId, fork, branchA);
            _db.AddLink(flowId, fork, branchB);
            _db.AddLink(flowId, branchA, join);
            _db.AddLink(flowId, branchB, join);
            _db.AddLink(flowId, join, end);

            var applierId = _db.Uid("applier");
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "applier", ApplyUserId = applierId });

            // 并行阶段活动集 >1 → 撤回被拒
            var ex = Assert.Throws<CustomException>(() => _engine.Withdraw(id, applierId));
            Assert.Contains("并行", ex.Message);
            Assert.Equal((int)WfInstanceStatus.Approval, _db.Db.Queryable<WfFlowInstance>().InSingle(id).Status);
        }

        /// <summary>
        /// 并行分叉阶段：某分支已审批后再撤回 → 同样被拒（并行阶段一律不可撤回）。
        /// </summary>
        [Fact]
        public void 并行某分支已审批后撤回_被拒绝()
        {
            var flowId = _db.AddDefinition("FIX_WD_PAR2", "并行撤回2");
            var fork = _db.AddNode(flowId, "分叉", (int)WfNodeType.ParallelFork, (int)WfApproverType.User, _db.Uids("boss"), 1);
            var branchA = _db.AddNode(flowId, "分支A", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 2);
            var branchB = _db.AddNode(flowId, "分支B", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("colleague"), 3);
            var join = _db.AddNode(flowId, "汇聚", (int)WfNodeType.ParallelJoin, (int)WfApproverType.User, _db.Uids("boss"), 4);
            var end = _db.AddNode(flowId, "总监", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("proxyB"), 5);
            _db.AddLink(flowId, fork, branchA);
            _db.AddLink(flowId, fork, branchB);
            _db.AddLink(flowId, branchA, join);
            _db.AddLink(flowId, branchB, join);
            _db.AddLink(flowId, join, end);

            var applierId = _db.Uid("applier");
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "applier", ApplyUserId = applierId });

            // 分支A 已审批
            var aTask = GetTask(id, branchA, "boss");
            _engine.Approve(aTask.TaskId, "同意", _db.Uid("boss"));

            // 并行阶段仍不可撤回
            var ex = Assert.Throws<CustomException>(() => _engine.Withdraw(id, applierId));
            Assert.Contains("并行", ex.Message);
            // 分支B 待办保留
            Assert.True(_db.Db.Queryable<WfFlowTask>()
                .Any(t => t.InstanceId == id && t.NodeId == branchB && t.Status == (int)WfTaskStatus.Pending));
        }

        /// <summary>
        /// 串行阶段（活动节点=1）：当前节点未处理时可撤回，即使前序节点已被审批。
        /// </summary>
        [Fact]
        public void 串行当前节点未处理时撤回_正常()
        {
            var flowId = _db.AddDefinition("FIX_WD_SEQ", "串行撤回");
            var node1 = _db.AddNode(flowId, "主管", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 1);
            var node2 = _db.AddNode(flowId, "总监", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("colleague"), 2);

            var applierId = _db.Uid("applier");
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "applier", ApplyUserId = applierId });

            // node1 已审批，推进到 node2（node2 未处理）
            var task1 = GetTask(id, node1, "boss");
            _engine.Approve(task1.TaskId, "同意", _db.Uid("boss"));
            Assert.Equal(node2, _db.Db.Queryable<WfFlowInstance>().InSingle(id).CurrentNodeId);

            // 活动集=[node2] 单节点，node2 未 Done → 允许撤回
            _engine.Withdraw(id, applierId);
            Assert.Equal((int)WfInstanceStatus.Withdrawn, _db.Db.Queryable<WfFlowInstance>().InSingle(id).Status);
        }

        /// <summary>
        /// 串行阶段：当前节点已被审批后不可撤回（原行为保持）。
        /// </summary>
        [Fact]
        public void 串行当前节点已审批后撤回_被拒绝()
        {
            var flowId = _db.AddDefinition("FIX_WD_SEQ2", "串行撤回2");
            var node1 = _db.AddNode(flowId, "主管", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 1);

            var applierId = _db.Uid("applier");
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "applier", ApplyUserId = applierId });

            var task1 = GetTask(id, node1, "boss");
            _engine.Approve(task1.TaskId, "同意", _db.Uid("boss"));

            // 已是最后一个节点，通过后实例 Approved，撤回本就不允许（状态非 Approval）
            var ex = Assert.Throws<CustomException>(() => _engine.Withdraw(id, applierId));
            Assert.NotNull(ex.Message);
        }
    }
}
