using System;
using System.Collections.Generic;
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
using ZR.Workflow.Service.IService;

namespace ZR.Tests
{
    /// <summary>
    /// 表单字段权限（钉钉式）后端契约与回写校验回归。
    /// 覆盖：详情按当前审批节点过滤不可见字段、申请人/历史实例全放开、审批通过回写可编辑字段、越权修改拒绝。
    /// </summary>
    [Collection("WfTests")]
    public class WfFieldPermissionTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;
        private readonly WfFlowInstanceService _instanceService;

        public WfFieldPermissionTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _db.EnsureUsers("applier", "approver");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>(), Mock.Of<IWfWebhookService>(), Mock.Of<IWfAiService>());
            _instanceService = new WfFlowInstanceService(_engine, Mock.Of<IWfAiService>());
        }

        /// <summary>
        /// 审批节点配置字段权限：仅 amount 可见且可编辑，remark 可见但只读，secret 不可见。
        /// 审批人查看详情时：FormContent 应剔除 secret；权限视图应正确反映可见/可编辑集合。
        /// </summary>
        [Fact]
        public void 审批节点配置字段权限_详情按权限过滤不可见字段()
        {
            var flowId = _db.AddDefinition("FP_FILTER", "字段权限过滤");
            var start = _db.AddNode(flowId, "开始", (int)WfNodeType.Start, (int)WfApproverType.User, null, 1);
            var audit = _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("approver"), 2,
                fieldPermission: "[{\"field\":\"amount\",\"perm\":0},{\"field\":\"remark\",\"perm\":1},{\"field\":\"secret\",\"perm\":2}]");
            var end = _db.AddNode(flowId, "结束", (int)WfNodeType.End, (int)WfApproverType.User, null, 3);
            _db.AddLink(flowId, start, audit);
            _db.AddLink(flowId, audit, end);

            var form = JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { "amount", "100" },
                { "remark", "备注" },
                { "secret", "机密" }
            });
            var instanceId = _engine.Start(new WfFlowInstance
            {
                FlowId = flowId,
                Title = "测试字段权限",
                FormContent = form,
                ApplyUser = "applier",
                ApplyUserId = _db.Uid("applier")
            });

            var detail = _instanceService.GetInfo(instanceId, _db.Uid("approver")); // 审批人视角

            Assert.NotNull(detail.FieldPermissionView);
            Assert.False(detail.FieldPermissionView.AllEditable);
            Assert.Single(detail.FieldPermissionView.ReadonlyFields);
            Assert.Contains("remark", detail.FieldPermissionView.ReadonlyFields);
            Assert.Single(detail.FieldPermissionView.HiddenFields);
            Assert.Contains("secret", detail.FieldPermissionView.HiddenFields);

            var returned = JsonConvert.DeserializeObject<Dictionary<string, string>>(detail.FormContent);
            Assert.Contains("amount", returned.Keys);
            Assert.Contains("remark", returned.Keys);
            Assert.DoesNotContain("secret", returned.Keys);
        }

        /// <summary>
        /// 申请人本人查看进行中实例 → 全字段可编辑（AllEditable=true），便于回填/修改。
        /// </summary>
        [Fact]
        public void 申请人查看进行中实例_全字段可编辑()
        {
            var flowId = _db.AddDefinition("FP_OWNER", "申请人字段权限");
            var start = _db.AddNode(flowId, "开始", (int)WfNodeType.Start, (int)WfApproverType.User, null, 1);
            var audit = _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("approver"), 2,
                fieldPermission: "[{\"field\":\"amount\",\"perm\":1}]");
            var end = _db.AddNode(flowId, "结束", (int)WfNodeType.End, (int)WfApproverType.User, null, 3);
            _db.AddLink(flowId, start, audit);
            _db.AddLink(flowId, audit, end);

            var form = JsonConvert.SerializeObject(new Dictionary<string, string> { { "amount", "100" }, { "remark", "备注" } });
            var instanceId = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "测试", FormContent = form, ApplyUser = "applier", ApplyUserId = _db.Uid("applier") });

            var detail = _instanceService.GetInfo(instanceId, _db.Uid("applier"));

            Assert.True(detail.FieldPermissionView.AllEditable);
            Assert.Empty(detail.FieldPermissionView.ReadonlyFields);
            Assert.Empty(detail.FieldPermissionView.HiddenFields);
            var returned = JsonConvert.DeserializeObject<Dictionary<string, string>>(detail.FormContent);
            Assert.Contains("amount", returned.Keys);
            Assert.Contains("remark", returned.Keys);
        }

        /// <summary>
        /// 流程结束后查看历史实例 → 全字段可见只读（历史实例直接放开）。
        /// </summary>
        [Fact]
        public void 历史实例已结束_全字段可见只读()
        {
            var flowId = _db.AddDefinition("FP_HIST", "历史字段权限");
            var start = _db.AddNode(flowId, "开始", (int)WfNodeType.Start, (int)WfApproverType.User, null, 1);
            var audit = _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("approver"), 2,
                fieldPermission: "[{\"field\":\"amount\",\"perm\":0}]");
            var end = _db.AddNode(flowId, "结束", (int)WfNodeType.End, (int)WfApproverType.User, null, 3);
            _db.AddLink(flowId, start, audit);
            _db.AddLink(flowId, audit, end);

            var form = JsonConvert.SerializeObject(new Dictionary<string, string> { { "amount", "100" }, { "remark", "备注" } });
            var instanceId = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "测试", FormContent = form, ApplyUser = "applier", ApplyUserId = _db.Uid("applier") });

            var task = _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == instanceId);
            _engine.Approve(task.TaskId, "同意", _db.Uid("approver"));

            var detail = _instanceService.GetInfo(instanceId, _db.Uid("approver") + 1000); // 第三方查看历史
            Assert.False(detail.FieldPermissionView.AllEditable);
            Assert.Empty(detail.FieldPermissionView.ReadonlyFields);
            Assert.Empty(detail.FieldPermissionView.HiddenFields);
            var returned = JsonConvert.DeserializeObject<Dictionary<string, string>>(detail.FormContent);
            Assert.Contains("amount", returned.Keys);
            Assert.Contains("remark", returned.Keys);
        }

        /// <summary>
        /// 审批通过时提交可编辑字段变更，应成功回写数据库，且后续详情读取为新值。
        /// </summary>
        [Fact]
        public void 审批通过_回写可编辑字段成功()
        {
            var flowId = _db.AddDefinition("FP_EDIT_OK", "字段编辑成功");
            var start = _db.AddNode(flowId, "开始", (int)WfNodeType.Start, (int)WfApproverType.User, null, 1);
            var audit = _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("approver"), 2,
                fieldPermission: "[{\"field\":\"amount\",\"perm\":0},{\"field\":\"remark\",\"perm\":1},{\"field\":\"secret\",\"perm\":2}]");
            var end = _db.AddNode(flowId, "结束", (int)WfNodeType.End, (int)WfApproverType.User, null, 3);
            _db.AddLink(flowId, start, audit);
            _db.AddLink(flowId, audit, end);

            var form = JsonConvert.SerializeObject(new Dictionary<string, string> { { "amount", "100" }, { "remark", "备注" } });
            var instanceId = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "测试", FormContent = form, ApplyUser = "applier", ApplyUserId = _db.Uid("applier") });

            var task = _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == instanceId);
            var edit = JsonConvert.SerializeObject(new Dictionary<string, string> { { "amount", "200" } });
            _engine.Approve(task.TaskId, "同意", _db.Uid("approver"), edit);

            // 数据库持久化验证
            var persisted = _db.Db.Queryable<WfFlowInstance>().First(i => i.InstanceId == instanceId);
            var persistedForm = JsonConvert.DeserializeObject<Dictionary<string, string>>(persisted.FormContent);
            Assert.Equal("200", persistedForm["amount"]);
            Assert.Equal("备注", persistedForm["remark"]);
            Assert.NotNull(persisted.Update_time);
            Assert.NotNull(persisted.Update_by);
        }

        /// <summary>
        /// 审批人尝试修改「不可见」或「只读」字段，应抛越权异常且数据库不更新。
        /// </summary>
        [Theory]
        [InlineData("secret")] // 不可见字段
        [InlineData("remark")] // 可见但只读字段
        public void 审批通过_修改无权字段被拒绝(string field)
        {
            var flowId = _db.AddDefinition($"FP_EDIT_FAIL_{field}", "字段编辑越权");
            var start = _db.AddNode(flowId, "开始", (int)WfNodeType.Start, (int)WfApproverType.User, null, 1);
            var audit = _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("approver"), 2,
                fieldPermission: "[{\"field\":\"amount\",\"perm\":0},{\"field\":\"remark\",\"perm\":1},{\"field\":\"secret\",\"perm\":2}]");
            var end = _db.AddNode(flowId, "结束", (int)WfNodeType.End, (int)WfApproverType.User, null, 3);
            _db.AddLink(flowId, start, audit);
            _db.AddLink(flowId, audit, end);

            var form = JsonConvert.SerializeObject(new Dictionary<string, string> { { "amount", "100" }, { "remark", "备注" } });
            var instanceId = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "测试", FormContent = form, ApplyUser = "applier", ApplyUserId = _db.Uid("applier") });

            var task = _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == instanceId);
            var edit = JsonConvert.SerializeObject(new Dictionary<string, string> { { field, "越权修改" } });

            Assert.Throws<CustomException>(() => _engine.Approve(task.TaskId, "同意", _db.Uid("approver"), edit));

            // 验证数据库未被修改
            var persisted = _db.Db.Queryable<WfFlowInstance>().First(i => i.InstanceId == instanceId);
            var persistedForm = JsonConvert.DeserializeObject<Dictionary<string, string>>(persisted.FormContent);
            Assert.Equal("100", persistedForm["amount"]);
        }

        /// <summary>
        /// 节点未配置 FieldPermission 时，未配置的字段默认可编辑，审批人提交字段变更应成功回写。
        /// </summary>
        [Fact]
        public void 审批节点未配置字段权限_未配置字段默认可编辑()
        {
            var flowId = _db.AddDefinition("FP_NO_CFG", "未配置字段权限");
            var start = _db.AddNode(flowId, "开始", (int)WfNodeType.Start, (int)WfApproverType.User, null, 1);
            var audit = _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.User, _db.Uids("approver"), 2);
            var end = _db.AddNode(flowId, "结束", (int)WfNodeType.End, (int)WfApproverType.User, null, 3);
            _db.AddLink(flowId, start, audit);
            _db.AddLink(flowId, audit, end);

            var form = JsonConvert.SerializeObject(new Dictionary<string, string> { { "amount", "100" } });
            var instanceId = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "测试", FormContent = form, ApplyUser = "applier", ApplyUserId = _db.Uid("applier") });

            var task = _db.Db.Queryable<WfFlowTask>().First(t => t.InstanceId == instanceId);
            var edit = JsonConvert.SerializeObject(new Dictionary<string, string> { { "amount", "200" } });

            _engine.Approve(task.TaskId, "同意", _db.Uid("approver"), edit);

            var persisted = _db.Db.Queryable<WfFlowInstance>().First(i => i.InstanceId == instanceId);
            var persistedForm = JsonConvert.DeserializeObject<Dictionary<string, string>>(persisted.FormContent);
            Assert.Equal("200", persistedForm["amount"]);
        }
    }
}
