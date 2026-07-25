using System;
using System.Collections.Generic;
using System.Net;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Moq;
using SqlSugar;
using SqlSugar.IOC;
using Xunit;
using ZR.Infrastructure.IPTools;
using ZR.Model;
using ZR.Model.System;
using ZR.ServiceCore.Services;
using IP2Region.Net.Abstractions;

namespace ZR.Tests
{
    /// <summary>
    /// 异地登录提醒（SysLoginService.GetAbnormalLoginNotice）单元测试。
    /// 通过注入假的 ISearcher 让 IpTool 在测试环境可用（无需真实 ip2region 数据文件），
    /// 并用 Moq 捕获推送的站内信内容，验证返回的纯文本格式与字段。
    /// </summary>
    public class LoginNoticeTests
    {
        // 测试不在宿主中运行：注入空配置 + 假的 IP 解析器 + 最小 SqlSugar 连接（仅供基类构造函数通过）
        static LoginNoticeTests()
        {
            InternalApp.Configuration = new ConfigurationBuilder().Build();
            IpTool.Configure(new FakeSearcher());
            DbScoped.SugarScope = new SqlSugarScope(new List<ConnectionConfig>
            {
                new ConnectionConfig
                {
                    ConfigId = "0",
                    DbType = DbType.Sqlite,
                    ConnectionString = "DataSource=:memory:",
                    IsAutoCloseConnection = true,
                },
            });
        }

        private static (SysLoginService svc, Func<string> GetContent) BuildService()
        {
            string captured = null;
            var userMsgMock = new Mock<ISysUserMsgService>();
            userMsgMock
                .Setup(x => x.AddSysUserMsg(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<UserMsgType>()))
                .Callback<long, string, UserMsgType>((_, content, _) => captured = content);

            var sysUser = new Mock<ISysUserService>().Object;
            var httpCtx = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object;
            var localizer = new Mock<Microsoft.Extensions.Localization.IStringLocalizer<ZR.ServiceCore.Resources.SharedResource>>().Object;

            return (new SysLoginService(sysUser, userMsgMock.Object, httpCtx, localizer), () => captured);
        }

        [Fact]
        public void GetAbnormalLoginNotice_异地登录_返回纯文本提醒并推送站内信()
        {
            var (svc, getContent) = BuildService();
            var user = new SysUser { UserId = 1, UserName = "admin", LoginIP = "1.2.3.4" };

            var msg = svc.GetAbnormalLoginNotice(user, "5.6.7.8");

            // 返回非空且推送的站内信内容与返回一致
            Assert.False(string.IsNullOrEmpty(msg));
            Assert.Equal(msg, getContent());

            // 纯文本结构与字段断言
            Assert.Contains("⚠️ 账号异地登录提醒", msg);
            Assert.Contains("检测到您的账号发生异地登录，请确认是否为本人操作：", msg);
            Assert.Contains("账号：admin", msg);
            Assert.Contains("本次地点：北京市-北京市", msg);
            Assert.Contains("上次地点：广东省-珠海市", msg);
            Assert.Contains("登录 IP：5.6.7.8", msg);
            Assert.Contains("如非本人操作，请立即修改密码！", msg);
            // 确认不含 Markdown 标记
            Assert.DoesNotContain("**", msg);
            Assert.DoesNotContain("- ", msg);
            Assert.DoesNotContain("> ", msg);
        }

        [Fact]
        public void GetAbnormalLoginNotice_同地点_不触发提醒()
        {
            var (svc, _) = BuildService();
            var user = new SysUser { UserId = 1, UserName = "admin", LoginIP = "1.2.3.4" };

            // 本次与上次 IP 解析为同一地点 → 返回空字符串
            var msg = svc.GetAbnormalLoginNotice(user, "1.2.3.4");

            Assert.Equal(string.Empty, msg);
        }

        [Fact]
        public void GetAbnormalLoginNotice_无效用户_返回空()
        {
            var (svc, _) = BuildService();

            var msg = svc.GetAbnormalLoginNotice(null, "5.6.7.8");
            Assert.Equal(string.Empty, msg);

            var disabled = new SysUser { UserId = 0, UserName = "x", LoginIP = "1.2.3.4" };
            Assert.Equal(string.Empty, svc.GetAbnormalLoginNotice(disabled, "5.6.7.8"));
        }

        /// <summary>
        /// 假的 IP 解析器：按 IP 返回固定区域，使测试不依赖真实 ip2region 数据库。
        /// 区域格式：国家|省份|城市|运营商
        /// </summary>
        private class FakeSearcher : ISearcher
        {
            public int IoCount => 0;

            public string Search(string ipStr) => ipStr switch
            {
                "1.2.3.4" => "中国|广东省|珠海市|电信",
                "5.6.7.8" => "中国|北京市|北京市|联通",
                _ => "中国|xx|xx|xx",
            };

            public string Search(IPAddress ipAddress) => Search(ipAddress.ToString());

            public string Search(uint ipAddress) => "中国|xx|xx|xx";

            public void Dispose() { }
        }
    }
}
