using System;
using System.Linq;
using Infrastructure;
using Xunit;
using ZR.Workflow.Enum;
using ZR.Workflow.Model;
using ZR.Workflow.Service;

namespace ZR.Tests
{
    /// <summary>
    /// WfEngineService.Withdraw 单元测试：仅申请人可撤回、非审批中/当前节点已审批约束、
    /// 撤回后置撤回并跳过待办。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineWithdrawTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineWithdrawTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _engine = new WfEngineService();
        }

        private long BuildFlow(out long node1, out long node2)
        {
            var flowId = _db.AddDefinition("WITHDRAW", "撤回流程");
            node1 = _db.AddNode(flowId, "一级", (int)WfNodeType.Audit, (int)WfApproverType.User, "zhangsan", 1);
            node2 = _db.AddNode(flowId, "二级", (int)WfNodeType.Audit, (int)WfApproverType.User, "lisi", 2);
            return flowId;
        }

        private WfFlowTask GetTask(long instanceId, long nodeId, string assignee) =>
            _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == instanceId && t.NodeId == nodeId && t.Assignee == assignee);

        [Fact]
        public void Withdraw_申请人撤回_置撤回并跳过待办()
        {
            var flowId = BuildFlow(out var node1, out _);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });

            _engine.Withdraw(id, "alice");

            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal((int)WfInstanceStatus.Withdrawn, saved.Status);
            Assert.Equal((int)WfTaskStatus.Skipped, GetTask(id, node1, "zhangsan").Status);

            var rec = _db.Db.Queryable<WfFlowRecord>().First(r => r.InstanceId == id && r.Operator == "alice" && r.Action == (int)WfAction.Withdraw);
            Assert.Equal((int)WfAction.Withdraw, rec.Action);
        }

        [Fact]
        public void Withdraw_非申请人_抛仅申请人可撤回()
        {
            var flowId = BuildFlow(out _, out _);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });

            var ex = Assert.Throws<CustomException>(() => _engine.Withdraw(id, "bob"));
            Assert.Contains("仅申请人可撤回", ex.Message);
        }

        [Fact]
        public void Withdraw_实例已通过_抛当前状态不可撤回()
        {
            // 单节点流程，审批通过后实例为 Approved
            var flowId = _db.AddDefinition("WAPPROVED", "已通过");
            var node1 = _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.User, "zhangsan", 1);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });
            _engine.Approve(GetTask(id, node1, "zhangsan").TaskId, "同意", "zhangsan");

            var ex = Assert.Throws<CustomException>(() => _engine.Withdraw(id, "alice"));
            Assert.Contains("当前状态不可撤回", ex.Message);
        }

        [Fact]
        public void Withdraw_当前节点已审批_抛无法撤回()
        {
            // 一级为会签节点，单人通过后节点未推进但已存在 Done 任务
            var flowId = _db.AddDefinition("WAND", "会签撤回");
            var node1 = _db.AddNode(flowId, "会签", (int)WfNodeType.Audit, (int)WfApproverType.User, "zhangsan,lisi", 1, (int)WfSignType.And);
            var node2 = _db.AddNode(flowId, "二级", (int)WfNodeType.Audit, (int)WfApproverType.User, "wangwu", 2);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });

            // zhangsan 通过，lisi 仍待审，当前节点(会签)已存在已审任务
            _engine.Approve(GetTask(id, node1, "zhangsan").TaskId, "同意", "zhangsan");

            var ex = Assert.Throws<CustomException>(() => _engine.Withdraw(id, "alice"));
            Assert.Contains("当前节点已审批，无法撤回", ex.Message);
        }
    }
}
