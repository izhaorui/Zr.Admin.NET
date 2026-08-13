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

        // 三级流程：nodeA(一级,zhangsan) → cond(条件网关) → nodeB(二级,lisi)
        // 跳转到条件网关后，网关本身不生成任务，应自动顺延到其后 nodeB 生成待办。
        [Fact]
        public async Task AdminJump_跳到条件网关_顺延到后续审批节点生成待办()
        {
            var flowId = _db.AddDefinition("JUMP_COND", "跳转-条件网关");
            var nodeA = _db.AddNode(flowId, "一级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("zhangsan"), 1);
            var cond = _db.AddNode(flowId, "条件", (int)WfNodeType.Condition, (int)WfApproverType.User, _db.Uids("zhangsan"), 2);
            var nodeB = _db.AddNode(flowId, "二级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("lisi"), 3);
            _db.AddLink(flowId, nodeA, cond);
            _db.AddLink(flowId, cond, nodeB); // 默认分支（无条件）
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            await _engine.AdminJump(id, cond, _db.Uid("admin"), "跳到条件网关");

            // 原一级待办被跳过
            Assert.Equal((int)WfTaskStatus.Skipped, GetTask(id, nodeA, "zhangsan").Status);
            // 条件网关自身不生成任务（无待办），流程已顺延到 nodeB 生成 lisi 待办
            Assert.Null(_db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id && t.NodeId == cond).ToList().FirstOrDefault());
            var tB = _db.Db.Queryable<WfFlowTask>().Where(x => x.InstanceId == id && x.NodeId == nodeB && x.Assignee == "lisi").ToList().FirstOrDefault();
            Assert.NotNull(tB);
            Assert.Equal((int)WfTaskStatus.Pending, tB.Status);
        }

        // 并行分叉流程：nodeA(一级) → fork(并行分叉) → nodeB(lisi) / nodeC(wangwu)
        // 跳转到 fork 网关应 fork 出两条出边，各自生成一个 Pending 待办。
        [Fact]
        public async Task AdminJump_跳到并行分叉网关_fork出全部分支待办()
        {
            var flowId = _db.AddDefinition("JUMP_FORK", "跳转-并行分叉");
            var nodeA = _db.AddNode(flowId, "一级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("zhangsan"), 1);
            var fork = _db.AddNode(flowId, "分叉", (int)WfNodeType.ParallelFork, (int)WfApproverType.User, _db.Uids("zhangsan"), 2);
            var nodeB = _db.AddNode(flowId, "分支B", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("lisi"), 3);
            var nodeC = _db.AddNode(flowId, "分支C", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("wangwu"), 4);
            _db.AddLink(flowId, nodeA, fork);
            _db.AddLink(flowId, fork, nodeB);
            _db.AddLink(flowId, fork, nodeC);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            await _engine.AdminJump(id, fork, _db.Uid("admin"), "跳到分叉");

            Assert.Equal((int)WfTaskStatus.Skipped, GetTask(id, nodeA, "zhangsan").Status);
            // 分叉网关自身不生成任务，两条出边各生成一个 Pending 待办
            Assert.Null(_db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id && t.NodeId == fork).ToList().FirstOrDefault());
            Assert.Equal((int)WfTaskStatus.Pending, _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == id && t.NodeId == nodeB && t.Assignee == "lisi").Status);
            Assert.Equal((int)WfTaskStatus.Pending, _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == id && t.NodeId == nodeC && t.Assignee == "wangwu").Status);
        }

        // 流程：nodeA(一级) → cc(抄送) → nodeB(二级)
        // 跳转到抄送节点应瞬时生成 Cc 任务(Skipped) 并顺延到 nodeB 生成审批待办。
        [Fact]
        public async Task AdminJump_跳到抄送节点_生成抄送并顺延到后续待办()
        {
            var flowId = _db.AddDefinition("JUMP_CC", "跳转-抄送");
            var nodeA = _db.AddNode(flowId, "一级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("zhangsan"), 1);
            var cc = _db.AddNode(flowId, "抄送", (int)WfNodeType.Cc, (int)WfApproverType.User, _db.Uids("lisi"), 2);
            var nodeB = _db.AddNode(flowId, "二级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("wangwu"), 3);
            _db.AddLink(flowId, nodeA, cc);
            _db.AddLink(flowId, cc, nodeB);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            await _engine.AdminJump(id, cc, _db.Uid("admin"), "跳到抄送");

            Assert.Equal((int)WfTaskStatus.Skipped, GetTask(id, nodeA, "zhangsan").Status);
            // 抄送节点生成 Cc 任务（瞬时完成 Skipped）
            var ccTask = _db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id && t.NodeId == cc).ToList().FirstOrDefault();
            Assert.NotNull(ccTask);
            Assert.Equal((int)WfTaskStatus.Skipped, ccTask.Status);
            // 顺延到后续 nodeB 生成 wangwu 待办
            Assert.Equal((int)WfTaskStatus.Pending, _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == id && t.NodeId == nodeB && t.Assignee == "wangwu").Status);
        }

        // 流程：nodeA(一级) → end(结束节点)
        // 跳转到结束节点流程应直接结束（Status=Approved），无新待办生成。
        [Fact]
        public async Task AdminJump_跳到结束节点_流程直接结束()
        {
            var flowId = _db.AddDefinition("JUMP_END", "跳转-结束");
            var nodeA = _db.AddNode(flowId, "一级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("zhangsan"), 1);
            var end = _db.AddNode(flowId, "结束", (int)WfNodeType.End, (int)WfApproverType.User, _db.Uids("zhangsan"), 2);
            _db.AddLink(flowId, nodeA, end);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            await _engine.AdminJump(id, end, _db.Uid("admin"), "跳到结束");

            Assert.Equal((int)WfTaskStatus.Skipped, GetTask(id, nodeA, "zhangsan").Status);
            // 结束节点不生成任务，流程直接置为已通过
            Assert.Null(_db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id && t.NodeId == end).ToList().FirstOrDefault());
            Assert.Equal((int)WfInstanceStatus.Approved, _db.Db.Queryable<WfFlowInstance>().InSingle(id).Status);
        }

        // 并行分叉流程：fork(7，首节点) → nodeB(lisi) / nodeC(wangwu) → join(8) → nodeD(admin)
        // 启动即分叉，活动集 CurrentNodeIds=[nodeB,nodeC]（两并行分支同时活动）。
        // 并行态下 AdminJump 跳到 nodeD，必须清空旧活动集：CurrentNodeIds 应只剩 nodeD，
        // 不能残留 nodeB/nodeC（旧分支待办已 Skipped），否则 CurrentNodeId=Min 会取到已跳过节点。
        [Fact]
        public async Task AdminJump_并行态跳转_清空旧活动集只留目标节点()
        {
            var flowId = _db.AddDefinition("JUMP_PARALLEL", "跳转-并行态");
            var fork = _db.AddNode(flowId, "分叉", (int)WfNodeType.ParallelFork, (int)WfApproverType.User, _db.Uids("zhangsan"), 1);
            var nodeB = _db.AddNode(flowId, "分支B", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("lisi"), 2);
            var nodeC = _db.AddNode(flowId, "分支C", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("wangwu"), 3);
            var join = _db.AddNode(flowId, "汇聚", (int)WfNodeType.ParallelJoin, (int)WfApproverType.User, _db.Uids("zhangsan"), 4);
            var nodeD = _db.AddNode(flowId, "二级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("admin"), 5);
            _db.AddLink(flowId, fork, nodeB);
            _db.AddLink(flowId, fork, nodeC);
            _db.AddLink(flowId, nodeB, join);
            _db.AddLink(flowId, nodeC, join);
            _db.AddLink(flowId, join, nodeD);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            // 前置断言：启动即分叉，活动集应为 [nodeB, nodeC]
            var before = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            var beforeActive = Newtonsoft.Json.JsonConvert.DeserializeObject<long[]>(before.CurrentNodeIds).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { nodeB, nodeC }.OrderBy(x => x).ToArray(), beforeActive);

            await _engine.AdminJump(id, nodeD, _db.Uid("admin"), "跳过并行分支");

            // 原并行分支待办被跳过
            Assert.Equal((int)WfTaskStatus.Skipped, GetTask(id, nodeB, "lisi").Status);
            Assert.Equal((int)WfTaskStatus.Skipped, GetTask(id, nodeC, "wangwu").Status);
            // 目标节点生成 admin 待办
            Assert.Equal((int)WfTaskStatus.Pending, _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == id && t.NodeId == nodeD && t.Assignee == "admin").Status);
            // 活动集被清空后只含目标节点 nodeD（不残留 nodeB/nodeC），单值指针=nodeD
            var after = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            var afterActive = Newtonsoft.Json.JsonConvert.DeserializeObject<long[]>(after.CurrentNodeIds).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { nodeD }, afterActive);
            Assert.Equal(nodeD, after.CurrentNodeId);
        }

        // 用户场景：并行节点[并行1,并行2](并行分组) → 并行汇聚(8) → 抄送1 → 结束。
        // 管理员跳转到 并行1（分组内成员节点）。按业界主流（Activiti/钉钉/飞书）的单令牌跳转语义（A 方案）：
        // 只重新激活目标节点 并行1，组内其它分支 并行2 的未完成任务被置 Skipped、不再重新待办；
        // 目标分支通过后并行汇聚判定组内其余分支已"完成"（Skipped）→ 放行到 抄送1 → 流程通过。
        // 不能整组重新 fork（会让 并行2 也重新待办并高亮，用户被迫重批本无问题分支）。
        [Fact]
        public async Task AdminJump_跳转并行分组节点_只激活目标分支不整组重新fork()
        {
            var flowId = _db.AddDefinition("JUMP_PGROUP", "跳转-并行分组");
            var fork = _db.AddNode(flowId, "分叉", (int)WfNodeType.ParallelFork, (int)WfApproverType.User, _db.Uids("zhangsan"), 1);
            var p1 = _db.AddNode(flowId, "并行1", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("lisi"), 2, parallelGroup: 1);
            var p2 = _db.AddNode(flowId, "并行2", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("wangwu"), 3, parallelGroup: 1);
            var join = _db.AddNode(flowId, "并行汇聚", (int)WfNodeType.ParallelJoin, (int)WfApproverType.User, _db.Uids("zhangsan"), 4);
            var cc = _db.AddNode(flowId, "抄送1", (int)WfNodeType.Cc, (int)WfApproverType.User, _db.Uids("admin"), 5);
            _db.AddLink(flowId, fork, p1);
            _db.AddLink(flowId, fork, p2);
            _db.AddLink(flowId, p1, join);
            _db.AddLink(flowId, p2, join);
            _db.AddLink(flowId, join, cc);
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            // 前置：启动即分叉，并行1/并行2 都有 Pending 待办
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, p1, "lisi").Status);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, p2, "wangwu").Status);

            // 管理员跳转到并行1
            await _engine.AdminJump(id, p1, _db.Uid("admin"), "重走并行1");

            // 关键断言：跳转后只激活目标 并行1（生成 Pending 待办）；
            // 并行2 不重新激活（原有任务全部 Skipped，无 Pending 待办）。
            Assert.Equal((int)WfTaskStatus.Pending, _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == id && t.NodeId == p1 && t.Assignee == "lisi" && t.Status == (int)WfTaskStatus.Pending).Status);
            // 并行2 不存在 Pending 任务（整组未重新 fork）
            Assert.False(_db.Db.Queryable<WfFlowTask>().Any(t => t.InstanceId == id && t.NodeId == p2 && t.Status == (int)WfTaskStatus.Pending));
            // 活动集 = 仅 [并行1]
            var inst = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal((int)WfInstanceStatus.Approval, inst.Status);
            var active = Newtonsoft.Json.JsonConvert.DeserializeObject<long[]>(inst.CurrentNodeIds).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { p1 }, active);
            Assert.Equal(p1, inst.CurrentNodeId);

            // 只需 并行1 通过 → 并行汇聚(8)放行（并行2 已 Skipped 视为完成）→ 抄送1 → 流程通过
            var p1Task = _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == id && t.NodeId == p1 && t.Assignee == "lisi" && t.Status == (int)WfTaskStatus.Pending);
            _engine.Approve(p1Task.TaskId, "ok", _db.Uid("lisi"));
            var done = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal((int)WfInstanceStatus.Approved, done.Status);
            var ccTask = _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == id && t.NodeId == cc);
            Assert.Equal((int)WfTaskStatus.Skipped, ccTask.Status);
        }

        // 挂起态跳转：Suspend → AdminJump 后，DB Status 必须恢复为 Approval（持久化），
        // 否则跳转后实例在库里仍是 Suspended，恢复链路与实际流转态不一致。
        [Fact]
        public async Task AdminJump_挂起态跳转_持久化恢复为审批中()
        {
            (long id, long node1) = StartRunning();
            await _engine.AdminSuspend(id, _db.Uid("admin"), "暂停");
            var flowId = _db.Db.Queryable<WfFlowNode>().Where(n => n.NodeId == node1).Select(n => n.FlowId).First();
            var node2 = _db.Db.Queryable<WfFlowNode>().Where(n => n.FlowId == flowId && n.NodeOrder == 2).Select(n => n.NodeId).First();

            await _engine.AdminJump(id, node2, _db.Uid("admin"), "挂起后跳转");

            // DB 状态恢复为审批中（此前 bug：只改内存不落库，库里仍是 Suspended）
            var saved = _db.Db.Queryable<WfFlowInstance>().InSingle(id);
            Assert.Equal((int)WfInstanceStatus.Approval, saved.Status);
            // 目标节点生成待办
            Assert.Equal((int)WfTaskStatus.Pending, _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == id && t.NodeId == node2 && t.Assignee == "lisi").Status);
        }
    }
}
