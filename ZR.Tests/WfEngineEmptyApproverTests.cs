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
    /// 空审批人兜底策略验证：节点 ResolveApprovers 解析为空时，
    /// 按节点 EmptyApproverStrategy 决定自动通过或使用默认审批人代为审批。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineEmptyApproverTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineEmptyApproverTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("alice", "zhangsan", "lisi");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
        }

        private WfFlowTask GetTask(long id, long node, string user) =>
            _db.Db.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == id && t.NodeId == node && t.Assignee == user)
                .OrderByDescending(t => t.TaskId)
                .First();

        [Fact]
        public void 空审批人_默认策略AutoPass_节点自动跳过推进到下一节点()
        {
            var flowId = _db.AddDefinition("EMPTYAUTO", "空审批人自动通过");
            // 节点1审批人为空（ApproverId 空），节点2为 lisi
            var node1 = _db.AddNode(flowId, "空审批节点", (int)WfNodeType.Audit, (int)WfApproverType.User, "", 1,
                emptyApproverStrategy: (int)WfEmptyApproverStrategy.AutoPass);
            var node2 = _db.AddNode(flowId, "后续审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("lisi"), 2);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            // 节点1自动跳过：不应有 Pending 待办，活动节点已推进到 node2
            var node1Pending = _db.Db.Queryable<WfFlowTask>()
                .Any(t => t.InstanceId == id && t.NodeId == node1 && t.Status == (int)WfTaskStatus.Pending);
            Assert.False(node1Pending);
            // 自动跳过应留痕一条 Skipped 任务
            Assert.True(_db.Db.Queryable<WfFlowTask>().Any(t => t.InstanceId == id && t.NodeId == node1 && t.Status == (int)WfTaskStatus.Skipped));
            // 实例已推进到 node2 且流程仍在审批中
            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal(node2, saved.CurrentNodeId);
            Assert.Equal((int)WfInstanceStatus.Approval, saved.Status);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, node2, "lisi").Status);
        }

        [Fact]
        public void 空审批人_默认用户策略_使用DefaultApproverId代为审批挂起待办()
        {
            var flowId = _db.AddDefinition("EMPTYDEF", "空审批人指定默认人");
            var node1 = _db.AddNode(flowId, "空审批节点", (int)WfNodeType.Audit, (int)WfApproverType.User, "", 1,
                emptyApproverStrategy: (int)WfEmptyApproverStrategy.DefaultUser, defaultApproverId: _db.Uid("zhangsan"));
            var node2 = _db.AddNode(flowId, "后续审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("lisi"), 2);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            // 默认审批人 zhangsan 应被挂起为 Pending 待办，流程停留在 node1
            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal(node1, saved.CurrentNodeId);
            Assert.Equal((int)WfInstanceStatus.Approval, saved.Status);
            var t = GetTask(id, node1, "zhangsan");
            Assert.Equal((int)WfTaskStatus.Pending, t.Status);

            // zhangsan 代为审批通过 → 推进到 node2
            _engine.Approve(t.TaskId, "代审批通过", _db.Uid("zhangsan"));
            var after = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal(node2, after.CurrentNodeId);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, node2, "lisi").Status);
        }

        [Fact]
        public void 空审批人_默认用户策略但默认人缺失_退化为自动通过()
        {
            var flowId = _db.AddDefinition("EMPTYDEF0", "默认人缺失退化为自动通过");
            // 指定默认审批人为不存在的 userId
            var node1 = _db.AddNode(flowId, "空审批节点", (int)WfNodeType.Audit, (int)WfApproverType.User, "", 1,
                emptyApproverStrategy: (int)WfEmptyApproverStrategy.DefaultUser, defaultApproverId: 999999);
            var node2 = _db.AddNode(flowId, "后续审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("lisi"), 2);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal(node2, saved.CurrentNodeId);
            Assert.False(_db.Db.Queryable<WfFlowTask>().Any(t => t.InstanceId == id && t.NodeId == node1 && t.Status == (int)WfTaskStatus.Pending));
        }
    }
}
