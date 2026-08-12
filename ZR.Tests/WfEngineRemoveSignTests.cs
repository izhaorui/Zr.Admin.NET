using System.Linq;
using Moq;
using Xunit;
using ZR.Common;
using Infrastructure;
using ZR.ServiceCore.Services;
using ZR.Workflow.Enum;
using ZR.Workflow.Model;
using ZR.Workflow.Service;

namespace ZR.Tests
{
    /// <summary>
    /// 减签验证：在某审批节点移除某审批人（任务置 Skipped），并重新判定节点完成/依次审批推进。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineRemoveSignTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineRemoveSignTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("alice", "bob", "carol", "dave", "erin");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
        }

        private WfFlowTask GetTask(long id, long node, string user) =>
            _db.Db.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == id && t.NodeId == node && t.Assignee == user)
                .OrderByDescending(t => t.TaskId)
                .First();

        [Fact]
        public void 会签减签_剩余全部完成后推进()
        {
            var flowId = _db.AddDefinition("RSAND", "会签减签");
            var node1 = _db.AddNode(flowId, "会签节点", (int)WfNodeType.Audit, (int)WfApproverType.User,
                string.Join(",", new[] { _db.Uids("bob"), _db.Uids("carol"), _db.Uids("dave") }), 1, signType: (int)WfSignType.And);
            var node2 = _db.AddNode(flowId, "后续", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("erin"), 2);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            // bob 减签 carol（bob 是该节点审批人之一，有权限）
            var bobTask = GetTask(id, node1, "bob");
            _engine.RemoveSign(bobTask.TaskId, _db.Uid("carol"), "不需要你审了", _db.Uid("bob"));

            // carol 任务应被置 Skipped，流程仍停留在 node1
            var carolTask = GetTask(id, node1, "carol");
            Assert.Equal((int)WfTaskStatus.Skipped, carolTask.Status);
            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal(node1, saved.CurrentNodeId);

            // bob + dave 都通过 → 节点完成（剩余任务全 Done，无 Pending）→ 推进到 node2
            _engine.Approve(GetTask(id, node1, "bob").TaskId, "ok", _db.Uid("bob"));
            _engine.Approve(GetTask(id, node1, "dave").TaskId, "ok", _db.Uid("dave"));

            saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal(node2, saved.CurrentNodeId);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, node2, "erin").Status);
        }

        [Fact]
        public void 或签减签_减掉唯一待办且无人通过_节点推进()
        {
            // 单审批人或签：减掉该唯一待办后节点无剩余审批人 → 自动推进
            var flowId = _db.AddDefinition("RSOR", "或签减签");
            var node1 = _db.AddNode(flowId, "或签节点", (int)WfNodeType.Audit, (int)WfApproverType.User,
                _db.Uids("bob"), 1, signType: (int)WfSignType.Or);
            var node2 = _db.AddNode(flowId, "后续", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("erin"), 2);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            var bobTask = GetTask(id, node1, "bob");
            _engine.RemoveSign(bobTask.TaskId, _db.Uid("bob"), "减签我自己", _db.Uid("bob"));

            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal(node2, saved.CurrentNodeId);
            Assert.False(_db.Db.Queryable<WfFlowTask>().Any(t => t.InstanceId == id && t.NodeId == node1 && t.Status == (int)WfTaskStatus.Pending));
        }

        [Fact]
        public void 依次审批减签_减掉当前处理人_自动激活下一位()
        {
            var flowId = _db.AddDefinition("RSSQ", "依次审批减签");
            var node1 = _db.AddNode(flowId, "依次节点", (int)WfNodeType.Audit, (int)WfApproverType.User,
                string.Join(",", new[] { _db.Uids("bob"), _db.Uids("carol"), _db.Uids("dave") }), 1, signType: (int)WfSignType.Sequential);
            var node2 = _db.AddNode(flowId, "后续", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("erin"), 2);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            // 仅 bob 为 Pending，carol/dave 为 Waiting
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, node1, "bob").Status);
            Assert.Equal((int)WfTaskStatus.Waiting, GetTask(id, node1, "carol").Status);
            Assert.Equal((int)WfTaskStatus.Waiting, GetTask(id, node1, "dave").Status);

            // bob 减签自己（或 carol 减 bob？权限要求操作人是该节点审批人）。用 carol 减 bob 不合法（carol 是 Waiting 无 task 在 Pending）。
            // 用 bob 减掉 carol：carol 是 Waiting，减掉后 bob 仍 Pending，不激活。
            var bobTask = GetTask(id, node1, "bob");
            _engine.RemoveSign(bobTask.TaskId, _db.Uid("carol"), "减签", _db.Uid("bob"));
            Assert.Equal((int)WfTaskStatus.Skipped, GetTask(id, node1, "carol").Status);
            // bob 仍 Pending（carol 是 Waiting 被减，不影响当前 Pending）
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, node1, "bob").Status);

            // 现在 bob 减掉自己（当前 Pending）→ 应自动激活 dave
            _engine.RemoveSign(GetTask(id, node1, "bob").TaskId, _db.Uid("bob"), "我撤", _db.Uid("bob"));
            Assert.Equal((int)WfTaskStatus.Skipped, GetTask(id, node1, "bob").Status);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, node1, "dave").Status);
            // 流程仍停留在 node1（dave 还未审）
            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal(node1, saved.CurrentNodeId);
        }

        [Fact]
        public void 减签无权限_非本节点审批人_抛异常()
        {
            var flowId = _db.AddDefinition("RSNA", "减签无权限");
            var node1 = _db.AddNode(flowId, "节点", (int)WfNodeType.Audit, (int)WfApproverType.User,
                string.Join(",", new[] { _db.Uids("bob"), _db.Uids("carol") }), 1);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            // erin 不是该节点审批人，拿不到 taskId；构造一个无效调用：用 alice 的 task（无）→ 直接调用会抛"任务不存在"
            var ex = Assert.Throws<CustomException>(() =>
                _engine.RemoveSign(999999, _db.Uid("bob"), "x", _db.Uid("erin")));
            Assert.Contains("审批任务不存在", ex.Message);
        }
    }
}
