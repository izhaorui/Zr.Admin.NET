using SqlSugar.IOC;
using ZR.Model.System;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 系统菜单种子：为 data.xlsx 之外的系统级菜单/按钮权限补种（与具体业务模块无关）。
    /// 注意：EnsureAiPermSeedData 只补挂在 data.xlsx 系统页下的 F 按钮；
    /// EnsureAiUsageMenuSeedData 维护「用量管理」(管理员) +「我的用量」(个人) +「AI 办公助手」(独立页) 三页；
    /// EnsureSsoAppMenuSeedData 维护「系统管理 - 应用接入」页及其按钮权限，
    /// 该方法由 ProModuleInitProvider 调度：应用接入页随专业版交付，开源版无此页面，不能写入菜单。
    /// </summary>
    internal sealed class SystemMenuSeedService
    {
        /// <summary>
        /// AI 能力按钮：挂在系统既有 C 页（本体由 data.xlsx 维护）下的 F 按钮。
        /// 元组含义：(父 C 页权限串, 按钮定义)。
        /// </summary>
        private static readonly (string ParentPerms, SeedButton Button)[] AiButtonDefs =
        {
            ("system:lang:list", new SeedButton("AI 翻译", "system:lang:ai", OrderNum: 99)),
            ("monitor:job:list", new SeedButton("AI 生成 Cron", "monitor:job:ai", OrderNum: 99)),
            ("tool:gen:list", new SeedButton("AI 推断列配置", "tool:gen:ai", OrderNum: 99)),
            ("monitor:logininfor:list", new SeedButton("AI 安全分析", "monitor:logininfor:ai", OrderNum: 99)),
            ("monitor:operlog:list", new SeedButton("AI 健康分析", "monitor:operlog:ai", OrderNum: 99)),
        };

        /// <summary>
        /// AI 应用子页：管理员全站（用量管理）、个人自助（我的用量）、独立 AI 办公助手。
        /// </summary>
        private static readonly SeedPage AiUsageAdminPage = new(
            Name: "用量管理",
            Path: "aiUsage",
            Component: "Ai/AiUsage",
            Perms: "ai:usage:list",
            OrderNum: 1,
            Buttons:
            [
                new SeedButton("用量导出", "ai:usage:export", OrderNum: 20),
            ],
            Icon: "chart");

        private static readonly SeedPage AiGovernancePage = new(
            Name: "AI治理",
            Path: "aiGovernance",
            Component: "Ai/AiGovernance",
            Perms: "ai:governance:list",
            OrderNum: 2,
            Buttons:
            [
                new SeedButton("治理策略查询", "ai:governance:list", OrderNum: 10),
                new SeedButton("治理策略详情", "ai:governance:query", OrderNum: 11),
                new SeedButton("治理策略新增", "ai:governance:add", OrderNum: 12),
                new SeedButton("治理策略修改", "ai:governance:edit", OrderNum: 13),
                new SeedButton("治理策略删除", "ai:governance:remove", OrderNum: 14),
                new SeedButton("Provider检测", "ai:governance:health", OrderNum: 15),
                new SeedButton("模型价格查询", "ai:price:list", OrderNum: 20),
                new SeedButton("模型价格详情", "ai:price:query", OrderNum: 21),
                new SeedButton("模型价格新增", "ai:price:add", OrderNum: 22),
                new SeedButton("模型价格修改", "ai:price:edit", OrderNum: 23),
                new SeedButton("模型价格删除", "ai:price:remove", OrderNum: 24),
            ],
            Icon: "system");

        private static readonly SeedPage AiKnowledgePage = new(
            Name: "知识库",
            Path: "aiKnowledge",
            Component: "Ai/knowledge/index",
            RouteName: "AiKnowledge",
            Perms: "ai:governance:list",
            OrderNum: 8,
            Buttons: [],
            Icon: "education");

        private static readonly SeedPage AiUsageMinePage = new(
            Name: "我的用量",
            Path: "myUsage",
            Component: "Ai/AiUsageMine",
            Perms: "ai:usage:mine",
            OrderNum: 3,
            Buttons: [],
            Icon: "user");

        /// <summary>
        /// AI 办公助手：独立页面
        /// </summary>
        private static readonly SeedPage AiChatPage = new(
            Name: "AI 办公助手",
            Path: "aiChat",
            Component: "Ai/AiChat",
            RouteName: "AiChat",
            Perms: "ai:chat",
            OrderNum: 4,
            Buttons: [],
            Icon: "message");

        /// <summary>
        /// SSO 应用接入：挂在「系统管理」目录下的页面，用于登记允许跳转本系统登录的外部应用。
        /// </summary>
        private static readonly SeedPage SsoAppPage = new(
            Name: "应用接入",
            Path: "sso",
            Component: "system/sso/index",
            Perms: "system:sso:list",
            OrderNum: 9,
            Buttons:
            [
                new SeedButton("应用查询", "system:sso:query", OrderNum: 10),
                new SeedButton("应用新增", "system:sso:add", OrderNum: 11),
                new SeedButton("应用修改", "system:sso:edit", OrderNum: 12),
                new SeedButton("应用删除", "system:sso:remove", OrderNum: 13),
                new SeedButton("重置密钥", "system:sso:resetsecret", OrderNum: 14),
            ],
            Icon: "ele-Lock",
            RouteName: "SsoApp");

        /// <summary>
        /// 确保系统模块 AI 能力按钮权限存在（幂等）。
        /// 多语言、定时任务等系统菜单本体由 data.xlsx 种子维护，此处只补齐新增的 F 类型按钮权限。
        /// 菜单未初始化时跳过，下次执行种子时自动补上。
        /// </summary>
        public string EnsureAiPermSeedData()
        {
            var db = DbScoped.SugarScope;
            var now = DateTime.Now;
            var inserted = 0;

            foreach (var (parentPerms, btn) in AiButtonDefs)
            {
                var parent = db.Queryable<SysMenu>()
                    .First(x => x.MenuType == "C" && x.Perms == parentPerms);
                if (parent == null)
                {
                    continue;
                }

                var (_, isNew) = EnsureButton(parent.MenuId, btn, now);
                if (isNew) inserted++;
            }

            return $"[系统 AI 权限] 新增{inserted}个按钮";
        }

        /// <summary>
        /// 确保 AI 应用菜单：用量管理（管理员全站）、我的用量（个人）、AI 办公助手（独立页）。
        /// 权限：ai:usage:list（管理端）、ai:usage:mine（个人端）、ai:chat（办公助手页）。
        /// </summary>
        public string EnsureAiUsageMenuSeedData()
        {
            var db = DbScoped.SugarScope;
            var now = DateTime.Now;

            var aiMenu = db.Queryable<SysMenu>()
                .First(x => x.MenuType == "M" && x.Path == "ai");
            if (aiMenu == null)
            {
                aiMenu = new SysMenu
                {
                    MenuName = "Ai应用",
                    ParentId = 0,
                    OrderNum = 50,
                    Path = "ai",
                    Component = null,
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "M",
                    Visible = "0",
                    Status = "0",
                    Perms = string.Empty,
                    Icon = "app",
                    RouteName = "ai",
                    Create_by = "system",
                    Create_time = now
                };
                aiMenu.MenuId = db.Insertable(aiMenu).ExecuteReturnIdentity();
            }

            var pages = new List<SeedPage>
            {
                AiUsageAdminPage,
                AiGovernancePage,
                AiUsageMinePage,
                AiChatPage,
                AiKnowledgePage,
            };

            var inserted = 0;
            foreach (var p in pages)
            {
                var pageMenu = db.Queryable<SysMenu>()
                    .First(x => x.MenuType == "C" && x.Component == p.Component);
                if (pageMenu == null)
                {
                    pageMenu = new SysMenu
                    {
                        MenuName = p.Name,
                        ParentId = aiMenu.MenuId,
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
                        RouteName = string.IsNullOrEmpty(p.RouteName) ? null : p.RouteName,
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

            return $"[AI 用量菜单] 新增{inserted}条菜单";
        }

        /// <summary>
        /// 确保「系统管理 - 应用接入」菜单及其按钮权限存在（幂等）。
        /// 父目录固定为「系统管理」目录的 MenuId（初始化数据中为 1）。
        /// </summary>
        public string EnsureSsoAppMenuSeedData()
        {
            var db = DbScoped.SugarScope;
            var now = DateTime.Now;

            // 「系统管理」目录在初始化数据中固定为 MenuId = 1
            const long parentId = 1;

            // SqlSugar 无法解析表达式中的私有静态字段，先取出为局部变量
            var ssoComponent = SsoAppPage.Component;

            var inserted = 0;
            var pageMenu = db.Queryable<SysMenu>()
                .First(x => x.MenuType == "C" && x.Component == ssoComponent);
            if (pageMenu == null)
            {
                pageMenu = new SysMenu
                {
                    MenuName = SsoAppPage.Name,
                    ParentId = parentId,
                    OrderNum = SsoAppPage.OrderNum,
                    Path = SsoAppPage.Path,
                    Component = SsoAppPage.Component,
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "C",
                    Visible = SsoAppPage.Visible,
                    Status = "0",
                    Perms = SsoAppPage.Perms,
                    Icon = SsoAppPage.Icon,
                    RouteName = string.IsNullOrEmpty(SsoAppPage.RouteName) ? null : SsoAppPage.RouteName,
                    Create_by = "system",
                    Create_time = now
                };
                pageMenu.MenuId = db.Insertable(pageMenu).ExecuteReturnIdentity();
                inserted++;
            }

            foreach (var btn in SsoAppPage.Buttons)
            {
                var (_, isNew) = EnsureButton(pageMenu.MenuId, btn, now);
                if (isNew) inserted++;
            }

            return $"[SSO 应用接入菜单] 新增{inserted}条菜单";
        }

        /// <summary>
        /// 确保 F 按钮存在（幂等）：缺失则插入到指定父菜单下。
        /// </summary>
        private static (long MenuId, bool Inserted) EnsureButton(long parentId, SeedButton btn, DateTime now)
        {
            var db = DbScoped.SugarScope;
            var exist = db.Queryable<SysMenu>()
                .First(x => x.MenuType == "F" && x.Perms == btn.Perms);
            if (exist != null)
            {
                return (exist.MenuId, false);
            }

            var menu = new SysMenu
            {
                MenuName = btn.Name,
                ParentId = parentId,
                OrderNum = btn.OrderNum,
                Path = string.Empty,
                Component = null,
                IsCache = "0",
                IsFrame = "0",
                MenuType = "F",
                Visible = "0",
                Status = "0",
                Perms = btn.Perms,
                Icon = "#",
                MenuNameKey = "",
                Create_by = "system",
                Create_time = now
            };
            menu.MenuId = db.Insertable(menu).ExecuteReturnIdentity();
            return (menu.MenuId, true);
        }
    }
}
