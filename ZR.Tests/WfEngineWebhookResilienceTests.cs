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
    /// 工作流节点 Webhook（外部 HTTP 钩子）容错稳定性测试。
    ///
    /// 引擎对节点钩子的设计是 best-effort（尽力投递、非阻塞）：
    /// 钩子在事务内入队（QueueNodeHook），事务提交成功后由 FlushPendingHooks 以
    /// Task.Run fire-and-forget 投递，投递异常仅记日志、绝不回滚主流程，也不向外抛。
    ///
    /// 重要边界（2026-08-13 澄清）：当前引擎**没有**钩子失败重试机制——
    /// 失败即记日志丢弃（符合 best-effort 契约）。因此本类固化的稳定性语义是：
    /// 「钩子目标不可达/抛异常时，审批流转主流程照常推进、实例状态正确、无未捕获异常外泄」。
    /// 若后续产品要求钩子至少一次投递，应新增重试/死信能力并补对应测试，而非依赖当前实现。
    ///
    /// 无效地址用 http://127.0.0.1:1/ 系列端口（本机几乎无服务监听，连接必然失败抛异常），
    /// 确保钩子投递路径真实命中 catch 分支。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineWebhookResilienceTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        // 一个必然连接失败的地址：端口 1 通常无监听，HttpPostAsync 会抛连接异常
        private const string DeadHookUrl = "http://127.0.0.1:1/workflow/webhook/dead";

        public WfEngineWebhookResilienceTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("applicant", "auditorA", "boss");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
        }

        private WfFlowInstance GetInstance(long id) =>
            _db.Db.Queryable<WfFlowInstance>().InSingle(id);

        private System.Collections.Generic.List<WfFlowTask> GetTasks(long instanceId) =>
            _db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == instanceId).ToList();

        /// <summary>
        /// 进入节点钩子目标不可达：首审批节点配置了 enterHookUrl 指向死地址。
        /// 发起后引擎应：① 正常生成待办、实例进入 Approval；② 钩子投递失败被 catch，
        /// 不向外抛异常（Start 调用本身成功返回）；③ 实例状态正确，不卡死。
        /// </summary>
        [Fact]
        public void 进入钩子不可达_主流程照常推进且Start不抛异常()
        {
            var flowId = _db.AddDefinition("HOOK_DEAD_ENTER", "死钩子进入");
            var audit = _db.AddNode(flowId, "主管审批", (int)WfNodeType.Audit, (int)WfApproverType.User,
                _db.Uids("auditorA"), 1, enterHookUrl: DeadHookUrl);

            var instance = new WfFlowInstance { FlowId = flowId, Title = "钩子死链", ApplyUser = "applicant", ApplyUserId = _db.Uid("applicant") };

            // Start 不应因钩子失败而抛异常
            var id = _engine.Start(instance);

            var saved = GetInstance(id);
            Assert.Equal((int)WfInstanceStatus.Approval, saved.Status);
            Assert.Equal(audit, saved.CurrentNodeId);

            var tasks = GetTasks(id);
            Assert.Single(tasks);
            Assert.Equal((int)WfTaskStatus.Pending, tasks[0].Status);
        }

        /// <summary>
        /// 离开钩子（审批通过后 leaveHookUrl）不可达：审批人 Approve 后，
        /// leave 钩子投递失败必须被吞掉，流程照常推进到下一节点或直接结束，Approve 调用不抛异常。
        /// </summary>
        [Fact]
        public void 离开钩子不可达_审批后仍正常推进()
        {
            var flowId = _db.AddDefinition("HOOK_DEAD_LEAVE", "死钩子离开");
            var audit = _db.AddNode(flowId, "主管审批", (int)WfNodeType.Audit, (int)WfApproverType.User,
                _db.Uids("auditorA"), 1, leaveHookUrl: DeadHookUrl);
            var boss = _db.AddNode(flowId, "老板确认", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 2);
            _db.AddLink(flowId, audit, boss);

            var instance = new WfFlowInstance { FlowId = flowId, Title = "钩子离开死链", ApplyUser = "applicant", ApplyUserId = _db.Uid("applicant") };
            var id = _engine.Start(instance);
            var task = GetTasks(id).Single();

            // Approve 不应因 leave 钩子失败而抛异常
            _engine.Approve(task.TaskId, "通过", _db.Uid("auditorA"));

            // 流程应已推进到 boss 节点（或结束），而非卡在 audit
            var saved = GetInstance(id);
            Assert.NotEqual(audit, saved.CurrentNodeId);
            Assert.Equal((int)WfInstanceStatus.Approval, saved.Status); // 还在审批中（boss 待审），未异常结束

            var bossTasks = GetTasks(id).Where(t => t.NodeId == boss).ToList();
            Assert.Single(bossTasks);
            Assert.Equal((int)WfTaskStatus.Pending, bossTasks[0].Status);
        }

        /// <summary>
        /// 钩子在「并行/连续」多次触发场景下持续失败：多节点都配死钩子，
        /// 整条链路由 Start → Approve → Approve 串行推进，验证每个环节的钩子失败
        /// 都不污染流转状态、不累积异常。此为真实生产「钩子服务长期宕机」的降级验证。
        /// </summary>
        [Fact]
        public async Task 多节点死钩子串联_整链路降级仍正常结束()
        {
            var flowId = _db.AddDefinition("HOOK_DEAD_CHAIN", "死钩子链路");
            var a = _db.AddNode(flowId, "初审", (int)WfNodeType.Audit, (int)WfApproverType.User,
                _db.Uids("auditorA"), 1, enterHookUrl: DeadHookUrl, leaveHookUrl: DeadHookUrl);
            var b = _db.AddNode(flowId, "复审", (int)WfNodeType.Audit, (int)WfApproverType.User,
                _db.Uids("boss"), 2, enterHookUrl: DeadHookUrl, leaveHookUrl: DeadHookUrl);
            _db.AddLink(flowId, a, b);

            var instance = new WfFlowInstance { FlowId = flowId, Title = "死钩子链路", ApplyUser = "applicant", ApplyUserId = _db.Uid("applicant") };
            var id = _engine.Start(instance);

            var taskA = GetTasks(id).Single();
            _engine.Approve(taskA.TaskId, "初审通过", _db.Uid("auditorA"));

            var taskB = GetTasks(id).Single(t => t.NodeId == b);
            _engine.Approve(taskB.TaskId, "复审通过", _db.Uid("boss"));

            // 整链路虽每个节点钩子都失败，但流程应正常结束（Approved）
            var saved = GetInstance(id);
            Assert.Equal((int)WfInstanceStatus.Approved, saved.Status);
            Assert.Null(saved.CurrentNodeId);
            Assert.DoesNotContain(GetTasks(id), t => t.Status == (int)WfTaskStatus.Pending);
        }
    }
}
