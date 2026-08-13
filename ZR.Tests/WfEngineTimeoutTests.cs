using System;
using Infrastructure;
using Moq;
using Xunit;
using ZR.ServiceCore.Services;
using ZR.Workflow.Enum;
using ZR.Workflow.Model;
using ZR.Workflow.Service;
using ZR.Workflow.Service.IService;

namespace ZR.Tests
{
    [Collection("WfTests")]
    public class WfEngineTimeoutTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineTimeoutTests(WfTestDb db)
        {
            _db = db;
            _db.Clean();
            _db.EnsureUsers("alice", "boss", "carol");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
        }

        private long BuildSingleNodeFlow(int timeoutHours, int timeoutAction, long? transferUserId = null)
        {
            var defId = _db.AddDefinition("T" + Guid.NewGuid().ToString("N").Substring(0, 6), "超时流程");
            // 单审批节点，不设后续 link → 通过即流程终点
            _db.AddNode(defId, "审批", 1, 0, _db.Uids("boss"), 1,
                timeoutHours: timeoutHours, timeoutAction: timeoutAction, timeoutTransferUserId: transferUserId);
            var startId = _engine.Start(new WfFlowInstance
            {
                FlowId = defId,
                Title = "超时测试单",
                ApplyUser = "alice",
                ApplyUserId = _db.Uid("alice"),
            });
            return startId;
        }

        private void BackdateDeadline(long instanceId, int minutes)
        {
            var task = _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == instanceId);
            task.ArriveTime = DateTime.Now.AddMinutes(-(minutes + 1));
            task.DeadlineTime = DateTime.Now.AddMinutes(-minutes); // 已超时
            _db.Db.Updateable(task).ExecuteCommand();
        }

        [Fact]
        public void 超时_自动通过_推进到通过()
        {
            var startId = BuildSingleNodeFlow(timeoutHours: 1, timeoutAction: (int)WfTimeoutAction.AutoApprove);
            BackdateDeadline(startId, 2);

            _engine.ProcessTimeoutTasks();

            var inst = _db.Db.Queryable<WfFlowInstance>().First(i => i.InstanceId == startId);
            Assert.Equal((int)WfInstanceStatus.Approved, inst.Status); // 单节点通过后流程通过
            var rec = _db.Db.Queryable<WfFlowRecord>().OrderByDescending(r => r.RecordId).First(r => r.InstanceId == startId);
            Assert.Equal("超时自动通过", rec.Opinion);
        }

        [Fact]
        public void 超时_自动驳回_直接驳回()
        {
            var startId = BuildSingleNodeFlow(timeoutHours: 1, timeoutAction: (int)WfTimeoutAction.AutoReject);
            BackdateDeadline(startId, 2);

            _engine.ProcessTimeoutTasks();

            var inst = _db.Db.Queryable<WfFlowInstance>().First(i => i.InstanceId == startId);
            Assert.Equal((int)WfInstanceStatus.Rejected, inst.Status); // 无前驱/指定节点 → 直接驳回
        }

        [Fact]
        public void 超时_转交指定人_任务归属转移()
        {
            var startId = BuildSingleNodeFlow(timeoutHours: 1, timeoutAction: (int)WfTimeoutAction.Transfer, transferUserId: _db.Uid("carol"));
            BackdateDeadline(startId, 2);

            _engine.ProcessTimeoutTasks();

            var task = _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == startId);
            Assert.Equal(_db.Uid("carol"), task.AssigneeId); // 归属转给 carol
            Assert.Equal((int)WfTaskStatus.Pending, task.Status); // 待 carol 处理，未推进
            var rec = _db.Db.Queryable<WfFlowRecord>().OrderByDescending(r => r.RecordId).First(r => r.InstanceId == startId);
            Assert.StartsWith("超时自动转交", rec.Opinion);
        }

        [Fact]
        public void 超时_转交目标无效_退化为自动通过()
        {
            var startId = BuildSingleNodeFlow(timeoutHours: 1, timeoutAction: (int)WfTimeoutAction.Transfer, transferUserId: null);
            BackdateDeadline(startId, 2);

            _engine.ProcessTimeoutTasks();

            var inst = _db.Db.Queryable<WfFlowInstance>().First(i => i.InstanceId == startId);
            Assert.Equal((int)WfInstanceStatus.Approved, inst.Status); // 退化为自动通过 → 通过
        }

        [Fact]
        public void 未超时_不触发处理()
        {
            var startId = BuildSingleNodeFlow(timeoutHours: 2, timeoutAction: (int)WfTimeoutAction.AutoApprove);
            // 不回填截止时间（>now），保持未超时

            _engine.ProcessTimeoutTasks();

            var inst = _db.Db.Queryable<WfFlowInstance>().First(i => i.InstanceId == startId);
            Assert.Equal((int)WfInstanceStatus.Approval, inst.Status); // 流程仍在审批中
        }

        [Fact]
        public void 催办_首次成功_24小时内再次失败()
        {
            var startId = BuildSingleNodeFlow(timeoutHours: 0, timeoutAction: 0);

            _engine.Urge(startId, _db.Uid("alice")); // 申请人催办，成功
            var inst = _db.Db.Queryable<WfFlowInstance>().First(i => i.InstanceId == startId);
            Assert.NotNull(inst.LastUrgeTime);

            var ex = Assert.Throws<CustomException>(() => _engine.Urge(startId, _db.Uid("alice")));
            Assert.Contains("24", ex.Message);
        }

        [Fact]
        public void 催办_非申请人被拒()
        {
            var startId = BuildSingleNodeFlow(timeoutHours: 0, timeoutAction: 0);
            Assert.Throws<CustomException>(() => _engine.Urge(startId, _db.Uid("boss"))); // boss 非申请人
        }
    }
}
