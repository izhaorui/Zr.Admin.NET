using Microsoft.Extensions.Configuration;
using MiniExcelLibs;
using SqlSugar.IOC;
using Infrastructure;
using ZR.Common;
using ZR.Model.Content;
using ZR.Model.System;
using ZR.Model.System.Tenant;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 种子数据处理
    /// </summary>
    public class SeedDataService
    {
        /// <summary>
        /// 初始化用户数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitUserData(List<SysUser> data)
        {
            data.ForEach(x =>
            {
                x.Password = "E10ADC3949BA59ABBE56E057F20F883E";
            });
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())//表示如果有where条件根据条件判断是否存在数据，不存在插入，存在不操作
                .SplitError(x => x.Item.UserName.IsEmpty(), "用户名不能为空")
                .SplitError(x => !Tools.CheckUserName(x.Item.UserName), "用户名不符合规范")
                .WhereColumns(it => it.UserId)//如果不是主键可以这样实现（多字段it=>new{it.x1,it.x2}）
                .ToStorage();
            var result = x.AsInsertable.OffIdentity().ExecuteCommand();//插入可插入部分;

            string msg = $"[用户数据] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 菜单数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitMenuData(List<SysMenu> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.MenuId)//如果不是主键可以这样实现（多字段it=>new{it.x1,it.x2}）
                .ToStorage();
            var result = x.AsInsertable.OffIdentity().ExecuteCommand();//插入可插入部分;

            string msg = $"[菜单数据] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }
        /// <summary>
        /// 角色菜单数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitRoleMenuData(List<SysRoleMenu> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => new { it.Menu_id, it.Role_id })
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();//插入可插入部分;

            string msg = $"[角色菜单] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }
        /// <summary>
        /// 初始化部门数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitDeptData(List<SysDept> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.DeptId)
                .ToStorage();
            var result = x.AsInsertable.OffIdentity().ExecuteCommand();

            string msg = $"[部门数据] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        public (string, object, object) InitPostData(List<SysPost> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.PostCode)
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();

            string msg = $"[岗位数据] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        public (string, object, object) InitRoleData(List<SysRole> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.RoleKey)
                .ToStorage();
            var result = x.AsInsertable.OffIdentity().ExecuteCommand();

            string msg = $"[角色数据] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        public (string, object, object) InitUserRoleData(List<SysUserRole> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => new { it.RoleId, it.UserId })
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();

            string msg = $"[用户角色] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 系统配置
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitConfigData(List<SysConfig> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.ConfigKey)
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();

            string msg = $"[系统配置] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 字典
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitDictType(List<SysDictType> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.DictType)
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();

            string msg = $"[字典管理] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 字典数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitDictData(List<SysDictData> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .WhereColumns(it => new { it.DictType, it.DictValue })
                .ToStorage();
            x.AsInsertable.ExecuteCommand();
            x.AsUpdateable.ExecuteCommand();

            string msg = $"[字典数据] 插入{x.InsertList.Count} 更新{x.UpdateList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 文章目录
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitArticleCategoryData(List<ArticleCategory> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                //.SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.Name)
                .ToStorage();
            x.AsInsertable.ExecuteCommand();
            x.AsUpdateable.ExecuteCommand();
            string msg = $"[文章目录] 插入{x.InsertList.Count} 更新{x.UpdateList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 文章话题
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitArticleTopicData(List<ArticleTopic> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .WhereColumns(it => it.TopicName)
                .ToStorage();
            x.AsInsertable.ExecuteCommand();
            x.AsUpdateable.ExecuteCommand();
            string msg = $"[文章话题] 插入{x.InsertList.Count} 更新{x.UpdateList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 任务
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitTaskData(List<SysTasks> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .SplitInsert(it => it.NotAny())
                .WhereColumns(it => it.Name)
                .ToStorage();
            var result = x.AsInsertable.ExecuteCommand();

            string msg = $"[任务数据] 插入{x.InsertList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 公告数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitNoticeData(List<SysNotice> data)
        {
            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .WhereColumns(it => new { it.NoticeId })
                .ToStorage();
            x.AsInsertable.ExecuteCommand();
            x.AsUpdateable.ExecuteCommand();

            string msg = $"[通知公告数据] 插入{x.InsertList.Count} 更新{x.UpdateList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 租户数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public (string, object, object) InitTenantData(List<SysTenant> data)
        {
            data ??= [];
            data = data.Where(x => !string.IsNullOrWhiteSpace(x.TenantId)).ToList();

            var mainDb = App.MainDbConfigId;
            if (!data.Any(x => string.Equals(x.TenantId, mainDb, StringComparison.OrdinalIgnoreCase)))
            {
                data.Add(new SysTenant
                {
                    TenantId = mainDb,
                    TenantName = "默认租户",
                    CompanyName = "默认租户",
                    Status = 0,
                    DelFlag = 0,
                    Remark = "种子初始化自动补齐"
                });
            }

            var db = DbScoped.SugarScope;
            var x = db.Storageable(data)
                .WhereColumns(it => it.TenantId)
                .ToStorage();

            x.AsInsertable.ExecuteCommand();
            x.AsUpdateable.ExecuteCommand();

            string msg = $"[租户数据] 插入{x.InsertList.Count} 更新{x.UpdateList.Count} 错误{x.ErrorList.Count} 总共{x.TotalList.Count}";
            return (msg, x.ErrorList, x.IgnoreList);
        }

        /// <summary>
        /// 初始化种子数据
        /// </summary>
        /// <param name="path"></param>
        /// <param name="clean"></param>
        /// <returns></returns>
        public List<string> InitSeedData(string path, bool clean)
        {
            List<string> result = new();

            var db = DbScoped.SugarScope;

            // 先读取 Excel 数据（不依赖事务）
            var sysUser = MiniExcel.Query<SysUser>(path, sheetName: "user").ToList();
            var sysPost = MiniExcel.Query<SysPost>(path, sheetName: "post").ToList();
            var sysRole = MiniExcel.Query<SysRole>(path, sheetName: "role").ToList();
            var sysUserRole = MiniExcel.Query<SysUserRole>(path, sheetName: "user_role").ToList();
            var sysMenu = MiniExcel.Query<SysMenu>(path, sheetName: "menu").ToList();
            var sysConfig = MiniExcel.Query<SysConfig>(path, sheetName: "config").ToList();
            var sysRoleMenu = MiniExcel.Query<SysRoleMenu>(path, sheetName: "role_menu").ToList();
            var sysDict = MiniExcel.Query<SysDictType>(path, sheetName: "dict_type").ToList();
            var sysDictData = MiniExcel.Query<SysDictData>(path, sheetName: "dict_data").ToList();
            var sysDept = MiniExcel.Query<SysDept>(path, sheetName: "dept").ToList();
            var sysArticleCategory = MiniExcel.Query<ArticleCategory>(path, sheetName: "article_category").ToList();
            var sysNotice = MiniExcel.Query<SysNotice>(path, sheetName: "notice").ToList();

            List<SysTenant> sysTenant = [];
            try
            {
                sysTenant = MiniExcel.Query<SysTenant>(path, sheetName: "tenant").ToList();
            }
            catch
            {
                // data.xlsx 里无 tenant sheet 时，自动回落到默认租户。
            }

            try
            {
                db.Ado.BeginTran();

                if (clean)
                {
                    db.DbMaintenance.TruncateTable<SysRoleDept>();
                    db.DbMaintenance.TruncateTable<SysRoleMenu>();
                    db.DbMaintenance.TruncateTable<SysMenu>();
                    db.DbMaintenance.TruncateTable<SysRole>();
                    db.DbMaintenance.TruncateTable<SysUser>();
                    db.DbMaintenance.TruncateTable<SysDept>();
                    db.DbMaintenance.TruncateTable<SysPost>();
                    db.DbMaintenance.TruncateTable<SysDictType>();
                    db.DbMaintenance.TruncateTable<SysDictData>();
                    db.DbMaintenance.TruncateTable<SysNotice>();
                    db.DbMaintenance.TruncateTable<SysUserRole>();
                    db.DbMaintenance.TruncateTable<SysTenant>();
                    db.DbMaintenance.TruncateTable<SysTenantPlan>();
                    db.DbMaintenance.TruncateTable<SysTenantPlanBinding>();
                    db.DbMaintenance.TruncateTable<SysTenantPlanMenu>();
                }

                result.Add(InitUserData(sysUser).Item1);
                result.Add(InitPostData(sysPost).Item1);
                result.Add(InitRoleData(sysRole).Item1);
                result.Add(InitUserRoleData(sysUserRole).Item1);
                result.Add(InitMenuData(sysMenu).Item1);
                result.Add(InitConfigData(sysConfig).Item1);
                result.Add(InitRoleMenuData(sysRoleMenu).Item1);
                result.Add(InitDictType(sysDict).Item1);
                result.Add(InitDictData(sysDictData).Item1);
                result.Add(InitDeptData(sysDept).Item1);
                result.Add(InitArticleCategoryData(sysArticleCategory).Item1);
                result.Add(InitNoticeData(sysNotice).Item1);
                result.Add(InitTenantData(sysTenant).Item1);
                result.Add(EnsureTenantMenuSeedData());
                result.Add(EnsureTenantPlanMenuSeedData());
                result.Add(EnsureTenantDictSeedData());
                result.Add(EnsureDailyScheduleMenuSeedData());

                db.Ado.CommitTran();
            }
            catch (Exception ex)
            {
                db.Ado.RollbackTran();
                result.Add($"[种子数据初始化失败] 事务已回滚: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 补齐租户菜单与权限(system:tenant:*)，并授权给管理员角色。
        /// </summary>
        /// <returns></returns>
        private string EnsureTenantMenuSeedData()
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
        private string EnsureTenantPlanMenuSeedData()
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
                var existingCount = db.Queryable<SysTenantPlanMenu>().Count(x => x.PlanCode == plan.PlanCode);
                if (existingCount > 0)
                    continue;

                var entities = filteredMenuIds.Select(menuId => new SysTenantPlanMenu
                {
                    PlanCode = plan.PlanCode,
                    MenuId = menuId,
                    Create_by = "system",
                    Create_time = now
                }).ToList();

                if (entities.Count > 0)
                {
                    db.Insertable(entities).ExecuteCommand();
                    insertedCount += entities.Count;
                }
            }

            return $"[套餐菜单] 为默认套餐写入{insertedCount}条菜单";
        }


        /// <summary>
        /// 为主租户写入系统字典种子数据（SysDictType + SysDictData）
        /// </summary>
        private string EnsureTenantDictSeedData()
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
        /// 补齐个人待办菜单与按钮权限，并授权给所有角色（个人功能对全员可见）
        /// </summary>
        private string EnsureDailyScheduleMenuSeedData()
        {
            var db = DbScoped.SugarScope;
            var now = DateTime.Now;

            // 1) 保证一级目录"个人办公"存在
            var officeMenu = db.Queryable<SysMenu>()
                .Where(x => x.MenuType == "M" && x.Path == "personal")
                .First();
            if (officeMenu == null)
            {
                officeMenu = new SysMenu
                {
                    MenuName = "个人办公",
                    ParentId = 0,
                    OrderNum = 50,
                    Path = "personal",
                    Component = null,
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "M",
                    Visible = "0",
                    Status = "0",
                    Perms = string.Empty,
                    Icon = "list",
                    Create_by = "system",
                    Create_time = now
                };
                officeMenu.MenuId = db.Insertable(officeMenu).ExecuteReturnIdentity();
            }

            // 2) 保证"日程管理"菜单存在
            var scheduleMenu = db.Queryable<SysMenu>()
                .Where(x => x.MenuType == "C" && x.Perms == "dailyschedule:list")
                .First();
            if (scheduleMenu == null)
            {
                scheduleMenu = new SysMenu
                {
                    MenuName = "日程管理",
                    ParentId = officeMenu.MenuId,
                    OrderNum = 1,
                    Path = "dailyschedule",
                    Component = "system/dailyschedule/index",
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "C",
                    Visible = "0",
                    Status = "0",
                    Perms = "dailyschedule:list",
                    Icon = "ele-Bell",
                    Create_by = "system",
                    Create_time = now
                };
                scheduleMenu.MenuId = db.Insertable(scheduleMenu).ExecuteReturnIdentity();
            }

            // 3) 补齐按钮权限
            var buttonSeed = new List<(string Name, string Perms, int OrderNum)>
            {
                ("查询", "dailyschedule:query", 1),
                ("新增", "dailyschedule:add", 2),
                ("修改", "dailyschedule:edit", 3),
                ("删除", "dailyschedule:remove", 4)
            };
            foreach (var item in buttonSeed)
            {
                var exists = db.Queryable<SysMenu>()
                    .Any(x => x.ParentId == scheduleMenu.MenuId && x.MenuType == "F" && x.Perms == item.Perms);
                if (exists) continue;
                db.Insertable(new SysMenu
                {
                    MenuName = item.Name,
                    ParentId = scheduleMenu.MenuId,
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
            }

            //// 6) 授权给所有角色（个人功能对全员可见）
            //var allMenuIds = db.Queryable<SysMenu>()
            //    .Where(x => x.MenuId == officeMenu.MenuId || x.MenuId == scheduleMenu.MenuId || x.ParentId == scheduleMenu.MenuId)
            //    .Select(x => x.MenuId)
            //    .ToList();

            //var roleIds = db.Queryable<SysRole>().Where(x => x.DelFlag == 0).Select(x => x.RoleId).ToList();
            //foreach (var roleId in roleIds)
            //{
            //    foreach (var menuId in allMenuIds)
            //    {
            //        var has = db.Queryable<SysRoleMenu>().Any(x => x.Role_id == roleId && x.Menu_id == menuId);
            //        if (has) continue;
            //        db.Insertable(new SysRoleMenu
            //        {
            //            Role_id = roleId,
            //            Menu_id = menuId
            //        }).ExecuteCommand();
            //    }
            //}

            return $"[日程管理菜单]";
        }
    }
}
