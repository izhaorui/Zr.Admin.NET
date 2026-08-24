using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using SqlSugar;
using SqlSugar.IOC;
using ZR.Model.System;
using ZR.Workflow.Model;
using Xunit;

namespace ZR.Tests
{
    /// <summary>
    /// 工作流测试共享夹具。
    /// 负责：① 注入空配置使多租户关闭、主库 ConfigId 回落为 "0"；
    /// ② 将 DbScoped.SugarScope 指向一个内存 SQLite（IsAutoCloseConnection=false 保持单连接，保证事务内外数据可见）；
    /// ③ CodeFirst 建表（幂等）；④ 提供清空与种子辅助方法。
    /// 以 [Collection("WfTests")] 的 ICollectionFixture 形式注入各测试类，集合内顺序执行、共享同一上下文。
    /// </summary>
    public class WfTestDb : IDisposable
    {
        // 使用临时文件型 SQLite：:memory: 数据库按物理连接隔离，跨 SqlSugar 连接不可见，
        // 而文件库在连接关闭后仍持久化，保证 CodeFirst 建表与后续查询看到同一库。
        private static readonly string DbPath = Path.Combine(Path.GetTempPath(), "zr_workflow_test.db");

        private static readonly List<ConnectionConfig> Configs = new()
        {
            new ConnectionConfig
            {
                ConfigId = "0",
                DbType = DbType.Sqlite,
                ConnectionString = $"DataSource={DbPath};Pooling=false;",
                IsAutoCloseConnection = true,
                // SQLite 仅允许 INTEGER PRIMARY KEY 使用 AUTOINCREMENT；
                // 模型自增主键为 long（被映射为 BIGINT），这里在 CodeFirst 时改写为 INTEGER，
                // 使 SqlSugar 生成合法的 "INTEGER PRIMARY KEY AUTOINCREMENT"。
                ConfigureExternalServices = new ConfigureExternalServices
                {
                    EntityService = (PropertyInfo property, EntityColumnInfo column) =>
                    {
                        if (column.IsPrimarykey && column.IsIdentity && property.PropertyType == typeof(long))
                        {
                            // SQLite 仅允许 INTEGER PRIMARY KEY 使用 AUTOINCREMENT
                            column.DataType = "INTEGER";
                        }
                        else if (!column.IsPrimarykey)
                        {
                            // 测试库放宽非空约束：CodeFirst 默认会把字符串列建成 NOT NULL，
                            // 种子插入常带 null，统一改为可空避免约束失败（生产库不受影响）。
                            column.IsNullable = true;
                        }
                    },
                },
            },
        };

        private readonly SqlSugarScope _scope;

        public WfTestDb()
        {
            if (File.Exists(DbPath)) File.Delete(DbPath);
            InternalApp.Configuration = new ConfigurationBuilder().Build();
            _scope = new SqlSugarScope(Configs);
            DbScoped.SugarScope = _scope;
            _scope.CodeFirst.InitTables(
                typeof(WfFlowDefinition),
                typeof(WfFlowNode),
                typeof(WfNodeLink),
                typeof(WfFlowInstance),
                typeof(WfFlowTask),
                typeof(WfFlowRecord),
                typeof(SysUser),
                typeof(SysUserRole),
                typeof(WfWebhook));

            // 启用 WAL 日志模式：SQLite 默认 rollback journal 在并发读写时易出现
            // "database is locked"，WAL 让读写可并存，消除多连接竞态/死锁。
            // 该模式随数据库文件持久化，对后续所有连接生效。
            Db.Ado.ExecuteCommand("PRAGMA journal_mode=WAL;");
        }

        /// <summary>
        /// 防御性确保 DbScoped.SugarScope 仍指向本夹具的内存库
        /// （集合顺序执行下一般不会被其它测试类静态构造覆盖）。
        /// </summary>
        public void Ensure()
        {
            if (!ReferenceEquals(DbScoped.SugarScope, _scope))
            {
                DbScoped.SugarScope = _scope;
            }
        }

        public ISqlSugarClient Db => _scope.GetConnectionScope("0");

        /// <summary>
        /// 清空所有工作流相关表，保证用例间数据隔离、可重复。
        /// </summary>
        public void Clean()
        {
            Ensure();
            Db.Deleteable<WfFlowTask>().ExecuteCommand();
            Db.Deleteable<WfFlowRecord>().ExecuteCommand();
            Db.Deleteable<WfFlowInstance>().ExecuteCommand();
            Db.Deleteable<WfNodeLink>().ExecuteCommand();
            Db.Deleteable<WfFlowNode>().ExecuteCommand();
            Db.Deleteable<WfFlowDefinition>().ExecuteCommand();
            Db.Deleteable<WfWebhook>().ExecuteCommand();
            Db.Deleteable<SysUserRole>().ExecuteCommand();
            Db.Deleteable<SysUser>().ExecuteCommand();
        }

        public long AddDefinition(string code, string name, int status = 1)
        {
            Ensure();
            return Db.Insertable(new WfFlowDefinition { FlowCode = code, FlowName = name, Status = status })
                .ExecuteReturnIdentity();
        }

        public long AddNode(long flowId, string name, int nodeType, int approverType, string approverId, int nodeOrder,
            int signType = 0, int parallelGroup = 0, string conditionField = null, int conditionOp = 0, string conditionValue = null,
            int rejectStrategy = 0, long? rejectTargetNodeId = null,
            int emptyApproverStrategy = 0, long? defaultApproverId = null,
            int timeoutHours = 0, int timeoutAction = 0, long? timeoutTransferUserId = null,
            string fieldPermission = null)
        {
            Ensure();
            return Db.Insertable(new WfFlowNode
            {
                FlowId = flowId,
                NodeName = name,
                NodeType = nodeType,
                ApproverType = approverType,
                ApproverId = approverId,
                NodeOrder = nodeOrder,
                SignType = signType,
                ParallelGroup = parallelGroup,
                ConditionField = conditionField,
                ConditionOp = conditionOp,
                ConditionValue = conditionValue,
                RejectStrategy = rejectStrategy,
                RejectTargetNodeId = rejectTargetNodeId,
                EmptyApproverStrategy = emptyApproverStrategy,
                DefaultApproverId = defaultApproverId,
                TimeoutHours = timeoutHours,
                TimeoutAction = timeoutAction,
                TimeoutTransferUserId = timeoutTransferUserId,
                FieldPermission = fieldPermission,
            }).ExecuteReturnIdentity();
        }

        /// <summary>
        /// 建一条节点连线（有向边）。conditionJson 为空/null 表示默认分支（无条件）。
        /// </summary>
        public long AddLink(long flowId, long sourceNodeId, long targetNodeId, string conditionJson = null, int sort = 0)
        {
            Ensure();
            return Db.Insertable(new WfNodeLink
            {
                FlowId = flowId,
                SourceNodeId = sourceNodeId,
                TargetNodeId = targetNodeId,
                ConditionJson = string.IsNullOrEmpty(conditionJson) ? null : conditionJson,
                Sort = sort,
            }).ExecuteReturnIdentity();
        }

        public long AddUser(string userName, long deptId, int status = 0)
        {
            Ensure();
            return Db.Insertable(new SysUser
            {
                UserName = userName,
                NickName = userName,
                Password = "test",
                DeptId = deptId,
                Status = status,
            }).ExecuteReturnIdentity();
        }

        /// <summary>
        /// 幂等播种一批测试用户（userName 与 userId 同值，便于按 userName 反查 / 作审批人标识）。
        /// 引擎 ResolveApprovers 需到 SysUser 落库审批人，测试库必须事先存在这些用户。
        /// </summary>
        public void EnsureUsers(params string[] userNames)
        {
            Ensure();
            foreach (var name in userNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                if (Db.Queryable<SysUser>().Any(u => u.UserName == name)) continue;
                Db.Insertable(new SysUser
                {
                    UserName = name,
                    NickName = name,
                    Password = "test",
                    DeptId = 1,
                    Status = 0,
                }).ExecuteCommand();
            }
        }

        /// <summary>
        /// 按 userName 反查其自增 userId。引擎 ResolveApprovers 的指定用户/部门/角色分支
        /// 最终都以 userId 落库，故测试建节点时 ApproverId 必须传数字 userId（而非 userName）。
        /// </summary>
        public long Uid(string userName)
        {
            Ensure();
            return Db.Queryable<SysUser>().Where(u => u.UserName == userName).Select(u => u.UserId).First();
        }

        /// <summary>
        /// 同 <see cref="Uid"/>，但直接返回字符串形式 userId，便于作为 AddNode 的 approverId 参数。
        /// </summary>
        public string Uids(string userName) => Uid(userName).ToString();

        public void AddUserRole(long userId, long roleId)
        {
            Ensure();
            Db.Insertable(new SysUserRole { UserId = userId, RoleId = roleId }).ExecuteCommand();
        }

        public void Dispose()
        {
            _scope?.Dispose();
            try
            {
                foreach (var ext in new[] { "", "-wal", "-shm", "-journal" })
                {
                    var p = DbPath + ext;
                    if (File.Exists(p)) File.Delete(p);
                }
            }
            catch (IOException)
            {
                // 连接已释放后文件通常可删；若仍被占用则忽略，不影响测试结论。
            }
        }
    }
}
