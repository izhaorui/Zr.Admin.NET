using System.Linq;
using Moq;
using Xunit;
using ZR.ServiceCore.Services;
using ZR.Workflow.Enum;
using ZR.Workflow.Model;
using ZR.Workflow.Service;

namespace ZR.Tests
{
    /// <summary>
    /// P0-1 引擎增强验证：依次审批（顺序会签）+ 可配置驳回策略（驳回到上一节点）。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineSequentialAndRejectTests : IClassFixture<WfTestDb>
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineSequentialAndRejectTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("alice", "zhangsan", "lisi", "wangwu");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
        }

        private WfFlowTask GetTask(long id, long node, string user) =>
            _db.Db.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == id && t.NodeId == node && t.Assignee == user)
                .OrderByDescending(t => t.TaskId)
                .First();

        [Fact]
        public void 依次审批_中间人通过后轮到下一位_末位通过才推进()
        {
            var flowId = _db.AddDefinition("SEQ", "依次审批流程");
            // 节点1：张三、王五依次审批；节点2：李四
            var node1 = _db.AddNode(flowId, "主管依次审", (int)WfNodeType.Audit, (int)WfApproverType.User,
                $"{_db.Uids("zhangsan")},{_db.Uids("wangwu")}", 1, (int)WfSignType.Sequential);
            var node2 = _db.AddNode(flowId, "总监审", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("lisi"), 2);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            // 发起后：首人张三 Pending，王五 Waiting
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, node1, "zhangsan").Status);
            Assert.Equal((int)WfTaskStatus.Waiting, GetTask(id, node1, "wangwu").Status);
            Assert.Equal(node1, _db.Db.Queryable<WfFlowInstance>().InSingle(id).CurrentNodeId);

            // 王五（Waiting）不能先操作：应当任务已处理/无权限（任务未激活）
            // 张三通过 → 王五被激活为 Pending，流程不推进
            _engine.Approve(GetTask(id, node1, "zhangsan").TaskId, "同意1", _db.Uid("zhangsan"));
            Assert.Equal((int)WfTaskStatus.Done, GetTask(id, node1, "zhangsan").Status);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, node1, "wangwu").Status);
            Assert.Equal(node1, _db.Db.Queryable<WfFlowInstance>().InSingle(id).CurrentNodeId); // 仍未推进

            // 王五通过 → 末位，节点完成，推进到 node2
            _engine.Approve(GetTask(id, node1, "wangwu").TaskId, "同意2", _db.Uid("wangwu"));
            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal(node2, saved.CurrentNodeId);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, node2, "lisi").Status);
        }

        [Fact]
        public void 依次审批_任一驳回则整节点驳回_回退到发起人()
        {
            var flowId = _db.AddDefinition("SEQREJ", "依次驳回");
            // 默认 RejectStrategy=0（驳回发起人）
            var node1 = _db.AddNode(flowId, "依次审", (int)WfNodeType.Audit, (int)WfApproverType.User,
                $"{_db.Uids("zhangsan")},{_db.Uids("wangwu")}", 1, (int)WfSignType.Sequential);
            var node2 = _db.AddNode(flowId, "二级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("lisi"), 2);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            // 张三驳回 → 整实例驳回（默认策略0）
            _engine.Reject(GetTask(id, node1, "zhangsan").TaskId, "不行", _db.Uid("zhangsan"));
            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal((int)WfInstanceStatus.Rejected, saved.Status);
        }

        [Fact]
        public void 驳回策略_驳回到上一节点_流程保持审批中且上一节点重新激活()
        {
            // A(审批) -> B(审批, 驳回策略=上一节点) -> C(审批)
            var flowId = _db.AddDefinition("REJPREV", "驳回到上一步");
            var nodeA = _db.AddNode(flowId, "A审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("zhangsan"), 1);
            var nodeB = _db.AddNode(flowId, "B审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("lisi"), 2, rejectStrategy: (int)WfRejectStrategy.ToPrevNode);
            var nodeC = _db.AddNode(flowId, "C审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("wangwu"), 3);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });
            _engine.Approve(GetTask(id, nodeA, "zhangsan").TaskId, "同意", _db.Uid("zhangsan"));
            Assert.Equal(nodeB, _db.Db.Queryable<WfFlowInstance>().InSingle(id).CurrentNodeId);

            // B 驳回 → 回退到 A，流程保持 Approval，A 重新 Pending
            _engine.Reject(GetTask(id, nodeB, "lisi").TaskId, "退回上一节点", _db.Uid("lisi"));
            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal((int)WfInstanceStatus.Approval, saved.Status);
            Assert.Equal(nodeA, saved.CurrentNodeId);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, nodeA, "zhangsan").Status);

            // 重新走完 A -> B -> C 直至通过
            _engine.Approve(GetTask(id, nodeA, "zhangsan").TaskId, "再同意", _db.Uid("zhangsan"));
            Assert.Equal(nodeB, _db.Db.Queryable<WfFlowInstance>().InSingle(id).CurrentNodeId);
            _engine.Approve(GetTask(id, nodeB, "lisi").TaskId, "同意", _db.Uid("lisi"));
            var finalSaved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal(nodeC, finalSaved.CurrentNodeId);
        }
    }
}
