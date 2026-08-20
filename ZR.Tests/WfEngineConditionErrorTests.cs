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
    /// 连线条件失败语义回归（2026-08-20 收紧）：
    /// 区分「条件不满足（业务 false）」与「条件配置错误（系统无法判断，抛异常并回滚）」。
    ///
    /// 背景：旧实现把 JSON 解析失败 / 字段缺失 / 字段为空 / 比较失败 一律折叠为 false。
    /// 危险场景：排他条件网关若所有出边都因「配置错误」而不满足，会被误判为「无满足分支 → 流程正常结束」，
    /// 一个配错的流程看起来像"正常审批完成"。因此配置错误必须抛异常并触发事务回滚，而非静默结束。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineConditionErrorTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineConditionErrorTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("applier", "boss");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>(), Mock.Of<IWfWebhookService>(), Mock.Of<IWfAiService>());
        }

        /// <summary>
        /// 配置错误（条件字段不在表单中）且无默认分支 → 抛异常并回滚，绝不误判为流程正常结束。
        /// 这是本次收紧的核心安全语义。
        /// </summary>
        [Fact]
        public void 全部条件分支因配置错误不满足_抛异常并回滚_不误判完成()
        {
            var flowId = _db.AddDefinition("COND_ERR_NOFIELD", "条件-字段不存在");
            var cond = _db.AddNode(flowId, "条件", (int)WfNodeType.Condition, (int)WfApproverType.User, _db.Uids("boss"), 1);
            var branchA = _db.AddNode(flowId, "分支A", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 2);
            var branchB = _db.AddNode(flowId, "分支B", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 3);
            // 两条出边都带条件，且引用表单中不存在的字段 notExists；无默认分支。
            _db.AddLink(flowId, cond, branchA, "{\"field\":\"notExists\",\"op\":3,\"value\":\"1000\"}", 0);
            _db.AddLink(flowId, cond, branchB, "{\"field\":\"notExists\",\"op\":4,\"value\":\"5000\"}", 1);

            // 发起即抛异常：RunInTx 把事务内异常包装成"发起申请失败"（ex.Message），
            // 真正的配置错误原因进 LogMsg。关键安全性质是「抛异常 + 事务回滚」，绝不静默完成。
            var ex = Assert.Throws<CustomException>(() => _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "t",
                ApplyUser = "applier",
                ApplyUserId = _db.Uid("applier"),
                FormContent = "{\"amount\":\"500\"}",
            }));
            Assert.Contains("发起申请失败", ex.Message);
            Assert.Contains("条件配置错误", ex.LogMsg);

            // 事务回滚：该 FlowId 下无任何实例落库——绝不可能留下"已通过/正常结束"的误导记录
            Assert.False(_db.Db.Queryable<WfFlowInstance>().Any(i => i.FlowId == flowId));
        }

        /// <summary>
        /// 配置错误（连线条件 JSON 无法解析）→ 抛异常并回滚。
        /// </summary>
        [Fact]
        public void 连线条件JSON解析失败_抛异常并回滚()
        {
            var flowId = _db.AddDefinition("COND_ERR_JSON", "条件-JSON错误");
            var cond = _db.AddNode(flowId, "条件", (int)WfNodeType.Condition, (int)WfApproverType.User, _db.Uids("boss"), 1);
            var branchA = _db.AddNode(flowId, "分支A", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 2);
            _db.AddLink(flowId, cond, branchA, "{this is not valid json", 0);

            var ex = Assert.Throws<CustomException>(() => _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "t",
                ApplyUser = "applier",
                ApplyUserId = _db.Uid("applier"),
                FormContent = "{\"amount\":\"500\"}",
            }));
            Assert.Contains("发起申请失败", ex.Message);
            Assert.Contains("条件配置错误", ex.LogMsg);
        }

        /// <summary>
        /// 业务不满足（字段存在、值 500 不满足 Gt 1000）且存在默认分支 → 走默认分支，不抛异常。
        /// 验证收紧语义没有破坏正常的分支选择。
        /// </summary>
        [Fact]
        public void 条件业务不满足_走默认分支_不抛异常()
        {
            var flowId = _db.AddDefinition("COND_OK_DEFAULT", "条件-业务不满足走默认");
            var cond = _db.AddNode(flowId, "条件", (int)WfNodeType.Condition, (int)WfApproverType.User, _db.Uids("boss"), 1);
            var branchA = _db.AddNode(flowId, "分支A", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 2);
            var branchB = _db.AddNode(flowId, "分支B", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 3);
            // 条件出边 Gt 1000（amount=500 → 业务不满足），另一条为无条件默认分支。
            _db.AddLink(flowId, cond, branchA, "{\"field\":\"amount\",\"op\":3,\"value\":\"1000\"}", 0);
            _db.AddLink(flowId, cond, branchB, null, 1);

            // 不应抛异常
            var id = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "t",
                ApplyUser = "applier",
                ApplyUserId = _db.Uid("applier"),
                FormContent = "{\"amount\":\"500\"}",
            });

            // 走默认分支 branchB：生成 boss 待办
            Assert.True(_db.Db.Queryable<WfFlowTask>()
                .Any(t => t.InstanceId == id && t.NodeId == branchB && t.Status == (int)WfTaskStatus.Pending));
            Assert.Equal((int)WfInstanceStatus.Approval, _db.Db.Queryable<WfFlowInstance>().InSingle(id).Status);
        }

        /// <summary>
        /// 条件业务满足（amount=2000 满足 Gt 1000）→ 走条件分支 A，不抛异常。
        /// </summary>
        [Fact]
        public void 条件业务满足_走条件分支_不抛异常()
        {
            var flowId = _db.AddDefinition("COND_OK_HIT", "条件-业务满足走条件分支");
            var cond = _db.AddNode(flowId, "条件", (int)WfNodeType.Condition, (int)WfApproverType.User, _db.Uids("boss"), 1);
            var branchA = _db.AddNode(flowId, "分支A", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 2);
            var branchB = _db.AddNode(flowId, "分支B", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("boss"), 3);
            _db.AddLink(flowId, cond, branchA, "{\"field\":\"amount\",\"op\":3,\"value\":\"1000\"}", 0);
            _db.AddLink(flowId, cond, branchB, null, 1);

            var id = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "t",
                ApplyUser = "applier",
                ApplyUserId = _db.Uid("applier"),
                FormContent = "{\"amount\":\"2000\"}",
            });

            Assert.True(_db.Db.Queryable<WfFlowTask>()
                .Any(t => t.InstanceId == id && t.NodeId == branchA && t.Status == (int)WfTaskStatus.Pending));
        }
    }
}
