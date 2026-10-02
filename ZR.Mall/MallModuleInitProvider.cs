using Infrastructure.Model;
using Infrastructure.ModuleInit;
using SqlSugar.IOC;
using ZR.Model.System;
using ZR.ServiceCore.Services;

namespace ZR.Mall
{
    /// <summary>
    /// 商城模块初始化 Provider：商城后台管理菜单/按钮权限 + 商城内置定时任务种子。
    /// 随商城模块内聚（框架层不再持有商城菜单与任务定义），由 ModuleInitRunner 按 ModuleInit.Mall 开关调度。
    /// 建好后由 EnsureTenantPlanMenuSeedData 自动纳入默认套餐，租户开箱可见。
    /// </summary>
    /// <remarks>
    /// 放项目根而非 <c>Service/</c>：它是启动引导（实现 Infrastructure 的框架契约、只在建库/建租户时被调度），
    /// 不是业务服务；与 <c>ZR.Workflow</c> 的同名两个初始化器归属一致。
    /// </remarks>
    [AppService(ServiceType = typeof(IModuleInitProvider))]
    public class MallModuleInitProvider : IModuleInitProvider
    {
        public string ModuleName => "Mall";

        public string DisplayName => "商城";

        /// <summary>先于 Pro(800)/Saas(900) 执行，与原 ModuleInitRunner 字典枚举顺序（Mall→Workflow→Pro→Saas）等价。</summary>
        public int Order => 100;

        public bool IsEnabled(OptionsSetting options) => options.ModuleInit.Mall;

        /// <summary>
        /// 非菜单种子：商城内置定时任务（待付款超时自动取消）。
        /// </summary>
        public void Seed()
        {
            Log.WriteLine(ConsoleColor.White, EnsureTasksSeedData());
        }

        /// <summary>
        /// 菜单/按钮权限种子。
        /// </summary>
        public List<string> SeedMenu()
        {
            return new List<string>
            {
                EnsureMenuSeedData()
            };
        }

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
                new(Name: "订单管理", Path: "order", Component: "order/Index", Perms: "oms:order:list", OrderNum: 6, Icon: "list",
                    Buttons:
                    [
                        new("发货", "oms:order:ship"), new("取消", "oms:order:cancel"),
                        new("修改", "oms:order:edit"), new("退款", "oms:order:refund"),
                        new("删除", "oms:order:delete"), new("导出", "oms:order:export"),
                    ]),
                new(Name: "支付流水", Path: "payment", Component: "shopping/Payment", Perms: "oms:payment:list", OrderNum: 7, Icon: "money",
                    Buttons: [new("查询", "oms:payment:list")]),
                new(Name: "运费模板", Path: "freightTemplate", Component: "shopping/FreightTemplate", Perms: "shop:freight:list", OrderNum: 8, Icon: "documentation",
                    Buttons:
                    [
                        new("查询", "shop:freight:query"), new("新增", "shop:freight:add"),
                        new("修改", "shop:freight:edit"), new("删除", "shop:freight:delete"),
                    ]),
                new(Name: "会员管理", Path: "member", Component: "shopping/Member", Perms: "shop:member:list", OrderNum: 9, Icon: "peoples",
                    Buttons:
                    [
                        new("查询", "shop:member:query"),
                        new("启用/禁用", "shop:member:edit"),
                    ]),
                new(Name: "售后管理", Path: "afterSale", Component: "shopping/AfterSale", Perms: "shop:aftersale:list", OrderNum: 10, Icon: "form",
                    Buttons:
                    [
                        new("查询", "shop:aftersale:query"), new("审核", "shop:aftersale:audit"),
                        new("登记退款", "shop:aftersale:refund"),
                    ]),
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
        /// 确保商城系统内置定时任务存在（待付款超时自动取消、发货后自动确认收货）。幂等。
        /// SaaS 开启时 TenantId="*" 由 Job_Dispatcher 按启用租户展开；未开启时写主库标识单次执行。
        /// 注意：AssemblyName/ClassName 字符串标识不可改动，否则会破坏既有调度数据。
        /// </summary>
        public string EnsureTasksSeedData()
        {
            var mainTenantId = App.MainDbConfigId;
            var db = DbScoped.SugarScope.GetConnectionScope(mainTenantId);
            var targetTenantId = App.IsTenantEnabled() ? "*" : mainTenantId;

            var tasks = new List<SysTasks>
            {
                new()
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
                    TenantId = targetTenantId,
                    Create_by = "system"
                },
                // 每小时一轮。没有它订单会永远停在"已发货"（用户不手动点就没人推进），
                // 售后期限与商家结算都无法收口。天数见 Job_AutoConfirmReceipt。
                new()
                {
                    ID = "mall_auto_confirm",
                    Name = "商城发货后自动确认收货",
                    JobGroup = "mall",
                    Cron = "0 0 * * * ?",
                    AssemblyName = "ZR.Mall",
                    ClassName = "Job_AutoConfirmReceipt",
                    TriggerType = 1,
                    IntervalSecond = 0,
                    IsStart = 1,
                    TaskType = 1,
                    TenantId = targetTenantId,
                    Create_by = "system"
                }
            };

            var results = tasks.Select(t => EnsureOneTask(db, t, targetTenantId));
            return $"[商城任务] {string.Join("；", results)}";
        }

        /// <summary>
        /// 幂等写入单个内置任务：已存在则只在 TenantId 变化时更新 —— 不覆盖运营在后台改过的 Cron 等配置。
        /// </summary>
        private static string EnsureOneTask(ISqlSugarClient db, SysTasks task, string targetTenantId)
        {
            var existingRows = db.Queryable<SysTasks>().ClearFilter()
                .Where(x => x.ID == task.ID)
                .Select(x => x.TenantId)
                .ToList();
            if (existingRows.Count > 0)
            {
                if (string.Equals(existingRows[0], targetTenantId, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{task.Name}已存在，跳过";
                }

                db.Updateable<SysTasks>()
                    .SetColumns(x => x.TenantId == targetTenantId)
                    .Where(x => x.ID == task.ID)
                    .ExecuteCommand();
                return $"{task.Name} TenantId 已更新为 {targetTenantId}";
            }

            db.Insertable(task).ExecuteCommand();
            return $"写入{task.Name}";
        }
    }
}
