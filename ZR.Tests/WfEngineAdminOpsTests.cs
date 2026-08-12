using System;
using System.Linq;
using System.Threading.Tasks;
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
    /// WfEngineService 管理员运维操作（P0）单元测试：终止 / 挂起 / 恢复 / 改派 / 跳转，
    /// 覆盖正常流转与异常拦截分支。仅校验状态机与任务变更，通知发送依赖 ISysUserMsgService Mock。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineAdminOpsTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineAdminOpsTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("alice", "zhangsan", "lisi", "wangwu", "admin");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
        }

        // 两级顺序审批：node1(zhangsan) → node2(lisi)
        private long BuildTwoStepFlow(out long node1, out long node2)
        {
            var flowId = _db.AddDefinition("ADMINOPS", "管理员运维");
            node1 = _db.AddNode(flowId, "一级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("zhangsan"), 1);
            node2 = _db.AddNode(flowId, "二级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("lisi"), 2);
            _db.AddLink(flowId, node1, node2);
            return flowId;
        }

        // 用 node1 反查 flowId 并发起一个运行中的实例
        private (long id, long node1) StartRunning()
        {
            BuildTwoStepFlow(out var node1, out _);
            var flowId = _db.Db.Queryable<WfFlowNode>().Where(n => n.NodeId == node1).Select(n => n.FlowId).First();
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });
            return (id, node1);
        }

        private WfFlowTask GetTask(long instanceId, long nodeId, string assignee) =>
            _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == instanceId && t.NodeId == nodeId && t.Assignee == assignee);

        [Fact]
        public async Task AdminTerminate_运行中实例_置终止且待办跳过()
        {
            (long id, long node1) = StartRunning();

            await _engine.AdminTerminate(id, _db.Uid("admin"), "违规");

            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal((int)WfInstanceStatus.Terminated, saved.Status);
            Assert.Equal((int)WfTaskStatus.Skipped, GetTask(id, node1, "zhangsan").Status);
            var rec = _db.Db.Queryable<WfFlowRecord>().First(r => r.InstanceId == id && r.Action == (int)WfAction.Terminate);
            Assert.NotNull(rec);
            Assert.Equal("违规", rec.Opinion);
        }

        [Fact]
        public async Task AdminTerminate_已终止实例_抛不可重复操作()
        {
            (long id, _) = StartRunning();
            await _engine.AdminTerminate(id, _db.Uid("admin"), "x");

            var ex = await Assert.ThrowsAsync<CustomException>(() => _engine.AdminTerminate(id, _db.Uid("admin"), "x2"));
            Assert.Contains("已终止", ex.Message);
        }

        [Fact]
        public async Task AdminSuspend_运行中_置挂起()
        {
            (long id, _) = StartRunning();

            await _engine.AdminSuspend(id, _db.Uid("admin"), "暂停");

            Assert.Equal((int)WfInstanceStatus.Suspended, _db.Db.Queryable<WfFlowInstance>().InSingle(id).Status);
        }

        [Fact]
        public async Task AdminResume_挂起后_恢复运行中()
        {
            (long id, _) = StartRunning();
            await _engine.AdminSuspend(id, _db.Uid("admin"), "暂停");

            await _engine.AdminResume(id, _db.Uid("admin"), "继续");

            Assert.Equal((int)WfInstanceStatus.Approval, _db.Db.Queryable<WfFlowInstance>().InSingle(id).Status);
        }

        [Fact]
        public async Task AdminResume_非挂起_抛仅被挂起可恢复()
        {
            (long id, _) = StartRunning();

            var ex = await Assert.ThrowsAsync<CustomException>(() => _engine.AdminResume(id, _db.Uid("admin"), "x"));
            Assert.Contains("仅被挂起", ex.Message);
        }

        [Fact]
        public async Task AdminReassign_节点待办_改派给目标用户()
        {
            (long id, long node1) = StartRunning();

            await _engine.AdminReassign(id, node1, _db.Uid("wangwu"), _db.Uid("admin"), "换人");

            var t = _db.Db.Queryable<WfFlowTask>().First(x => x.InstanceId == id && x.NodeId == node1);
            Assert.Equal(_db.Uid("wangwu"), t.AssigneeId);
            Assert.Equal("wangwu", t.Assignee);
            var rec = _db.Db.Queryable<WfFlowRecord>().First(r => r.InstanceId == id && r.Action == (int)WfAction.Reassign);
            Assert.NotNull(rec);
        }

        [Fact]
        public async Task AdminReassign_节点无未完成任务_抛无可改派()
        {
            (long id, long node1) = StartRunning();
            // 不存在的节点
            var ex = await Assert.ThrowsAsync<CustomException>(() => _engine.AdminReassign(id, node1 + 999999, _db.Uid("wangwu"), _db.Uid("admin"), "x"));
            Assert.Contains("无可改派", ex.Message);
        }

        [Fact]
        public async Task AdminJump_跳到后续节点_原待办跳过且目标节点生成待办()
        {
            (long id, long node1) = StartRunning();
            var flowId = _db.Db.Queryable<WfFlowNode>().Where(n => n.NodeId == node1).Select(n => n.FlowId).First();
            var node2 = _db.Db.Queryable<WfFlowNode>().Where(n => n.FlowId == flowId && n.NodeOrder == 2).Select(n => n.NodeId).First();

            await _engine.AdminJump(id, node2, _db.Uid("admin"), "跳过一级");

            // 原节点1待办被跳过
            Assert.Equal((int)WfTaskStatus.Skipped, GetTask(id, node1, "zhangsan").Status);
            // 目标节点2生成新待办（lisi）
            var t2 = _db.Db.Queryable<WfFlowTask>().First(x => x.InstanceId == id && x.NodeId == node2 && x.Assignee == "lisi");
            Assert.Equal((int)WfTaskStatus.Pending, t2.Status);
            var rec = _db.Db.Queryable<WfFlowRecord>().First(r => r.InstanceId == id && r.Action == (int)WfAction.Jump);
            Assert.NotNull(rec);
        }

        [Fact]
        public async Task AdminJump_目标节点不存在_抛跳转目标节点不存在()
        {
            (long id, long node1) = StartRunning();

            var ex = await Assert.ThrowsAsync<CustomException>(() => _engine.AdminJump(id, node1 + 999999, _db.Uid("admin"), "x"));
            Assert.Contains("跳转目标节点不存在", ex.Message);
        }
    }
}
