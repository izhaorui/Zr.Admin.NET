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
    /// 工作流引擎「并发竞态 / 幂等性」稳定性测试。
    ///
    /// 生产环境最典型的竞态：同一个或签节点，多个审批人几乎同时点击「通过」。
    /// 引擎以 WfFlowTask.Status=Pending 作为乐观锁——二次操作命中已 Done 的任务会抛「该任务已处理」，
    /// 且或签节点在头部有「节点已完成则跳过同节点其余待办，避免重复流转下一节点」的防护
    /// （见 WfEngineService.Approve 与 CompleteNodeIfNeeded）。
    ///
    /// 注意：测试夹具为单连接 SQLite（WAL 模式），Task.WhenAll 在此仍主要串行化执行，
    /// 但足以验证「重复/并发审批不产生双流转、不抛未捕获异常、状态最终一致」。
    /// 真正的多连接行锁竞态需在生产 SQL Server 下以集成测试方式验证，本类为其提供单元级防线。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineConcurrencyTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineConcurrencyTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("applicant", "auditorA", "auditorB", "auditorC", "boss");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
        }

        private WfFlowInstance GetInstance(long id) =>
            _db.Db.Queryable<WfFlowInstance>().InSingle(id);

        private System.Collections.Generic.List<WfFlowTask> GetTasks(long instanceId) =>
            _db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == instanceId).ToList();

        /// <summary>
        /// 或签节点（A/B/C 任一通过即通过）。三人同时发起 Approve，
        /// 验证：① 流程最终正常结束（Approved）；② 三个任务都标记 Done（无遗漏）；
        /// ③ 不会因重复流转导致「下一节点待办被重复创建」或抛未捕获异常。
        /// </summary>
        [Fact]
        public async Task OrSign_三人并发全部通过_流程正常结束且不重复流转()
        {
            var flowId = _db.AddDefinition("CONC_OR", "并发或签");
            var audit = _db.AddNode(flowId, "会签审批", (int)WfNodeType.Audit, (int)WfApproverType.User,
                $"{_db.Uid("auditorA")},{_db.Uid("auditorB")},{_db.Uid("auditorC")}", 1,
                signType: (int)WfSignType.Or); // 或签：一人通过即通过
            var boss = _db.AddNode(flowId, "老板确认", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 2);
            _db.AddLink(flowId, audit, boss);

            var instance = new WfFlowInstance { FlowId = flowId, Title = "并发测试", ApplyUser = "applicant", ApplyUserId = _db.Uid("applicant") };
            var id = _engine.Start(instance);

            var tasks = GetTasks(id).Where(t => t.NodeId == audit).ToList();
            Assert.Equal(3, tasks.Count);

            // 三人几乎同时点通过。或签节点一人通过即推进，后续命中已处理任务会抛
            // 「该任务已处理」（乐观锁防护），这是预期行为而非故障。
            var exA = Task.Run(() => _engine.Approve(tasks[0].TaskId, "ok", _db.Uid("auditorA")));
            var exB = Task.Run(() => _engine.Approve(tasks[1].TaskId, "ok", _db.Uid("auditorB")));
            var exC = Task.Run(() => _engine.Approve(tasks[2].TaskId, "ok", _db.Uid("auditorC")));

            // 等待全部任务执行完毕（各自要么成功、要么受控抛「已处理」）
            await Task.WhenAll(Task.WhenAny(exA, Task.Delay(5000)), Task.WhenAny(exB, Task.Delay(5000)), Task.WhenAny(exC, Task.Delay(5000)));

            // 所有 Approve 调用都不应抛出非业务异常（异常要么是 CustomException「已处理」）
            foreach (var t in new[] { exA, exB, exC })
            {
                if (t.IsFaulted)
                    Assert.IsType<CustomException>(t.Exception?.InnerException ?? t.Exception);
            }

            var allTasks = GetTasks(id);
            // 三个或签任务都应被处理（首过者 Done；其余被乐观锁置 Done 或标记已完成）
            Assert.All(allTasks.Where(t => t.NodeId == audit), t => Assert.NotEqual((int)WfTaskStatus.Pending, t.Status));

            // 或签一人通过即推进到 boss 节点，boss 节点应有且仅有 1 条 Pending 待办（不重复）
            var bossTasks = allTasks.Where(t => t.NodeId != audit).ToList();
            Assert.Single(bossTasks);
            Assert.Equal((int)WfTaskStatus.Pending, bossTasks[0].Status);
            Assert.Equal("boss", bossTasks[0].Assignee);
        }

        /// <summary>
        /// 同一任务被「重复」提交两次 Approve（网络重发/用户双击场景）。
        /// 首次成功后任务已 Done，第二次必须抛「该任务已处理」，而非静默重复流转或抛未捕获异常。
        /// 此即引擎对并发/重放的乐观锁防线。
        /// </summary>
        [Fact]
        public void 同一任务重复提交Approve_第二次必须抛已处理()
        {
            var flowId = _db.AddDefinition("DUP_APPROVE", "重复审批");
            var audit = _db.AddNode(flowId, "主管审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("auditorA"), 1);

            var instance = new WfFlowInstance { FlowId = flowId, Title = "重复审批测试", ApplyUser = "applicant", ApplyUserId = _db.Uid("applicant") };
            var id = _engine.Start(instance);

            var task = GetTasks(id).Single();
            _engine.Approve(task.TaskId, "通过", _db.Uid("auditorA"));

            // 第二次重复提交：必须抛「该任务已处理」，而非再次流转或静默成功
            var ex = Assert.Throws<CustomException>(() => _engine.Approve(task.TaskId, "通过again", _db.Uid("auditorA")));
            Assert.Contains("已处理", ex.Message);

            // 实例不应因重复提交产生额外待办/记录
            Assert.Single(GetTasks(id));
        }

        /// <summary>
        /// 并行撤回竞态：发起后，申请人立即撤回；同时另一线程尝试对首节点 Approve。
        /// 撤回会将实例置为 Withdrawn 并清空待办（或标记 Skipped），Approve 应因任务已非 Pending 而失败，
        /// 不会在已撤回实例上产生「审批通过」的脏状态。
        /// </summary>
        [Fact]
        public async Task 并发_撤回与审批竞态_撤回后审批必须失败且实例不脏()
        {
            var flowId = _db.AddDefinition("CONC_WD", "撤回竞态");
            var audit = _db.AddNode(flowId, "主管审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("auditorA"), 1);

            var instance = new WfFlowInstance { FlowId = flowId, Title = "撤回竞态", ApplyUser = "applicant", ApplyUserId = _db.Uid("applicant") };
            var id = _engine.Start(instance);
            var task = GetTasks(id).Single();

            // 申请人撤回 与 审批人审批 并发。二者谁先谁赢（非确定性竞态），
            // 但无论胜负，另一方必然受控失败（撤回后审批命中非 Pending 任务 / 审批后撤回命中「已审批」），
            // 绝不会出现「未捕获异常」或「脏终态」。
            var withdrawTask = Task.Run(() => _engine.Withdraw(id, _db.Uid("applicant")));
            var approveTask = Task.Run(() => _engine.Approve(task.TaskId, "通过", _db.Uid("auditorA")));

            await Task.WhenAll(Task.WhenAny(withdrawTask, Task.Delay(5000)), Task.WhenAny(approveTask, Task.Delay(5000)));

            // 两个并发操作都不应抛出非业务异常
            if (withdrawTask.IsFaulted)
                Assert.IsType<CustomException>(withdrawTask.Exception?.InnerException ?? withdrawTask.Exception);
            if (approveTask.IsFaulted)
                Assert.IsType<CustomException>(approveTask.Exception?.InnerException ?? approveTask.Exception);

            // 终态必须合法：要么审批推进（Approval，待办在 boss 或仍在 audit），要么撤回（Withdrawn）。
            // 不允许出现「Approved/Rejected 终态却仍有 Pending 待办」的脏状态，也不允许同时两种终态。
            var saved = GetInstance(id);
            var pendingTasks = GetTasks(id).Where(t => t.Status == (int)WfTaskStatus.Pending).ToList();

            if (saved.Status == (int)WfInstanceStatus.Withdrawn)
            {
                // 撤回胜出：所有原待办应已退出 Pending
                Assert.Empty(pendingTasks);
            }
            else
            {
                // 审批胜出（或审批先推进、撤回被拒）：终态须为合法流转态，且无脏 Pending 堆积。
                // 单节点审批胜出即 Approved(1) 直接结束（无 Pending）；多节点场景则为 Approval(0) 且有 1 条待办。
                Assert.True(saved.Status == (int)WfInstanceStatus.Approved
                         || saved.Status == (int)WfInstanceStatus.Approval);
                // 不允许出现「结束态却仍有 Pending 待办」的脏状态
                if (saved.Status == (int)WfInstanceStatus.Approved)
                    Assert.Empty(pendingTasks);
                else
                    Assert.Single(pendingTasks);
            }
        }
    }
}
