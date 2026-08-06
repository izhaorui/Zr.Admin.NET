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
    /// WfEngineService.Start 单元测试。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineStartTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineStartTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("alice", "zhangsan", "lisi", "wangwu");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
        }

        [Fact]
        public void Start_单审批节点_生成实例与首节点待办及提交记录()
        {
            var flowId = _db.AddDefinition("LEAVE", "请假流程");
            var nodeId = _db.AddNode(flowId, "主管审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("zhangsan"), 1);

            var instance = new WfFlowInstance { FlowId = flowId, Title = "请假1天", ApplyUser = "alice" };
            var id = _engine.Start(instance);

            Assert.True(id > 0);

            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal((int)WfInstanceStatus.Approval, saved.Status);
            Assert.Equal(nodeId, saved.CurrentNodeId);
            Assert.Equal("请假流程", saved.FlowName);

            var tasks = _db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id).ToList();
            Assert.Single(tasks);
            Assert.Equal("zhangsan", tasks[0].Assignee);
            Assert.Equal((int)WfTaskStatus.Pending, tasks[0].Status);
            Assert.Equal(nodeId, tasks[0].NodeId);

            var records = _db.Db.Queryable<WfFlowRecord>().Where(r => r.InstanceId == id).ToList();
            Assert.Single(records);
            Assert.Equal((int)WfAction.Submit, records[0].Action);
            Assert.Equal("alice", records[0].Operator);
        }

        [Fact]
        public void Start_无审批或抄送节点_直接置通过()
        {
            var flowId = _db.AddDefinition("EMPTY", "无节点流程");
            _db.AddNode(flowId, "开始", (int)WfNodeType.Start, (int)WfApproverType.User, "", 1);

            var instance = new WfFlowInstance { FlowId = flowId, Title = "直接通过", ApplyUser = "alice" };
            var id = _engine.Start(instance);

            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal((int)WfInstanceStatus.Approved, saved.Status);
            Assert.Null(saved.CurrentNodeId);

            Assert.Empty(_db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id).ToList());
            Assert.Single(_db.Db.Queryable<WfFlowRecord>().Where(r => r.InstanceId == id).ToList());
        }

        [Fact]
        public void Start_流程定义不存在_抛CustomException()
        {
            var instance = new WfFlowInstance { FlowId = 999999, Title = "x", ApplyUser = "alice" };

            var ex = Assert.Throws<CustomException>(() => _engine.Start(instance));
            Assert.Contains("流程定义不存在", ex.Message);
        }

        [Fact]
        public void Start_已填流程名_保留用户填写值()
        {
            var flowId = _db.AddDefinition("LEAVE2", "请假流程2");
            _db.AddNode(flowId, "主管审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("zhangsan"), 1);

            var instance = new WfFlowInstance { FlowId = flowId, Title = "请假", ApplyUser = "alice", FlowName = "我的请假" };
            var id = _engine.Start(instance);

            Assert.Equal("我的请假", _db.Db.Queryable<WfFlowInstance>().InSingle(id).FlowName);
        }
    }
}
