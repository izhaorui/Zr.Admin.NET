using System;
using System.Collections.Generic;
using System.Linq;
using Infrastructure;
using Xunit;
using ZR.Workflow.Enum;
using ZR.Workflow.Model;
using ZR.Workflow.Service;

namespace ZR.Tests
{
    /// <summary>
    /// 审批人解析（ResolveApprovers）单元测试。
    /// 该方法为私有，通过构造含不同 ApproverType 节点的流程，发起后断言生成的
    /// wf_flow_task.Assignee 集合，间接验证指定用户/角色/部门三类解析分支。
    /// </summary>
    [Collection("WfTests")]
    public class WfApproverResolveTests
    {
        private readonly WfTestDb _db;
        private readonly WfEngineService _engine;

        public WfApproverResolveTests(WfTestDb db)
        {
            _db = db;
            _db.Ensure();
            _db.Clean();
            _engine = new WfEngineService();
        }

        private HashSet<string> StartAndGetAssignees(long flowId)
        {
            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });
            return _db.Db.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == id)
                .Select(t => t.Assignee)
                .ToList()
                .ToHashSet();
        }

        [Fact]
        public void Resolve_指定用户_逗号拆分生成多待办()
        {
            var flowId = _db.AddDefinition("U", "用户");
            _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.User, "u1,u2,u3", 1);

            var assignees = StartAndGetAssignees(flowId);

            Assert.Equal(new HashSet<string> { "u1", "u2", "u3" }, assignees);
        }

        [Fact]
        public void Resolve_指定角色_取角色下用户且不含其它角色()
        {
            var raId = _db.AddUser("ra", 1);
            var rbId = _db.AddUser("rb", 1);
            var rcId = _db.AddUser("rc", 1);
            _db.AddUserRole(raId, 10);
            _db.AddUserRole(rbId, 10);
            _db.AddUserRole(rcId, 20);

            var flowId = _db.AddDefinition("R", "角色");
            _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.Role, "10", 1);

            var assignees = StartAndGetAssignees(flowId);

            Assert.Equal(new HashSet<string> { "ra", "rb" }, assignees);
        }

        [Fact]
        public void Resolve_指定部门_仅取本部门且状态正常用户()
        {
            _db.AddUser("da", 5, 0); // 部门5 正常
            _db.AddUser("db", 5, 0); // 部门5 正常
            _db.AddUser("dc", 6, 0); // 部门6 正常（不含）
            _db.AddUser("dd", 5, 1); // 部门5 但禁用（不含）

            var flowId = _db.AddDefinition("D", "部门");
            _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.Dept, "5", 1);

            var assignees = StartAndGetAssignees(flowId);

            Assert.Equal(new HashSet<string> { "da", "db" }, assignees);
        }
    }
}
