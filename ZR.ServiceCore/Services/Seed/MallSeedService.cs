using Infrastructure;
using SqlSugar.IOC;
using ZR.Model.System;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 商城模块种子：菜单/按钮权限 + 商城内置定时任务。
    /// 建好后由 EnsureTenantPlanMenuSeedData 自动纳入默认套餐，租户开箱可见。
    /// </summary>
    internal sealed class MallSeedService
    {
        /// <summary>
        /// 确保商城后台管理菜单与按钮权限存在（幂等）。与租户菜单/字典等系统数据一致，通过代码而非 data.xlsx 维护。
        /// </summary>
        public string EnsureMenuSeedData()
        {
            var db = DbScoped.SugarScope;
            var now = DateTime.Now;

            // 1) 商城目录（一级目录）
            var mallMenu = db.Queryable<SysMenu>()
                .First(x => x.MenuType == "M" && x.Path == "shopping");
            if (mallMenu == null)
            {
                mallMenu = new SysMenu
                {
                    MenuName = "商城",
                    ParentId = 0,
                    OrderNum = 50,
                    Path = "shopping",
                    Component = null,
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "M",
                    Visible = "0",
                    Status = "0",
                    Perms = string.Empty,
                    Icon = "shopping",
                    MenuNameKey = "",
                    Create_by = "system",
                    Create_time = now
                };
                mallMenu.MenuId = db.Insertable(mallMenu).ExecuteReturnIdentity();
            }

            // 2) 子页面 + 按钮权限定义
            var pages = new List<(string Name, string Path, string Component, string Perms, string Icon, int OrderNum, List<(string, string, int)> Buttons)>
            {
                ("品牌管理", "brand", "shopping/Brand", "shop:brand:list", "goods", 1,
                    new() { ("查询", "shop:brand:query", 1), ("新增", "shop:brand:add", 2), ("修改", "shop:brand:edit", 3), ("删除", "shop:brand:delete", 4), ("导出", "shop:brand:export", 5) }),
                ("商品分类", "category", "shopping/Category", "shop:category:list", "tree", 2,
                    new() { ("查询", "shop:category:query", 1), ("新增", "shop:category:add", 2), ("修改", "shop:category:edit", 3), ("删除", "shop:category:delete", 4), ("导出", "shop:category:export", 5) }),
                ("商品管理", "product", "shopping/Product", "shop:product:list", "shopping", 3,
                    new() { ("查询", "shop:product:query", 1), ("新增", "shop:product:add", 2), ("修改", "shop:product:edit", 3), ("删除", "shop:product:delete", 4), ("导出", "shop:product:export", 5) }),
                ("规格模板", "spectemplate", "shopping/SpecTemplate", "spectpl:list", "operation", 4,
                    new() { ("查询", "spectpl:query", 1), ("新增", "spectpl:add", 2), ("修改", "spectpl:edit", 3), ("删除", "spectpl:delete", 4) }),
                ("库存/SKU", "skus", "shopping/Skus", "shop:skus:list", "collection", 5,
                    new() { ("查询", "shop:skus:query", 1), ("新增", "shop:skus:add", 2), ("修改", "shop:skus:edit", 3), ("删除", "shop:skus:delete", 4) }),
                ("订单管理", "order", "shopping/Order", "oms:order:list", "list", 6,
                    new() { ("查询", "oms:order:query", 1), ("发货", "oms:order:ship", 2), ("取消", "oms:order:cancel", 3), ("删除", "oms:order:delete", 4), ("导出", "oms:order:export", 5), ("销售统计", "oms:sale:query", 6) }),
                ("支付流水", "payment", "shopping/Payment", "oms:payment:list", "money", 7,
                    new() { ("查询", "oms:payment:list", 1) }),
            };

            var inserted = 0;
            foreach (var p in pages)
            {
                var pageMenu = db.Queryable<SysMenu>()
                    .First(x => x.MenuType == "C" && (x.Perms == p.Perms || x.Component == p.Component));
                if (pageMenu == null)
                {
                    pageMenu = new SysMenu
                    {
                        MenuName = p.Name,
                        ParentId = mallMenu.MenuId,
                        OrderNum = p.OrderNum,
                        Path = p.Path,
                        Component = p.Component,
                        IsCache = "0",
                        IsFrame = "0",
                        MenuType = "C",
                        Visible = "0",
                        Status = "0",
                        Perms = p.Perms,
                        Icon = p.Icon,
                        Create_by = "system",
                        Create_time = now
                    };
                    pageMenu.MenuId = db.Insertable(pageMenu).ExecuteReturnIdentity();
                    inserted++;
                }

                foreach (var btn in p.Buttons)
                {
                    var exist = db.Queryable<SysMenu>()
                        .Any(x => x.ParentId == pageMenu.MenuId && x.MenuType == "F" && x.Perms == btn.Item2);
                    if (exist) continue;

                    db.Insertable(new SysMenu
                    {
                        MenuName = btn.Item1,
                        ParentId = pageMenu.MenuId,
                        OrderNum = btn.Item3,
                        Path = string.Empty,
                        Component = string.Empty,
                        IsCache = "0",
                        IsFrame = "0",
                        MenuType = "F",
                        Visible = "0",
                        Status = "0",
                        Perms = btn.Item2,
                        Icon = "#",
                        Create_by = "system",
                        Create_time = now
                    }).ExecuteCommand();
                    inserted++;
                }
            }

            return $"[商城菜单] 新增{inserted}条菜单/权限";
        }

        /// <summary>
        /// 确保商城系统内置定时任务存在（待付款超时自动取消）。幂等，仅首次写入。
        /// 商城数据固定走 MallDb、与租户无关，TenantId 设为主库即可单次执行（OMSOrderService 内部已固定连接）。
        /// </summary>
        public string EnsureTasksSeedData()
        {
            var mainTenantId = App.MainDbConfigId;
            var db = DbScoped.SugarScope.GetConnectionScope(mainTenantId);

            if (db.Queryable<SysTasks>().ClearFilter().Any(x => x.ID == "mall_close_pending"))
                return "[商城任务] 待付款超时自动取消已存在，跳过";

            db.Insertable(new SysTasks
            {
                ID = "mall_close_pending",
                Name = "商城待付款订单超时自动取消",
                JobGroup = "mall",
                Cron = "0 0/5 * * * ?",
                AssemblyName = "ZR.Mall",
                ClassName = "Job_ClosePendingOrder",
                TriggerType = 1,
                IntervalSecond = 0,
                IsStart = 1,
                TaskType = 1,
                TenantId = mainTenantId,
                Create_by = "system"
            }).ExecuteCommand();

            return "[商城任务] 写入待付款超时自动取消";
        }

        /// <summary>
        /// 单独初始化商城模块：创建商城菜单与按钮权限，并纳入默认套餐使其对租户可见。
        /// 供 InitDb=false 时通过 InitMall 单独执行（不再随全量种子数据自动执行）。
        /// </summary>
        public List<string> InitMenuSeedData()
        {
            var result = new List<string>();
            result.Add(EnsureMenuSeedData());
            result.Add(new SeedDataService().EnsureTenantPlanMenuSeedData());
            result.Add(new SystemMenuSeedService().EnsureDbSyncMenuSeedData());
            return result;
        }
    }
}
