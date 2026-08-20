using System.Linq;
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
    /// <summary>
    /// 排他选路语义回归（2026-08-20 修正）：
    /// 条件网关 / 普通节点后接条件分支必须是「排他只选一路」——第一条命中的条件边优先，
    /// 无条件出边（默认分支）是严格 fallback，绝不与命中条件并列后"取第一个"。
    /// 旧实现 ResolveNextNodes 会把「所有命中条件边 + 默认分支」全部加入 result 再取 nexts[0]，
    /// 导致：① 默认分支 Sort 靠前时错误地走默认而非条件分支；② 普通节点后多条件同时命中会非预期并行分叉。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineConditionExclusiveTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineConditionExclusiveTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("applier", "boss");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>(), Mock.Of<IWfWebhookService>(), Mock.Of<IWfAiService>());
        }

        /// <summary>
        /// 核心回归：默认分支 Sort 排在最前，但条件边也命中 → 必须走第一条命中的条件分支，绝不走默认分支。
        /// 旧实现把默认分支与命中条件边并列进 result，Sort=0 的默认分支被 nexts[0] 取走 → 错误走默认。
        /// </summary>
        [Fact]
        public void 条件命中且默认分支排前_走条件分支而非默认分支()
        {
            var flowId = _db.AddDefinition("EXCL_DEFAULT_FIRST", "排他-默认排前但条件命中");
            var cond = _db.AddNode(flowId, "条件", (int)WfNodeType.Condition, (int)WfApproverType.User, _db.Uids("boss"), 1);
            var branchA = _db.AddNode(flowId, "分支A(Gt100)", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 2);
            var branchB = _db.AddNode(flowId, "分支B(Gt500)", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 3);
            var branchC = _db.AddNode(flowId, "默认分支C", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 4);
            // 默认分支（无条件）Sort=0 排最前；两条条件边排在后面
            _db.AddLink(flowId, cond, branchC, null, 0);
            _db.AddLink(flowId, cond, branchA, "{\"field\":\"amount\",\"op\":3,\"value\":\"100\"}", 1);
            _db.AddLink(flowId, cond, branchB, "{\"field\":\"amount\",\"op\":3,\"value\":\"500\"}", 2);

            // amount=2000：A(Gt100) 与 B(Gt500) 均命中；默认分支也"无条件成立"。
            var id = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "t",
                ApplyUser = "applier",
                ApplyUserId = _db.Uid("applier"),
                FormContent = "{\"amount\":\"2000\"}",
            });

            // 必须只激活第一条命中的条件分支 A；默认分支 C 与 B 都不应生成待办
            Assert.True(_db.Db.Queryable<WfFlowTask>()
                .Any(t => t.InstanceId == id && t.NodeId == branchA && t.Status == (int)WfTaskStatus.Pending));
            Assert.False(_db.Db.Queryable<WfFlowTask>()
                .Any(t => t.InstanceId == id && t.NodeId == branchB));
            Assert.False(_db.Db.Queryable<WfFlowTask>()
                .Any(t => t.InstanceId == id && t.NodeId == branchC));
        }

        /// <summary>
        /// 多条件同时命中 → 排他只走第一条命中，绝不并行分叉（每实例只生成一个待办节点）。
        /// </summary>
        [Fact]
        public void 多条件同时命中_只激活第一条不并行分叉()
        {
            var flowId = _db.AddDefinition("EXCL_NO_FORK", "排他-多条件命中不分叉");
            var cond = _db.AddNode(flowId, "条件", (int)WfNodeType.Condition, (int)WfApproverType.User, _db.Uids("boss"), 1);
            var branchA = _db.AddNode(flowId, "分支A", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 2);
            var branchB = _db.AddNode(flowId, "分支B", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 3);
            var branchC = _db.AddNode(flowId, "分支C", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 4);
            _db.AddLink(flowId, cond, branchA, "{\"field\":\"amount\",\"op\":3,\"value\":\"100\"}", 0);
            _db.AddLink(flowId, cond, branchB, "{\"field\":\"amount\",\"op\":3,\"value\":\"500\"}", 1);
            _db.AddLink(flowId, cond, branchC, "{\"field\":\"amount\",\"op\":3,\"value\":\"1000\"}", 2);

            var id = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "t",
                ApplyUser = "applier",
                ApplyUserId = _db.Uid("applier"),
                FormContent = "{\"amount\":\"2000\"}", // A/B/C 三条全命中
            });

            // 排他：仅 A（第一条命中）生成待办；B、C 不生成任务
            var tasks = _db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id).ToList();
            Assert.Single(tasks);
            Assert.Equal(branchA, tasks[0].NodeId);
            Assert.Equal((int)WfTaskStatus.Pending, tasks[0].Status);
        }

        /// <summary>
        /// 条件全不满足 → 走默认分支（严格 fallback，唯一出路）。
        /// </summary>
        [Fact]
        public void 条件全不满足_走默认分支()
        {
            var flowId = _db.AddDefinition("EXCL_FALLBACK", "排他-无命中走默认");
            var cond = _db.AddNode(flowId, "条件", (int)WfNodeType.Condition, (int)WfApproverType.User, _db.Uids("boss"), 1);
            var branchA = _db.AddNode(flowId, "分支A", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 2);
            var branchB = _db.AddNode(flowId, "默认B", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 3);
            _db.AddLink(flowId, cond, branchA, "{\"field\":\"amount\",\"op\":3,\"value\":\"5000\"}", 0);
            _db.AddLink(flowId, cond, branchB, null, 1);

            var id = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "t",
                ApplyUser = "applier",
                ApplyUserId = _db.Uid("applier"),
                FormContent = "{\"amount\":\"100\"}",
            });

            Assert.True(_db.Db.Queryable<WfFlowTask>()
                .Any(t => t.InstanceId == id && t.NodeId == branchB && t.Status == (int)WfTaskStatus.Pending));
            // 被跳过的条件分支仅留 Skipped 痕迹（防 join 死锁），绝不生成 Pending 待办
            Assert.False(_db.Db.Queryable<WfFlowTask>()
                .Any(t => t.InstanceId == id && t.NodeId == branchA && t.Status == (int)WfTaskStatus.Pending));
        }

        /// <summary>
        /// 条件网关后接普通审批节点：走条件分支生成待办后，审批通过再走该分支下一条，全程只一路。
        /// </summary>
        [Fact]
        public void 排他网关选路后_后续审批沿单分支推进()
        {
            var flowId = _db.AddDefinition("EXCL_CHAIN", "排他-选路后续推进");
            var cond = _db.AddNode(flowId, "条件", (int)WfNodeType.Condition, (int)WfApproverType.User, _db.Uids("boss"), 1);
            var branchA = _db.AddNode(flowId, "分支A", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 2);
            var branchB = _db.AddNode(flowId, "分支B", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 3);
            var tail = _db.AddNode(flowId, "汇聚尾节点", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 4);
            _db.AddLink(flowId, cond, branchA, "{\"field\":\"amount\",\"op\":3,\"value\":\"100\"}", 0);
            _db.AddLink(flowId, cond, branchB, null, 1);
            _db.AddLink(flowId, branchA, tail, null, 0);
            _db.AddLink(flowId, branchB, tail, null, 0);

            var id = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "t",
                ApplyUser = "applier",
                ApplyUserId = _db.Uid("applier"),
                FormContent = "{\"amount\":\"2000\"}",
            });

            // 先到分支A
            var taskA = _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == id && t.NodeId == branchA);
            Assert.Equal((int)WfTaskStatus.Pending, taskA.Status);

            // 通过分支A → 沿 A 的下一条（tail）推进，不经过 B
            _engine.Approve(taskA.TaskId, "ok", _db.Uid("boss"));
            Assert.True(_db.Db.Queryable<WfFlowTask>()
                .Any(t => t.InstanceId == id && t.NodeId == tail && t.Status == (int)WfTaskStatus.Pending));
            Assert.False(_db.Db.Queryable<WfFlowTask>()
                .Any(t => t.InstanceId == id && t.NodeId == branchB));
        }
    }
}
