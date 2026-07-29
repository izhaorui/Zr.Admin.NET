using SqlSugar.IOC;
using ZR.Model.System;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 系统菜单种子（与具体业务模块无关，如监控下的"数据库同步"）。
    /// </summary>
    internal sealed class SystemMenuSeedService
    {
        /// <summary>
        /// 确保"数据库结构同步"菜单与按钮权限存在（幂等），挂到监控目录(monitor)下。
        /// 与租户菜单/字典等系统数据一致，通过代码而非 data.xlsx 维护。
        /// </summary>
        public string EnsureDbSyncMenuSeedData()
        {
            var db = DbScoped.SugarScope;
            var now = DateTime.Now;

            // 监控一级目录
            var monitorMenu = db.Queryable<SysMenu>()
                .First(x => x.MenuType == "M" && x.Path == "monitor");
            if (monitorMenu == null) return "[数据库同步菜单] 未找到监控目录，跳过";

            // 页面菜单
            var pageMenu = db.Queryable<SysMenu>()
                .First(x => x.MenuType == "C" && x.Perms == "system:dbSync:list");
            if (pageMenu == null)
            {
                pageMenu = new SysMenu
                {
                    MenuName = "数据库同步",
                    ParentId = monitorMenu.MenuId,
                    OrderNum = 99,
                    Path = "dbsync",
                    Component = "monitor/DbSync",
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "C",
                    Visible = "0",
                    Status = "0",
                    Perms = "system:dbSync:list",
                    Icon = "ele-refresh",
                    Create_by = "system",
                    Create_time = now
                };
                pageMenu.MenuId = db.Insertable(pageMenu).ExecuteReturnIdentity();
            }

            // 按钮权限
            var buttons = new List<(string Name, string Perms, int OrderNum)>
            {
                ("预览差异", "system:dbSync:diff", 1),
                ("执行同步", "system:dbSync:sync", 2)
            };
            foreach (var b in buttons)
            {
                var exist = db.Queryable<SysMenu>()
                    .Any(x => x.ParentId == pageMenu.MenuId && x.MenuType == "F" && x.Perms == b.Perms);
                if (exist) continue;

                db.Insertable(new SysMenu
                {
                    MenuName = b.Name,
                    ParentId = pageMenu.MenuId,
                    OrderNum = b.OrderNum,
                    Path = string.Empty,
                    Component = string.Empty,
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "F",
                    Visible = "0",
                    Status = "0",
                    Perms = b.Perms,
                    Icon = "#",
                    Create_by = "system",
                    Create_time = now
                }).ExecuteCommand();
            }

            return "[数据库同步菜单] 已确保存在";
        }
    }
}
