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
        /// 确保商城后台管理菜单与按钮权限存在（幂等）
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
                    MenuName = "商城管理",
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

            // 2) 子页面 + 按钮权限定义（用 SeedPage / SeedButton 命名属性，新增字段无需改动此处）
            var pages = new List<SeedPage>
            {
                new(Name: "品牌管理", Path: "brand", Component: "shopping/Brand", Perms: "shop:brand:list", OrderNum: 1, Icon: "star",
                    Buttons: new List<SeedButton>
                    {
                        new("查询", "shop:brand:query"), new("新增", "shop:brand:add"),
                        new("修改", "shop:brand:edit"), new("删除", "shop:brand:delete"), new("导出", "shop:brand:export"),
                    }),
                new(Name: "商品分类", Path: "category", Component: "shopping/Category", Perms: "shop:category:list", OrderNum: 2, Icon: "tree",
                    Buttons: new List<SeedButton>
                    {
                        new("查询", "shop:category:query"), new("新增", "shop:category:add"),
                        new("修改", "shop:category:edit"), new("删除", "shop:category:delete"), new("导出", "shop:category:export"),
                    }),
                new(Name: "商品管理", Path: "product", Component: "shopping/Product", Perms: "shop:product:list", OrderNum: 3, Icon: "shopping",
                    Buttons: new List<SeedButton>
                    {
                        new("查询", "shop:product:query"), new("新增", "shop:product:add"),
                        new("删除", "shop:product:delete"), new("导出", "shop:product:export"),
                    }),
                new(Name: "规格模板", Path: "spectemplate", Component: "shopping/SpecTemplate", Perms: "spectpl:list", OrderNum: 4, Icon: "zujian",
                    Buttons:
                    [
                        new("查询", "spectpl:query"), new("新增", "spectpl:add"),
                        new("修改", "spectpl:edit"), new("删除", "spectpl:delete"),
                    ]),
                new(Name: "库存/SKU", Path: "skus", Component: "shopping/Skus", Perms: "shop:skus:list", OrderNum: 5, Icon: "database",
                    Buttons:
                    [
                        new("查询", "shop:skus:query"), new("新增", "shop:skus:add"),
                        new("修改", "shop:skus:edit"), new("删除", "shop:skus:delete"),
                    ]),
                new(Name: "订单管理", Path: "order", Component: "shopping/Order", Perms: "oms:order:list", OrderNum: 6, Icon: "list",
                    Buttons:
                    [
                        new("发货", "oms:order:ship"), new("取消", "oms:order:cancel"),
                        new("删除", "oms:order:delete"), new("导出", "oms:order:export"),
                    ]),
                new(Name: "支付流水", Path: "payment", Component: "shopping/Payment", Perms: "oms:payment:list", OrderNum: 7, Icon: "money",
                    Buttons: [new("查询", "oms:payment:list")]),
                new(Name: "商品编辑", Path: "productEdit", Component: "shopping/ProductEdit", Perms: "shop:product:edit", OrderNum: 8, Visible: "1",
                    Buttons: []),
                new(Name: "订单详情", Path: "/orderDetails", Component: "order/Details", Perms: "oms:order:query", OrderNum: 8, Visible: "1",
                    Buttons: []),
                new(Name: "销售统计", Path: "salesDashboard", Component: "order/SalesDashboard", Perms: "oms:sale:query", OrderNum: 11, Visible: "0", Icon: "chart",
                    Buttons: []),
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
                        Visible = p.Visible,
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
                        .Any(x => x.ParentId == pageMenu.MenuId && x.MenuType == "F" && x.Perms == btn.Perms);
                    if (exist) continue;

                    db.Insertable(new SysMenu
                    {
                        MenuName = btn.Name,
                        ParentId = pageMenu.MenuId,
                        OrderNum = btn.OrderNum,
                        Path = string.Empty,
                        Component = string.Empty,
                        IsCache = "0",
                        IsFrame = "0",
                        MenuType = "F",
                        Visible = "0",
                        Status = "0",
                        Perms = btn.Perms,
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
            var result = new List<string>
            {
                EnsureMenuSeedData()
            };
            return result;
        }
    }
}
