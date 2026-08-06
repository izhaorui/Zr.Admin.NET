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
    /// WfEngineService.Approve 单元测试：或签/会签、权限与状态校验、推进下一节点、
    /// 末节点通过置通过、完成后跳过同节点其余待办。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineApproveTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineApproveTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("alice", "zhangsan", "lisi", "wangwu");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
        }

        private long BuildTwoNodeFlow(out long node1, out long node2)
        {
            var flowId = _db.AddDefinition("APPROVE", "审批流程");
            node1 = _db.AddNode(flowId, "一级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("zhangsan"), 1);
            node2 = _db.AddNode(flowId, "二级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("wangwu"), 2);
            return flowId;
        }

        private WfFlowTask GetTask(long instanceId, long nodeId, string assignee) =>
            _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == instanceId && t.NodeId == nodeId && t.Assignee == assignee);

        [Fact]
        public void Approve_或签单人通过_推进到下一节点()
        {
            var flowId = BuildTwoNodeFlow(out var node1, out var node2);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });

            var task = GetTask(id, node1, "zhangsan");
            _engine.Approve(task.TaskId, "同意", "zhangsan");

            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal((int)WfInstanceStatus.Approval, saved.Status);
            Assert.Equal(node2, saved.CurrentNodeId);

            Assert.Equal((int)WfTaskStatus.Done, GetTask(id, node1, "zhangsan").Status);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, node2, "wangwu").Status);
            Assert.Contains(_db.Db.Queryable<WfFlowRecord>().Where(r => r.InstanceId == id).ToList(),
                r => r.Action == (int)WfAction.Approve && r.Operator == "zhangsan");
        }

        [Fact]
        public void Approve_会签需全部通过_单人通过不推进()
        {
            var flowId = _db.AddDefinition("ANDSIGN", "会签流程");
            var node1 = _db.AddNode(flowId, "会签", (int)WfNodeType.Audit, (int)WfApproverType.User, $"{_db.Uids("zhangsan")},{_db.Uids("lisi")}", 1, (int)WfSignType.And);
            var node2 = _db.AddNode(flowId, "二级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("wangwu"), 2);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });

            _engine.Approve(GetTask(id, node1, "zhangsan").TaskId, "同意", "zhangsan");
            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal(node1, saved.CurrentNodeId);
            Assert.Equal((int)WfInstanceStatus.Approval, saved.Status);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, node1, "lisi").Status);

            _engine.Approve(GetTask(id, node1, "lisi").TaskId, "同意", "lisi");
            saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal(node2, saved.CurrentNodeId);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, node2, "wangwu").Status);
        }

        [Fact]
        public void Approve_非审批人_抛无权限()
        {
            var flowId = BuildTwoNodeFlow(out var node1, out _);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });

            var task = GetTask(id, node1, "zhangsan");
            var ex = Assert.Throws<CustomException>(() => _engine.Approve(task.TaskId, "同意", "lisi"));
            Assert.Contains("无审批权限", ex.Message);
        }

        [Fact]
        public void Approve_已处理任务_抛该任务已处理()
        {
            var flowId = BuildTwoNodeFlow(out var node1, out _);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });

            var task = GetTask(id, node1, "zhangsan");
            _engine.Approve(task.TaskId, "同意", "zhangsan");

            var ex = Assert.Throws<CustomException>(() => _engine.Approve(task.TaskId, "再同意", "zhangsan"));
            Assert.Contains("该任务已处理", ex.Message);
        }

        [Fact]
        public void Approve_末节点通过_实例置通过()
        {
            var flowId = _db.AddDefinition("SINGLE", "单节点");
            var node1 = _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("zhangsan"), 1);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });

            _engine.Approve(GetTask(id, node1, "zhangsan").TaskId, "同意", "zhangsan");

            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal((int)WfInstanceStatus.Approved, saved.Status);
            Assert.Equal((int)WfTaskStatus.Done, GetTask(id, node1, "zhangsan").Status);
        }

        [Fact]
        public void Approve_或签通过_跳过同节点其余待办()
        {
            var flowId = _db.AddDefinition("ORSKIP", "或签跳过");
            var node1 = _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.User, $"{_db.Uids("zhangsan")},{_db.Uids("lisi")}", 1, (int)WfSignType.Or);
            var node2 = _db.AddNode(flowId, "二级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("wangwu"), 2);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });

            _engine.Approve(GetTask(id, node1, "zhangsan").TaskId, "同意", "zhangsan");

            Assert.Equal((int)WfTaskStatus.Done, GetTask(id, node1, "zhangsan").Status);
            Assert.Equal((int)WfTaskStatus.Skipped, GetTask(id, node1, "lisi").Status);
            Assert.Equal(node2, _db.Db.Queryable<WfFlowInstance>().InSingle(id).CurrentNodeId);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, node2, "wangwu").Status);
        }
    }
}
