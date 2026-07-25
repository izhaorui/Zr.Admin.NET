using System;
using System.IO;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Moq;
using SqlSugar;
using SqlSugar.IOC;
using Xunit;
using ZR.Model;
using ZR.Model.System;
using ZR.ServiceCore.Services;

namespace ZR.Tests
{
    /// <summary>
    /// P1 租户级系统消息单元测试。
    /// 验证后台任务（无租户请求上下文）通过 AddSysUserMsg(..., tenantId) 重载发送的消息，
    /// 能正确落入目标租户（TenantId 不被主库默认值覆盖），且接收人为约定租户管理员 UserId=1。
    /// 每个测试方法使用独立的临时 Sqlite 文件库，避免与 LoginNoticeTests 共享静态 DbScoped.SugarScope 造成竞态。
    /// </summary>
    public class TenantMessageTests
    {
        static TenantMessageTests()
        {
            // 测试不在宿主中运行：注入空配置，使 App.MainDbConfigId / GetCurrentTenantId 不抛 NRE
            InternalApp.Configuration = new ConfigurationBuilder().Build();
            // 雪花ID主键生成需要初始化 WorkId（宿主在 Program.cs 设置）
            SnowFlakeSingle.WorkId = 1;
        }

        /// <summary>
        /// 为每个测试准备独立的临时 Sqlite 库（建 sys_user_msg 表），并返回查询连接。
        /// </summary>
        private static ISqlSugarClient EnsureDb()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"tenantmsg_{Guid.NewGuid():N}.db");
            DbScoped.SugarScope = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "0",
                DbType = DbType.Sqlite,
                ConnectionString = $"DataSource={dbPath};",
                IsAutoCloseConnection = true,
            });
            var db = DbScoped.SugarScope.GetConnection("0");
            // 手动建表，确保与实体一致的列且允许 NULL（避免 CodeFirst 对 long? 生成 NOT NULL 约束的差异）
            db.Ado.ExecuteCommand(@"
                CREATE TABLE IF NOT EXISTS sys_user_msg (
                    MsgId INTEGER PRIMARY KEY,
                    UserId INTEGER,
                    Content TEXT,
                    IsRead INTEGER,
                    AddTime TEXT,
                    TargetId INTEGER,
                    MsgType INTEGER,
                    IsDelete INTEGER,
                    FromUserid INTEGER,
                    TenantId TEXT
                );");
            return db;
        }

        private static SysUserMsgService BuildService()
        {
            var notifierMock = new Mock<IMessageNotifier>();
            notifierMock
                .Setup(x => x.NotifyUserAsync(It.IsAny<long>(), It.IsAny<object>()))
                .Returns(Task.CompletedTask);
            return new SysUserMsgService(notifierMock.Object);
        }

        [Fact]
        public void AddSysUserMsg_带租户重载_消息落入指定租户且接收人为管理员()
        {
            var db = EnsureDb();
            var svc = BuildService();

            const string tenantId = "tenant-xyz";
            const string content = "您的租户已暂停服务，原因：欠费";
            svc.AddSysUserMsg(1, content, UserMsgType.SYSTEM, tenantId);

            var rows = db.Queryable<SysUserMsg>().ToList();
            Assert.Single(rows);
            var row = rows[0];

            // 核心断言：TenantId 必须精确等于传入值（不被主库默认覆盖）
            Assert.Equal(tenantId, row.TenantId);
            // 接收人为约定租户管理员 UserId=1
            Assert.Equal(1L, row.UserId);
            Assert.Equal(content, row.Content);
            Assert.Equal((int)UserMsgType.SYSTEM, (int)row.MsgType);
        }

        [Fact]
        public void AddSysUserMsg_显式租户优先于自动填充_不被当前上下文覆盖()
        {
            var db = EnsureDb();
            var svc = BuildService();

            // 后台任务场景：GetCurrentTenantId() 此时为空/主库，但显式传入的 tenantId 必须保留
            var explicitTenant = "tenant-abc";
            svc.AddSysUserMsg(1, "您的租户已续费成功", UserMsgType.SYSTEM, explicitTenant);

            var row = db.Queryable<SysUserMsg>().First();
            Assert.Equal(explicitTenant, row.TenantId);
            Assert.NotEqual(string.Empty, row.TenantId);
        }

        [Fact]
        public void AddSysUserMsg_不同租户消息相互隔离()
        {
            var db = EnsureDb();
            var svc = BuildService();

            svc.AddSysUserMsg(1, "租户A消息", UserMsgType.SYSTEM, "tenant-A");
            svc.AddSysUserMsg(1, "租户B消息", UserMsgType.SYSTEM, "tenant-B");

            Assert.Equal(2, db.Queryable<SysUserMsg>().Count());
            Assert.Equal(1, db.Queryable<SysUserMsg>().Where(m => m.TenantId == "tenant-A").Count());
            Assert.Equal(1, db.Queryable<SysUserMsg>().Where(m => m.TenantId == "tenant-B").Count());
        }
    }
}
