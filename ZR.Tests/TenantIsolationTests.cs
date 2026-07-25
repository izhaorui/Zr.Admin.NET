using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Xunit;
using Infrastructure;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Model;
using ZR.ServiceCore.SqlSugar;

namespace ZR.Tests
{
    /// <summary>
    /// 租户数据隔离回归测试。
    /// 直接对 TenantFilter 的 5 个主库共享实体过滤表达式编译执行（内存数据），
    /// 验证在不同租户上下文下：主租户可见全部、普通租户仅可见自己 + 通配(*)，
    /// 且任何情况下都不会泄露其他租户的数据。无需真实数据库。
    /// </summary>
    public class TenantIsolationTests
    {
        // 测试不在宿主中运行，注入空配置使 App.MainDbConfigId 回退为 "0"（主库）
        static TenantIsolationTests()
        {
            InternalApp.Configuration = new ConfigurationBuilder().Build();
        }

        private const string Main = "0";
        private const string TenantA = "t1";
        private const string TenantB = "t2";

        #region SysTasks —— 主库共享，主租户看全部，普通租户看自己 + 通配(*)

        [Fact]
        public void SysTasks_主租户_可见全部()
        {
            var data = new List<SysTasks>
            {
                new SysTasks { ID = "own", TenantId = Main },
                new SysTasks { ID = "a", TenantId = TenantA },
                new SysTasks { ID = "b", TenantId = TenantB },
                new SysTasks { ID = "wild", TenantId = "*" },
            };

            using (TenantContext.Change(Main))
            {
                var visible = data.Where(TenantFilter.SysTasksTenantFilter().Compile())
                    .Select(x => x.ID).OrderBy(x => x).ToList();

                Assert.Equal(new[] { "a", "b", "own", "wild" }, visible);
            }
        }

        [Fact]
        public void SysTasks_普通租户_仅可见自己与通配_不泄露他人()
        {
            var data = new List<SysTasks>
            {
                new SysTasks { ID = "own", TenantId = TenantA },
                new SysTasks { ID = "b", TenantId = TenantB },
                new SysTasks { ID = "main", TenantId = Main },
                new SysTasks { ID = "wild", TenantId = "*" },
            };

            using (TenantContext.Change(TenantA))
            {
                var visible = data.Where(TenantFilter.SysTasksTenantFilter().Compile())
                    .Select(x => x.ID).OrderBy(x => x).ToList();

                Assert.Equal(new[] { "own", "wild" }, visible);
                Assert.DoesNotContain("b", visible);
                Assert.DoesNotContain("main", visible);
            }
        }

        #endregion

        #region SysTasksLog —— 主库共享，主租户看全部，普通租户只看自己

        [Fact]
        public void SysTasksLog_主租户_可见全部()
        {
            var data = new List<SysTasksLog>
            {
                new SysTasksLog { JobId = "own", TenantId = Main },
                new SysTasksLog { JobId = "a", TenantId = TenantA },
                new SysTasksLog { JobId = "b", TenantId = TenantB },
            };

            using (TenantContext.Change(Main))
            {
                var visible = data.Where(TenantFilter.SysTasksLogTenantFilter().Compile()).ToList();
                Assert.Equal(3, visible.Count);
            }
        }

        [Fact]
        public void SysTasksLog_普通租户_仅可见自己()
        {
            var data = new List<SysTasksLog>
            {
                new SysTasksLog { JobId = "own", TenantId = TenantA },
                new SysTasksLog { JobId = "b", TenantId = TenantB },
                new SysTasksLog { JobId = "main", TenantId = Main },
            };

            using (TenantContext.Change(TenantA))
            {
                var visible = data.Where(TenantFilter.SysTasksLogTenantFilter().Compile())
                    .Select(x => x.JobId).ToList();

                Assert.Single(visible);
                Assert.Contains("own", visible);
            }
        }

        #endregion

        #region SysUserMsg —— 主库共享，主租户兼容器看 null/主库，普通租户仅看自己(未删除)

        [Fact]
        public void SysUserMsg_主租户_仅可见平台共享数据_不含租户私有()
        {
            var data = new List<SysUserMsg>
            {
                new SysUserMsg { MsgId = 1, TenantId = null, IsDelete = 0 },
                new SysUserMsg { MsgId = 2, TenantId = Main, IsDelete = 0 },
                new SysUserMsg { MsgId = 3, TenantId = TenantA, IsDelete = 0 },
                new SysUserMsg { MsgId = 4, TenantId = TenantA, IsDelete = 1 },
            };

            using (TenantContext.Change(Main))
            {
                var visible = data.Where(TenantFilter.SysUserMsgTenantFilter().Compile())
                    .Select(x => x.MsgId).OrderBy(x => x).ToList();

                // 消息为租户私有：主租户仅看平台共享(null/主库)，不含其他租户私有消息（与 SysTasks 行为不同）
                Assert.Equal(new[] { 1L, 2L }, visible);
            }
        }

        [Fact]
        public void SysUserMsg_普通租户_仅可见自己未删除_不泄露Null或他人()
        {
            var data = new List<SysUserMsg>
            {
                new SysUserMsg { MsgId = 1, TenantId = null, IsDelete = 0 },
                new SysUserMsg { MsgId = 2, TenantId = TenantA, IsDelete = 0 },
                new SysUserMsg { MsgId = 3, TenantId = TenantB, IsDelete = 0 },
                new SysUserMsg { MsgId = 4, TenantId = TenantA, IsDelete = 1 },
            };

            using (TenantContext.Change(TenantA))
            {
                var visible = data.Where(TenantFilter.SysUserMsgTenantFilter().Compile())
                    .Select(x => x.MsgId).ToList();

                Assert.Single(visible);
                Assert.Contains(2L, visible);
            }
        }

        #endregion

        #region SysFile / SysFileGroup —— 主库共享，主租户看全部，普通租户仅看自己

        [Fact]
        public void SysFile_主租户_仅可见平台共享数据_不含租户私有()
        {
            var data = new List<SysFile>
            {
                new SysFile { Id = 1, TenantId = null },
                new SysFile { Id = 2, TenantId = Main },
                new SysFile { Id = 3, TenantId = TenantA },
            };

            using (TenantContext.Change(Main))
            {
                var visible = data.Where(TenantFilter.SysFileTenantFilter().Compile())
                    .Select(x => x.Id).OrderBy(x => x).ToList();

                // 文件为租户私有：主租户仅看平台共享(null/主库)，不含其他租户私有文件
                Assert.Equal(new[] { 1L, 2L }, visible);
            }
        }

        [Fact]
        public void SysFile_普通租户_仅可见自己()
        {
            var data = new List<SysFile>
            {
                new SysFile { Id = 1, TenantId = null },
                new SysFile { Id = 2, TenantId = TenantA },
                new SysFile { Id = 3, TenantId = TenantB },
            };

            using (TenantContext.Change(TenantA))
            {
                var visible = data.Where(TenantFilter.SysFileTenantFilter().Compile())
                    .Select(x => x.Id).ToList();

                Assert.Single(visible);
                Assert.Contains(2L, visible);
            }
        }

        [Fact]
        public void SysFileGroup_主租户_仅可见平台共享数据_不含租户私有()
        {
            var data = new List<SysFileGroup>
            {
                new SysFileGroup { GroupId = 1, TenantId = null },
                new SysFileGroup { GroupId = 2, TenantId = Main },
                new SysFileGroup { GroupId = 3, TenantId = TenantA },
            };

            using (TenantContext.Change(Main))
            {
                var visible = data.Where(TenantFilter.SysFileGroupTenantFilter().Compile())
                    .Select(x => x.GroupId).OrderBy(x => x).ToList();

                // 文件分组为租户私有：主租户仅看平台共享(null/主库)，不含其他租户私有分组
                Assert.Equal(new[] { 1, 2 }, visible);
            }
        }

        [Fact]
        public void SysFileGroup_普通租户_仅可见自己()
        {
            var data = new List<SysFileGroup>
            {
                new SysFileGroup { GroupId = 1, TenantId = null },
                new SysFileGroup { GroupId = 2, TenantId = TenantA },
                new SysFileGroup { GroupId = 3, TenantId = TenantB },
            };

            using (TenantContext.Change(TenantA))
            {
                var visible = data.Where(TenantFilter.SysFileGroupTenantFilter().Compile())
                    .Select(x => x.GroupId).ToList();

                Assert.Single(visible);
                Assert.Contains(2, visible);
            }
        }

        #endregion

        #region 跨租户安全底线

        [Fact]
        public void 任一普通租户_均无法读取另一租户数据()
        {
            // 构造大量租户 B 的数据，验证租户 A 上下文完全不可见
            var others = Enumerable.Range(1, 50)
                .Select(i => new SysFile { Id = i, TenantId = TenantB })
                .ToList();
            var own = new SysFile { Id = 100, TenantId = TenantA };

            var data = others.Concat(new[] { own }).ToList();

            using (TenantContext.Change(TenantA))
            {
                var visible = data.Where(TenantFilter.SysFileTenantFilter().Compile()).ToList();
                Assert.All(visible, x => Assert.Equal(TenantA, x.TenantId));
                Assert.Contains(own, visible);
                Assert.DoesNotContain(data.Where(x => x.TenantId == TenantB).First(), visible);
            }
        }

        #endregion
    }
}
