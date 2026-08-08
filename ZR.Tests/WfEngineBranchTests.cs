using System;
using System.Linq;
using Infrastructure;
using Moq;
using Newtonsoft.Json;
using Xunit;
using ZR.ServiceCore.Services;
using ZR.Workflow.Enum;
using ZR.Workflow.Model;
using ZR.Workflow.Model.Dto;
using ZR.Workflow.Service;

namespace ZR.Tests
{
    /// <summary>
    /// 工作流流转引擎的「分支 / 并行 / 条件网关 / 抄送 / 节点条件」全场景测试。
    ///
    /// 重点覆盖 2026-08-04 修复的链路：带节点连线（WfNodeLink）时，引擎严格按图建模串联，
    /// 节点无出边即为「流程终点」（绝不可 fallback NodeOrder 顺延），且条件网关可作为流程首节点。
    /// 所有用例均显式建连线，区别于旧测试的 NodeOrder fallback 通道。
    ///
    /// 注意：引擎 ResolveApprovers 以数字 userId 落库，故建节点时 ApproverId 统一用 _db.Uid("用户名")。
    /// </summary>
    [Collection("WfTests")]
    public class WfEngineBranchTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfEngineBranchTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            // 引擎 ResolveApprovers 需到 SysUser 落库审批人，测试库须先存在这些用户
            _db.EnsureUsers("boss", "mgr", "lead", "a", "b", "c", "vip", "cc1", "cc2");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
        }

        private static string Cond(string field, WfConditionOp op, string value) =>
            JsonConvert.SerializeObject(new WfLinkCondition { Field = field, Op = (int)op, Value = value });

        private WfFlowTask GetTask(long instanceId, long nodeId, string assignee) =>
            _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == instanceId && t.NodeId == nodeId && t.Assignee == assignee);

        private WfFlowInstance GetInstance(long instanceId) =>
            _db.Db.Queryable<WfFlowInstance>().InSingle(instanceId);

        #region A. 条件网关分支（本次修复主战场）

        /// <summary>
        /// 复现原始 bug：网关为首节点，金额 100 >= 10 → 走「老板批」，老板批无出边 → 应直接结束（Approved）。
        /// 修复前会错误 fallback 到 NodeOrder 顺延到「经理批」，实例永不结束。
        /// </summary>
        [Fact]
        public void ConditionGateway_首节点_大额命中老板批_老板批无出边_应结束()
        {
            var flowId = _db.AddDefinition("CG1", "金额分流");
            var gw = _db.AddNode(flowId, "金额判断", (int)WfNodeType.Condition, 0, "", 1);
            var boss = _db.AddNode(flowId, "老板批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("boss").ToString(), 2);
            var mgr = _db.AddNode(flowId, "经理批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("mgr").ToString(), 3);
            // 网关 → 老板批(条件命中) / 网关 → 经理批(默认)
            _db.AddLink(flowId, gw, boss, Cond("amount", WfConditionOp.Ge, "10"), 1);
            _db.AddLink(flowId, gw, mgr, null, 2);
            // 老板批、经理批均无出边（分支叶子）

            var id = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "报销100",
                ApplyUser = "alice", ApplyUserId = _db.Uid("alice"),
                FormContent = "{\"amount\":\"100\"}",
            });

            // 发起后应停在「老板批」，仅生成 boss 待办（不生成 mgr 待办）
            var inst = GetInstance(id);
            Assert.Equal((int)WfInstanceStatus.Approval, inst.Status);
            Assert.Equal(boss, inst.CurrentNodeId);
            Assert.Single(_db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id).ToList());
            Assert.Equal("boss", GetTask(id, boss, "boss").Assignee);

            _engine.Approve(GetTask(id, boss, "boss").TaskId, "同意", _db.Uid("boss"));

            // 老板批完 → 无出边 = 终点 → 应结束
            inst = GetInstance(id);
            Assert.Equal((int)WfInstanceStatus.Approved, inst.Status);
            Assert.Null(inst.CurrentNodeId);
            // 经理批绝对不应被卷入
            Assert.Empty(_db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id && t.NodeId == mgr).ToList());
        }

        /// <summary>
        /// 同结构但金额 5 < 10 → 走默认分支「经理批」，经理批无出边 → 结束。验证默认分支选择正确。
        /// </summary>
        [Fact]
        public void ConditionGateway_首节点_小额走默认经理批_应结束()
        {
            var flowId = _db.AddDefinition("CG2", "金额分流");
            var gw = _db.AddNode(flowId, "金额判断", (int)WfNodeType.Condition, 0, "", 1);
            var boss = _db.AddNode(flowId, "老板批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("boss").ToString(), 2);
            var mgr = _db.AddNode(flowId, "经理批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("mgr").ToString(), 3);
            _db.AddLink(flowId, gw, boss, Cond("amount", WfConditionOp.Ge, "10"), 1);
            _db.AddLink(flowId, gw, mgr, null, 2);

            var id = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "报销5",
                ApplyUser = "alice", ApplyUserId = _db.Uid("alice"),
                FormContent = "{\"amount\":\"5\"}",
            });

            var inst = GetInstance(id);
            Assert.Equal(mgr, inst.CurrentNodeId);
            Assert.Equal("mgr", GetTask(id, mgr, "mgr").Assignee);
            Assert.Empty(_db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id && t.NodeId == boss).ToList());

            _engine.Approve(GetTask(id, mgr, "mgr").TaskId, "同意", _db.Uid("mgr"));

            inst = GetInstance(id);
            Assert.Equal((int)WfInstanceStatus.Approved, inst.Status);
            Assert.Null(inst.CurrentNodeId);
        }

        /// <summary>
        /// 边界值：金额恰好 = 10，Ge(>=) 应命中条件分支走老板批并结束。
        /// </summary>
        [Fact]
        public void ConditionGateway_边界值等于10_Ge命中()
        {
            var flowId = _db.AddDefinition("CG3", "金额分流");
            var gw = _db.AddNode(flowId, "金额判断", (int)WfNodeType.Condition, 0, "", 1);
            var boss = _db.AddNode(flowId, "老板批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("boss").ToString(), 2);
            var mgr = _db.AddNode(flowId, "经理批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("mgr").ToString(), 3);
            _db.AddLink(flowId, gw, boss, Cond("amount", WfConditionOp.Ge, "10"), 1);
            _db.AddLink(flowId, gw, mgr, null, 2);

            var id = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "报销10",
                ApplyUser = "alice", ApplyUserId = _db.Uid("alice"),
                FormContent = "{\"amount\":\"10\"}",
            });

            var inst = GetInstance(id);
            Assert.Equal(boss, inst.CurrentNodeId);
            _engine.Approve(GetTask(id, boss, "boss").TaskId, "同意", _db.Uid("boss"));
            Assert.Equal((int)WfInstanceStatus.Approved, GetInstance(id).Status);
        }

        /// <summary>
        /// 条件网关在中间节点（先一级审批 → 网关 → 分支）。验证网关非首节点也正确分流。
        /// </summary>
        [Fact]
        public void ConditionGateway_中间节点_前置审批后分流()
        {
            var flowId = _db.AddDefinition("CG4", "混合");
            var n1 = _db.AddNode(flowId, "主管审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("lead").ToString(), 1);
            var gw = _db.AddNode(flowId, "金额判断", (int)WfNodeType.Condition, 0, "", 2);
            var boss = _db.AddNode(flowId, "老板批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("boss").ToString(), 3);
            var mgr = _db.AddNode(flowId, "经理批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("mgr").ToString(), 4);
            _db.AddLink(flowId, n1, gw, null, 1);
            _db.AddLink(flowId, gw, boss, Cond("amount", WfConditionOp.Gt, "100"), 1);
            _db.AddLink(flowId, gw, mgr, null, 2);

            var id = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "t",
                ApplyUser = "alice", ApplyUserId = _db.Uid("alice"),
                FormContent = "{\"amount\":\"200\"}",
            });

            // 应先停在主管审批
            Assert.Equal(n1, GetInstance(id).CurrentNodeId);
            _engine.Approve(GetTask(id, n1, "lead").TaskId, "同意", _db.Uid("lead"));

            // 主管通过后到网关，金额 200 > 100 走老板批
            var inst = GetInstance(id);
            Assert.Equal(boss, inst.CurrentNodeId);
            _engine.Approve(GetTask(id, boss, "boss").TaskId, "同意", _db.Uid("boss"));

            inst = GetInstance(id);
            Assert.Equal((int)WfInstanceStatus.Approved, inst.Status);
            Assert.Empty(_db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id && t.NodeId == mgr).ToList());
        }

        /// <summary>
        /// 多条件分支（3 条出边），无条件命中且无默认分支 → 流程直接结束（保守）。
        /// </summary>
        [Fact]
        public void ConditionGateway_多分支均不命中且无默认_直接结束()
        {
            var flowId = _db.AddDefinition("CG5", "三分支");
            var gw = _db.AddNode(flowId, "判断", (int)WfNodeType.Condition, 0, "", 1);
            var a = _db.AddNode(flowId, "A批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("a").ToString(), 2);
            var b = _db.AddNode(flowId, "B批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("b").ToString(), 3);
            var c = _db.AddNode(flowId, "C批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("c").ToString(), 4);
            _db.AddLink(flowId, gw, a, Cond("lvl", WfConditionOp.Eq, "1"), 1);
            _db.AddLink(flowId, gw, b, Cond("lvl", WfConditionOp.Eq, "2"), 2);
            _db.AddLink(flowId, gw, c, Cond("lvl", WfConditionOp.Eq, "3"), 3);

            var id = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "t",
                ApplyUser = "alice", ApplyUserId = _db.Uid("alice"),
                FormContent = "{\"lvl\":\"9\"}", // 无分支命中
            });

            var inst = GetInstance(id);
            Assert.Equal((int)WfInstanceStatus.Approved, inst.Status);
            Assert.Null(inst.CurrentNodeId);
            Assert.Empty(_db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id).ToList());
        }

        #endregion

        #region B. 并行分组

        /// <summary>
        /// 并行两组节点：A 与 B 并行，必须都完成才汇聚到 C。单人通过 A 不推进。
        /// </summary>
        [Fact]
        public void Parallel_两组需全部完成才汇聚()
        {
            var flowId = _db.AddDefinition("PL1", "并行");
            var n1 = _db.AddNode(flowId, "发起", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("lead").ToString(), 1);
            var pa = _db.AddNode(flowId, "并行A", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("a").ToString(), 2, parallelGroup: 1);
            var pb = _db.AddNode(flowId, "并行B", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("b").ToString(), 3, parallelGroup: 1);
            var pc = _db.AddNode(flowId, "汇聚C", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("c").ToString(), 4);
            _db.AddLink(flowId, n1, pa, null, 1);
            _db.AddLink(flowId, pa, pb, null, 1); // 串接写法，引擎按 ParallelGroup 识别同组 fork
            _db.AddLink(flowId, pb, pc, null, 1);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            // Start 后先停在发起节点 n1，并行组尚未 fork
            Assert.Equal(n1, GetInstance(id).CurrentNodeId);
            _engine.Approve(GetTask(id, n1, "lead").TaskId, "同意", _db.Uid("lead"));

            // n1 审批后并行 A、B 同时有待办
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, pa, "a").Status);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, pb, "b").Status);

            _engine.Approve(GetTask(id, pa, "a").TaskId, "同意", _db.Uid("a"));
            // A 完成但 B 未完成，仍在并行组，不汇聚；活动集仅剩 B，CurrentNodeId 取活动集剩余成员（pb）
            var inst = GetInstance(id);
            Assert.Equal((int)WfInstanceStatus.Approval, inst.Status);
            Assert.Equal(pb, inst.CurrentNodeId);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, pb, "b").Status);

            _engine.Approve(GetTask(id, pb, "b").TaskId, "同意", _db.Uid("b"));
            // 两组都完成 → 汇聚到 C
            inst = GetInstance(id);
            Assert.Equal(pc, inst.CurrentNodeId);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, pc, "c").Status);

            _engine.Approve(GetTask(id, pc, "c").TaskId, "同意", _db.Uid("c"));
            Assert.Equal((int)WfInstanceStatus.Approved, GetInstance(id).Status);
        }

        /// <summary>
        /// 并行组内某节点条件不满足（包容网关）→ 视为已完成，仅其余节点需审批。
        /// </summary>
        [Fact]
        public void Parallel_组内条件不满足节点被跳过()
        {
            var flowId = _db.AddDefinition("PL2", "并行包容");
            var n1 = _db.AddNode(flowId, "发起", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("lead").ToString(), 1);
            var pa = _db.AddNode(flowId, "并行A", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("a").ToString(), 2, parallelGroup: 1,
                conditionField: "vip", conditionOp: (int)WfConditionOp.Eq, conditionValue: "1");
            var pb = _db.AddNode(flowId, "并行B", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("b").ToString(), 3, parallelGroup: 1);
            var pc = _db.AddNode(flowId, "汇聚C", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("c").ToString(), 4);
            _db.AddLink(flowId, n1, pa, null, 1);
            _db.AddLink(flowId, pa, pb, null, 1);
            _db.AddLink(flowId, pb, pc, null, 1);

            // vip != 1 → A 条件不满足，不应生成 A 待办
            var id = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "t",
                ApplyUser = "alice", ApplyUserId = _db.Uid("alice"),
                FormContent = "{\"vip\":\"0\"}",
            });

            // 先审批发起节点 n1，触发并行组 fork
            _engine.Approve(GetTask(id, n1, "lead").TaskId, "同意", _db.Uid("lead"));

            Assert.Empty(_db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id && t.NodeId == pa).ToList());
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, pb, "b").Status);

            // 仅 B 通过即汇聚到 C
            _engine.Approve(GetTask(id, pb, "b").TaskId, "同意", _db.Uid("b"));
            Assert.Equal(pc, GetInstance(id).CurrentNodeId);
            _engine.Approve(GetTask(id, pc, "c").TaskId, "同意", _db.Uid("c"));
            Assert.Equal((int)WfInstanceStatus.Approved, GetInstance(id).Status);
        }

        #endregion

        #region C. 抄送节点

        /// <summary>
        /// 抄送节点不阻塞流程：审批 → 抄送 → 末节点结束。
        /// </summary>
        [Fact]
        public void Cc_抄送节点不阻塞流转()
        {
            var flowId = _db.AddDefinition("CC1", "抄送");
            var n1 = _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("a").ToString(), 1);
            var cc = _db.AddNode(flowId, "抄送", (int)WfNodeType.Cc, (int)WfApproverType.User, $"{_db.Uid("cc1")},{_db.Uid("cc2")}", 2);
            _db.AddLink(flowId, n1, cc, null, 1);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });

            _engine.Approve(GetTask(id, n1, "a").TaskId, "同意", _db.Uid("a"));

            // 抄送任务生成（Skipped 状态）：引擎对抄送节点只落 1 条任务，多人以逗号串存于 Assignee
            var ccTasks = _db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id && t.NodeId == cc).ToList();
            Assert.Single(ccTasks); // 抄送节点合一条任务
            Assert.Equal((int)WfTaskStatus.Skipped, ccTasks[0].Status);
            Assert.Equal("cc1,cc2", ccTasks[0].Assignee);

            var inst = GetInstance(id);
            Assert.Equal((int)WfInstanceStatus.Approved, inst.Status);
            Assert.Null(inst.CurrentNodeId);
        }

        /// <summary>
        /// 抄送 + 审批链路：审批 → 抄送 → 再审批(末节点)。抄送不阻断，最终到末节点待审。
        /// </summary>
        [Fact]
        public void Cc_抄送后再审批()
        {
            var flowId = _db.AddDefinition("CC2", "抄送后审批");
            var n1 = _db.AddNode(flowId, "一级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("a").ToString(), 1);
            var cc = _db.AddNode(flowId, "抄送", (int)WfNodeType.Cc, (int)WfApproverType.User, _db.Uid("cc1").ToString(), 2);
            var n2 = _db.AddNode(flowId, "二级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("b").ToString(), 3);
            _db.AddLink(flowId, n1, cc, null, 1);
            _db.AddLink(flowId, cc, n2, null, 1);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });
            _engine.Approve(GetTask(id, n1, "a").TaskId, "同意", _db.Uid("a"));

            // 抄送落库后继续到二级
            Assert.Equal(n2, GetInstance(id).CurrentNodeId);
            Assert.Equal((int)WfTaskStatus.Pending, GetTask(id, n2, "b").Status);
            _engine.Approve(GetTask(id, n2, "b").TaskId, "同意", _db.Uid("b"));
            Assert.Equal((int)WfInstanceStatus.Approved, GetInstance(id).Status);
        }

        #endregion

        #region D. 节点级条件排他（ConditionField）

        /// <summary>
        /// 节点条件不满足时直接跳过该节点到下一节点。
        /// </summary>
        [Fact]
        public void NodeCondition_条件不满足跳过到下一节点()
        {
            var flowId = _db.AddDefinition("NC1", "节点条件");
            var n1 = _db.AddNode(flowId, "一级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("a").ToString(), 1);
            var n2 = _db.AddNode(flowId, "VIP审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("vip").ToString(), 2,
                conditionField: "vip", conditionOp: (int)WfConditionOp.Eq, conditionValue: "1");
            var n3 = _db.AddNode(flowId, "三级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("b").ToString(), 3);
            _db.AddLink(flowId, n1, n2, null, 1);
            _db.AddLink(flowId, n2, n3, null, 1);

            var id = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "t",
                ApplyUser = "alice", ApplyUserId = _db.Uid("alice"),
                FormContent = "{\"vip\":\"0\"}", // n2 条件不满足
            });

            _engine.Approve(GetTask(id, n1, "a").TaskId, "同意", _db.Uid("a"));

            // n2 被跳过，直接到 n3，不生成 n2 待办
            Assert.Equal(n3, GetInstance(id).CurrentNodeId);
            Assert.Empty(_db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id && t.NodeId == n2).ToList());
            _engine.Approve(GetTask(id, n3, "b").TaskId, "同意", _db.Uid("b"));
            Assert.Equal((int)WfInstanceStatus.Approved, GetInstance(id).Status);
        }

        /// <summary>
        /// 所有节点条件都不满足 → 流程直接通过（无任何待办）。
        /// </summary>
        [Fact]
        public void NodeCondition_全部不满足_直接通过()
        {
            var flowId = _db.AddDefinition("NC2", "全跳过");
            var n1 = _db.AddNode(flowId, "A", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("a").ToString(), 1,
                conditionField: "x", conditionOp: (int)WfConditionOp.Eq, conditionValue: "1");
            var n2 = _db.AddNode(flowId, "B", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("b").ToString(), 2,
                conditionField: "y", conditionOp: (int)WfConditionOp.Eq, conditionValue: "1");
            _db.AddLink(flowId, n1, n2, null, 1);

            var id = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "t",
                ApplyUser = "alice", ApplyUserId = _db.Uid("alice"),
                FormContent = "{\"x\":\"0\",\"y\":\"0\"}",
            });

            var inst = GetInstance(id);
            Assert.Equal((int)WfInstanceStatus.Approved, inst.Status);
            Assert.Null(inst.CurrentNodeId);
            Assert.Empty(_db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id).ToList());
        }

        #endregion

        #region E. 回归：无连线老数据走 NodeOrder fallback 仍正常

        /// <summary>
        /// 确保修复没有误删 fallback：完全没有节点连线（存量老数据）时，仍按 NodeOrder 串联并正常结束。
        /// </summary>
        [Fact]
        public void NoLinks_老数据按NodeOrder串联并结束()
        {
            var flowId = _db.AddDefinition("OLD", "老数据");
            var n1 = _db.AddNode(flowId, "一级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("a").ToString(), 1);
            var n2 = _db.AddNode(flowId, "二级", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uid("b").ToString(), 2);
            // 不建任何 WfNodeLink

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = _db.Uid("alice") });
            Assert.Equal(n1, GetInstance(id).CurrentNodeId);

            _engine.Approve(GetTask(id, n1, "a").TaskId, "同意", _db.Uid("a"));
            Assert.Equal(n2, GetInstance(id).CurrentNodeId);

            _engine.Approve(GetTask(id, n2, "b").TaskId, "同意", _db.Uid("b"));
            Assert.Equal((int)WfInstanceStatus.Approved, GetInstance(id).Status);
            Assert.Null(GetInstance(id).CurrentNodeId);
        }

        #endregion
    }
}
