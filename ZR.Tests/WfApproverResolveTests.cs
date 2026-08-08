using System;
using System.Collections.Generic;
using System.Linq;
using Infrastructure;
using Moq;
using Xunit;
using ZR.Model.System;
using ZR.ServiceCore.Services;
using ZR.Workflow.Enum;
using ZR.Workflow.Model;
using ZR.Workflow.Service;

namespace ZR.Tests
{
    /// <summary>
    /// 审批人解析（ResolveApprovers）单元测试。
    /// 该方法为私有，通过构造含不同 ApproverType 节点的流程，发起后断言生成的
    /// wf_flow_task.Assignee 集合，间接验证指定用户/角色/部门三类解析分支。
    /// 注意：指定用户分支以数字 userId 解析，故 ApproverId 传 _db.Uids("用户名")。
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
            _db.EnsureUsers("alice", "u1", "u2", "u3", "ra", "rb", "rc", "da", "db", "dc", "dd");
            _engine = new WfEngineService(Mock.Of<ISysUserMsgService>());
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
            _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.User, $"{_db.Uids("u1")},{_db.Uids("u2")},{_db.Uids("u3")}", 1);

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

        [Fact]
        public void Resolve_部门负责人_取部门LeaderIds对应用户()
        {
            var bossId = _db.AddUser("boss", 1);
            // 测试库默认未建 SysDept 表，局部 InitTables 建表并写入部门负责人
            _db.Db.CodeFirst.InitTables(typeof(SysDept));
            var newDept = new SysDept { DeptName = "测试部门", LeaderIds = bossId.ToString() };
            var deptId = _db.Db.Insertable(newDept).ExecuteReturnIdentity();
            try
            {
                var flowId = _db.AddDefinition("DL", "部门负责人");
                _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.DeptLeader, deptId.ToString(), 1);

                var assignees = StartAndGetAssignees(flowId);

                Assert.Equal(new HashSet<string> { "boss" }, assignees);
            }
            finally
            {
                _db.Db.Deleteable<SysDept>().Where(d => d.DeptId == deptId).ExecuteCommand();
            }
        }

        [Fact]
        public void Resolve_发起人主管_取发起人LeaderId对应用户()
        {
            var bossId = _db.AddUser("boss2", 1);
            var aliceId = _db.Db.Queryable<SysUser>().Where(u => u.UserName == "alice").Select(u => u.UserId).First();
            _db.Db.Updateable<SysUser>().SetColumns(u => u.LeaderId == bossId).Where(u => u.UserId == aliceId).ExecuteCommand();

            var flowId = _db.AddDefinition("AL", "发起人主管");
            _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.ApplyLeader, "", 1);

            var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice", ApplyUserId = aliceId });
            var assignees = _db.Db.Queryable<WfFlowTask>()
                .Where(t => t.InstanceId == id)
                .Select(t => t.Assignee)
                .ToList()
                .ToHashSet();

            Assert.Equal(new HashSet<string> { "boss2" }, assignees);
        }

        [Fact]
        public void Resolve_部门负责人_无LeaderIds时节点自动跳过不卡死()
        {
            // 测试库默认未建 SysDept 表，局部 InitTables 建表并写入一个【无负责人】的部门
            _db.Db.CodeFirst.InitTables(typeof(SysDept));
            var newDept = new SysDept { DeptName = "空负责人部门", LeaderIds = "" };
            var deptId = _db.Db.Insertable(newDept).ExecuteReturnIdentity();
            try
            {
                var flowId = _db.AddDefinition("DL_Empty", "部门负责人空");
                _db.AddNode(flowId, "审批", (int)WfNodeType.Audit, (int)WfApproverType.DeptLeader, deptId.ToString(), 1);

                var id = _engine.Start(new WfFlowInstance { FlowId = flowId, Title = "t", ApplyUser = "alice" });

                // 断言：流程不卡死——单节点流程在审批人缺失时自动跳过并直接通过（Status=Approved=1）
                var status = _db.Db.Queryable<WfFlowInstance>().Where(i => i.InstanceId == id).Select(i => i.Status).First();
                Assert.Equal((int)WfInstanceStatus.Approved, status);
                // 断言：留痕——生成了一条 Skipped 状态的审批任务，而非 Pending 待办
                var tasks = _db.Db.Queryable<WfFlowTask>().Where(t => t.InstanceId == id).ToList();
                Assert.Single(tasks);
                Assert.Equal((int)WfTaskStatus.Skipped, tasks[0].Status);
            }
            finally
            {
                _db.Db.Deleteable<SysDept>().Where(d => d.DeptId == deptId).ExecuteCommand();
            }
        }
    }
}
