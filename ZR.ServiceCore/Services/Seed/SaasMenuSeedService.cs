using Infrastructure;
using Microsoft.Extensions.Configuration;
using SqlSugar.IOC;
using ZR.Model.System;
using ZR.Model.System.Tenant;

namespace ZR.ServiceCore.Services.Seed
{
    /// <summary>
    /// SaaS 独立种子菜单服务：不绑定业务模块（区别于商城/工作流模块的建表+菜单种子），
    /// 仅负责补齐 SaaS 平台级菜单与权限（租户管理、套餐菜单、字典种子、日程管理等）。
    /// 由 appsettings 的 InitSeedMenu 开关统一驱动——仅当开关开启时才写入。
    /// </summary>
    public class SaasMenuSeedService
    {
        /// <summary>
        /// 补齐租户菜单与权限(system:tenant:*)，并授权给管理员角色。
        /// </summary>
        public string EnsureTenantMenuSeedData()
        {
            var db = DbScoped.SugarScope;
            var now = DateTime.Now;

            // 1) 保证 SaaS 管理目录存在（一级目录）
            var saasMenu = db.Queryable<SysMenu>()
                .Where(x => x.MenuType == "M" && x.Path == "saas")
                .First();

            if (saasMenu == null)
            {
                saasMenu = new SysMenu
                {
                    MenuName = "租户管理",
                    ParentId = 0,
                    OrderNum = 2,
                    Path = "saas",
                    Component = null,
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "M",
                    Visible = "0",
                    Status = "0",
                    Perms = string.Empty,
                    Icon = "chart",
                    MenuNameKey = "menu.tenantMenu",
                    Create_by = "system",
                    Create_time = now
                };
                saasMenu.MenuId = db.Insertable(saasMenu).ExecuteReturnIdentity();
            }

            // 2) 保证租户管理菜单存在
            var tenantMenu = db.Queryable<SysMenu>()
                .Where(x => x.MenuType == "C" && (x.Path == "tenant" || x.Perms == "system:tenant:list"))
                .First();

            if (tenantMenu == null)
            {
                tenantMenu = new SysMenu
                {
                    MenuName = "租户管理",
                    MenuNameKey = "menu.tenantMenu",
                    ParentId = saasMenu.MenuId,
                    OrderNum = 11,
                    Path = "tenant",
                    Component = "system/tenant/index",
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "C",
                    Visible = "0",
                    Status = "0",
                    Perms = "system:tenant:list",
                    Icon = "list",
                    Create_by = "system",
                    Create_time = now
                };
                tenantMenu.MenuId = db.Insertable(tenantMenu).ExecuteReturnIdentity();
            }
            else if (tenantMenu.ParentId != saasMenu.MenuId)
            {
                tenantMenu.ParentId = saasMenu.MenuId;
                tenantMenu.Update_by = "system";
                tenantMenu.Update_time = now;
                db.Updateable(tenantMenu).UpdateColumns(x => new { x.ParentId, x.Update_by, x.Update_time }).ExecuteCommand();
            }

            // 3) 补齐租户按钮权限
            var buttonSeed = new List<(string Name, string Perms, int OrderNum)>
            {
                ("查询", "system:tenant:query", 1),
                ("新增", "system:tenant:add", 2),
                ("修改", "system:tenant:update", 3),
                ("删除", "system:tenant:remove", 4)
            };

            var insertedMenus = 0;
            foreach (var item in buttonSeed)
            {
                var exists = db.Queryable<SysMenu>()
                    .Any(x => x.ParentId == tenantMenu.MenuId && x.MenuType == "F" && x.Perms == item.Perms);
                if (exists)
                {
                    continue;
                }

                db.Insertable(new SysMenu
                {
                    MenuName = item.Name,
                    ParentId = tenantMenu.MenuId,
                    OrderNum = item.OrderNum,
                    Path = string.Empty,
                    Component = string.Empty,
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "F",
                    Visible = "0",
                    Status = "0",
                    Perms = item.Perms,
                    Icon = "#",
                    Create_by = "system",
                    Create_time = now
                }).ExecuteCommand();

                insertedMenus++;
            }

            // 4) 保证套餐管理菜单存在（从旧"套餐菜单管理"迁移）
            var planManageMenu = db.Queryable<SysMenu>()
                .Where(x => x.MenuType == "C" && (x.Perms == "system:tenantplanmenu:list" || x.Component == "system/tenantPlan/index"))
                .First();

            if (planManageMenu == null)
            {
                planManageMenu = new SysMenu
                {
                    MenuName = "套餐管理",
                    MenuNameKey = "menu.tenantPlan",
                    ParentId = saasMenu.MenuId,
                    OrderNum = 12,
                    Path = "tenantplan",
                    Component = "system/tenantPlan/index",
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "C",
                    Visible = "0",
                    Status = "0",
                    Perms = "system:tenant:list",
                    Icon = "tree-table",
                    Create_by = "system",
                    Create_time = now
                };
                planManageMenu.MenuId = db.Insertable(planManageMenu).ExecuteReturnIdentity();
                insertedMenus++;
            }
            else
            {
                var needUpdate = planManageMenu.ParentId != saasMenu.MenuId
                    || planManageMenu.Path != "tenantplan"
                    || planManageMenu.Component != "system/tenantPlan/index"
                    || planManageMenu.Perms != "system:tenant:list"
                    || planManageMenu.MenuName != "套餐管理";
                if (needUpdate)
                {
                    planManageMenu.ParentId = saasMenu.MenuId;
                    planManageMenu.Path = "tenantplan";
                    planManageMenu.Component = "system/tenantPlan/index";
                    planManageMenu.Perms = "system:tenant:list";
                    planManageMenu.MenuName = "套餐管理";
                    planManageMenu.Update_by = "system";
                    planManageMenu.Update_time = now;
                    db.Updateable(planManageMenu)
                        .UpdateColumns(x => new { x.ParentId, x.Path, x.Component, x.Perms, x.MenuName, x.Update_by, x.Update_time })
                        .ExecuteCommand();
                }
            }

            // 5) 清理旧的套餐菜单按钮权限（已无需独立页面）
            var oldPlanButtons = db.Queryable<SysMenu>()
                .Where(x => x.ParentId == planManageMenu.MenuId && x.MenuType == "F")
                .ToList();
            foreach (var btn in oldPlanButtons)
            {
                db.Deleteable<SysMenu>().Where(x => x.MenuId == btn.MenuId).ExecuteCommand();
            }

            // 6) 保证"我的租户"菜单存在（子租户自助查看到期时间、套餐用量）
            var myTenantMenu = db.Queryable<SysMenu>()
                .Where(x => x.MenuType == "C" && x.Perms == "tenant:my")
                .First();

            if (myTenantMenu == null)
            {
                myTenantMenu = new SysMenu
                {
                    MenuName = "我的租户",
                    MenuNameKey = "menu.myTenant",
                    ParentId = saasMenu.MenuId,
                    OrderNum = 13,
                    Path = "my-tenant",
                    Component = "system/tenant/my",
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "C",
                    Visible = "0",
                    Status = "0",
                    Perms = "tenant:my",
                    Icon = "user",
                    Create_by = "system",
                    Create_time = now
                };
                myTenantMenu.MenuId = db.Insertable(myTenantMenu).ExecuteReturnIdentity();
                insertedMenus++;
            }
            else if (myTenantMenu.ParentId != saasMenu.MenuId)
            {
                myTenantMenu.ParentId = saasMenu.MenuId;
                myTenantMenu.Update_by = "system";
                myTenantMenu.Update_time = now;
                db.Updateable(myTenantMenu).UpdateColumns(x => new { x.ParentId, x.Update_by, x.Update_time }).ExecuteCommand();
            }

            return $"[租户菜单权限补齐] 菜单新增{insertedMenus}";
        }

        /// <summary>
        /// 为默认套餐写入所有非平台菜单作为初始菜单，确保新套餐开箱即用
        /// </summary>
        public string EnsureTenantPlanMenuSeedData()
        {
            if (!App.IsTenantEnabled())
                return "[套餐菜单] 多租户未启用，跳过";

            var db = DbScoped.SugarScope;
            var plans = db.Queryable<SysTenantPlan>().Where(x => x.DelFlag == 0).ToList();
            if (!plans.Any())
                return "[套餐菜单] 无套餐，跳过";

            var allMenus = db.Queryable<SysMenu>().Where(x => x.Status == "0").ToList();
            var platformPermPrefixes = App.Configuration.GetSection("TenantSettings:PlatformMenuPermPrefixes").Get<List<string>>()
                ?? new List<string>();

            var filteredMenuIds = allMenus
                .Where(m => !platformPermPrefixes.Any(p => !string.IsNullOrWhiteSpace(m.Perms) && m.Perms.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                .Select(m => m.MenuId)
                .ToList();

            var now = DateTime.Now;
            var insertedCount = 0;
            foreach (var plan in plans)
            {
                // 增量补充：只补「套餐里还没有」的菜单，避免整体跳过导致后续新增菜单无法纳入
                var existingMenuIds = db.Queryable<SysTenantPlanMenu>()
                    .Where(x => x.PlanCode == plan.PlanCode)
                    .Select(x => x.MenuId)
                    .ToList();

                var toAdd = filteredMenuIds
                    .Where(menuId => !existingMenuIds.Contains(menuId))
                    .Select(menuId => new SysTenantPlanMenu
                    {
                        PlanCode = plan.PlanCode,
                        MenuId = menuId,
                        Create_by = "system",
                        Create_time = now
                    })
                    .ToList();

                if (toAdd.Count > 0)
                {
                    db.Insertable(toAdd).ExecuteCommand();
                    insertedCount += toAdd.Count;
                }
            }

            return $"[套餐菜单] 为默认套餐补充{insertedCount}条菜单";
        }

        /// <summary>
        /// 为主租户写入系统字典种子数据（SysDictType + SysDictData）
        /// </summary>
        public string EnsureTenantDictSeedData()
        {
            if (!App.IsTenantEnabled())
                return "[字典种子数据] 多租户未启用，跳过";

            var mainTenantId = App.MainDbConfigId;
            var tenantDb = DbScoped.SugarScope.GetConnectionScope(mainTenantId);

            // 检查租户库是否已有字典数据（可能已被 InitDictType 写入同一 config "0"）
            if (tenantDb.Queryable<SysDictType>().Where(x => x.Type == "Y").Any())
                return "[字典种子数据] 主租户系统字典已存在，跳过";

            // 从 Excel 已写入的数据中读取系统字典
            var dictTypes = tenantDb.Queryable<SysDictType>()
                .Where(x => x.Type == "Y" && x.Status == "0")
                .ToList();

            if (dictTypes.Count == 0)
                return "[字典种子数据] 无系统字典数据，跳过";

            var typeNames = dictTypes.Select(x => x.DictType).ToList();
            var dictData = tenantDb.Queryable<SysDictData>()
                .Where(x => typeNames.Contains(x.DictType))
                .ToList();

            var now = DateTime.Now;
            foreach (var item in dictTypes)
            {
                item.Create_by = "system";
                item.Create_time = now;
            }
            foreach (var item in dictData)
            {
                item.Create_by = "system";
                item.Create_time = now;
            }

            var x1 = tenantDb.Storageable(dictTypes)
                .WhereColumns(it => it.DictType)
                .ToStorage();
            x1.AsInsertable.ExecuteCommand();

            var x2 = tenantDb.Storageable(dictData)
                .WhereColumns(it => new { it.DictType, it.DictValue })
                .ToStorage();
            x2.AsInsertable.ExecuteCommand();

            return $"[字典种子数据] 主租户写入字典类型{x1.InsertList.Count}条，字典数据{x2.InsertList.Count}条";
        }

        /// <summary>
        /// 按固定顺序执行全部 SaaS 独立种子菜单（租户/套餐/字典/日程），聚合返回日志行。
        /// 供 InitTable 在 InitSeedMenu 开关开启时统一调用。
        /// </summary>
        public List<string> EnsureAllSeedMenus()
        {
            return new List<string>
            {
                EnsureTenantMenuSeedData(),
                EnsureTenantPlanMenuSeedData(),
                EnsureTenantDictSeedData()
            };
        }
    }
}
