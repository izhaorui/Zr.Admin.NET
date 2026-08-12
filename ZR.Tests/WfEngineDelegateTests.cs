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
    /// P0-3 委托代审验证：原审批人将待办委托他人代审，任务仍归属原审批人（"X 代 Y 审"），
    /// 代审人可代为审批，流转与记录标注正常；非委托/非原审批人无法操作。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineDelegateTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineDelegateTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("applier", "boss", "proxyA", "proxyB");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
        }

        private WfFlowTask GetTask(long instanceId, long nodeId, string assignee) =>
            _db.Db.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == instanceId && t.NodeId == nodeId && t.Assignee == assignee)
                .OrderByDescending(t => t.TaskId)
                .First();

        private WfFlowRecord GetRecord(long instanceId, WfAction action) =>
            _db.Db.Queryable<WfFlowRecord>()
                .Where(r => r.InstanceId == instanceId && r.Action == (int)action)
                .OrderByDescending(r => r.RecordId)
                .First();

        [Fact]
        public void 委托后任务仍归属原审批人_且记录代审人()
        {
            var flowId = _db.AddDefinition("DELEGATE1", "委托测试");
            var node1 = _db.AddNode(flowId, "主管审", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 1);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "applier", ApplyUserId = _db.Uid("applier") });
            var task = GetTask(id, node1, "boss");
            var bossId = _db.Uid("boss");
            var proxyId = _db.Uid("proxyA");

            _engine.Delegate(task.TaskId, proxyId, "帮我审一下", bossId);

            var saved = _db.Db.Queryable<WfFlowTask>().InSingle(task.TaskId);
            // 任务归属不变（仍记原审批人名下）
            Assert.Equal(bossId, saved.AssigneeId);
            Assert.Equal(proxyId, saved.DelegateId);
            Assert.Equal("proxyA", saved.DelegateName);
            // 委托记录存在
            var rec = GetRecord(id, WfAction.Delegate);
            Assert.NotNull(rec);
            Assert.Equal(bossId, rec.OperatorId);
            Assert.Contains("proxyA", rec.Opinion);
        }

        [Fact]
        public void 代审人可代为审批_流转正常且记录标注代审()
        {
            var flowId = _db.AddDefinition("DELEGATE2", "代审通过");
            var node1 = _db.AddNode(flowId, "主管审", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 1);
            var node2 = _db.AddNode(flowId, "总监审", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("proxyB"), 2);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "applier", ApplyUserId = _db.Uid("applier") });
            var task = GetTask(id, node1, "boss");
            var bossId = _db.Uid("boss");
            var proxyId = _db.Uid("proxyA");

            _engine.Delegate(task.TaskId, proxyId, "", bossId);

            // 代审人（proxyA）代为通过
            _engine.Approve(task.TaskId, "同意", proxyId);

            var saved = _db.Db.Queryable<WfFlowTask>().InSingle(task.TaskId);
            Assert.Equal((int)WfTaskStatus.Done, saved.Status);
            // 流转推进到 node2
            Assert.Equal(node2, _db.Db.Queryable<WfFlowInstance>().InSingle(id).CurrentNodeId);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, node2, "proxyB").Status);

            // 审批记录标注"代 boss 审批"
            var rec = GetRecord(id, WfAction.Approve);
            Assert.NotNull(rec);
            Assert.Contains("代 boss 审批", rec.Opinion);
        }

        [Fact]
        public void 未获委托的他人无法操作委托任务()
        {
            var flowId = _db.AddDefinition("DELEGATE3", "越权防护");
            var node1 = _db.AddNode(flowId, "主管审", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 1);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "applier", ApplyUserId = _db.Uid("applier") });
            var task = GetTask(id, node1, "boss");

            // boss 委托给 proxyA
            _engine.Delegate(task.TaskId, _db.Uid("proxyA"), "", _db.Uid("boss"));

            // proxyB（未获委托）尝试审批 → 无权限
            var ex = Assert.Throws<CustomException>(() =>
                _engine.Approve(task.TaskId, "试试", _db.Uid("proxyB")));
            Assert.Contains("无审批权限", ex.Message);
        }

        [Fact]
        public void 不能委托给自己_且不能重复委托()
        {
            var flowId = _db.AddDefinition("DELEGATE4", "委托校验");
            var node1 = _db.AddNode(flowId, "主管审", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 1);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "applier", ApplyUserId = _db.Uid("applier") });
            var task = GetTask(id, node1, "boss");
            var bossId = _db.Uid("boss");
            var proxyId = _db.Uid("proxyA");

            // 不能委托给自己
            var selfEx = Assert.Throws<CustomException>(() =>
                _engine.Delegate(task.TaskId, bossId, "", bossId));
            Assert.Contains("自己", selfEx.Message);

            // 正常委托一次
            _engine.Delegate(task.TaskId, proxyId, "", bossId);

            // 重复委托应被拒绝
            var dupEx = Assert.Throws<CustomException>(() =>
                _engine.Delegate(task.TaskId, _db.Uid("proxyB"), "", bossId));
            Assert.Contains("已委托", dupEx.Message);
        }
    }
}
