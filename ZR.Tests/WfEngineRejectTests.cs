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
    /// WfEngineService.Reject 单元测试：驳回置实例驳回、跳过其余待办、权限校验，
    /// 以及驳回后流程不可再审批。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineRejectTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineRejectTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("alice", "zhangsan", "lisi", "wangwu");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
        }

        private long BuildTwoNodeFlow(out long node1, out long node2)
        {
            var flowId = _db.AddDefinition("REJECT", "驳回流程");
            node1 = _db.AddNode(flowId, "一级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("zhangsan"), 1);
            node2 = _db.AddNode(flowId, "二级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("lisi"), 2);
            return flowId;
        }

        private WfFlowTask GetTask(long instanceId, long nodeId, string assignee) =>
            _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == instanceId && t.NodeId == nodeId && t.Assignee == assignee);

        [Fact]
        public void Reject_二级驳回_实例置驳回并记录驳回动作()
        {
            var flowId = BuildTwoNodeFlow(out var node1, out var node2);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });
            _engine.Approve(GetTask(id, node1, "zhangsan").TaskId, "同意", "zhangsan");

            _engine.Reject(GetTask(id, node2, "lisi").TaskId, "不同意", "lisi");

            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal((int)WfInstanceStatus.Rejected, saved.Status);
            Assert.Equal((int)WfTaskStatus.Done, GetTask(id, node2, "lisi").Status);

            var rec = _db.Db.Queryable<WfFlowRecord>().First(r => r.InstanceId == id && r.Operator == "lisi");
            Assert.Equal((int)WfAction.Reject, rec.Action);
            Assert.Equal("不同意", rec.Opinion);
        }

        [Fact]
        public void Reject_非审批人_抛无权限()
        {
            var flowId = BuildTwoNodeFlow(out var node1, out _);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });

            var task = GetTask(id, node1, "zhangsan");
            var ex = Assert.Throws<CustomException>(() => _engine.Reject(task.TaskId, "不同意", "wangwu"));
            Assert.Contains("无审批权限", ex.Message);
        }

        [Fact]
        public void Reject_驳回后流程不可再审批_抛异常()
        {
            var flowId = BuildTwoNodeFlow(out var node1, out var node2);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });
            _engine.Approve(GetTask(id, node1, "zhangsan").TaskId, "同意", "zhangsan");
            _engine.Reject(GetTask(id, node2, "lisi").TaskId, "不同意", "lisi");

            var ex = Assert.Throws<CustomException>(() =>
                _engine.Approve(GetTask(id, node1, "zhangsan").TaskId, "再同意", "zhangsan"));
            Assert.True(ex.Message.Contains("该任务已处理") || ex.Message.Contains("流程状态异常"));
        }
    }
}
