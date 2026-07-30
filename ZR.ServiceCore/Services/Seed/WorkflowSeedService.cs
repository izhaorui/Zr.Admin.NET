using SqlSugar.IOC;
using ZR.Model.System;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 工作流模块种子：菜单/按钮权限。建好后由 EnsureTenantPlanMenuSeedData 自动纳入默认套餐，租户开箱可见。
    /// 结构参照 ZR.Workflow/workflow_menu.sql。
    /// </summary>
    internal sealed class WorkflowSeedService
    {
        /// <summary>
        /// 确保工作流模块菜单与按钮权限存在（幂等）。与商城菜单一致，通过代码维护而非 data.xlsx。
        /// </summary>
        public string EnsureMenuSeedData()
        {
            var db = DbScoped.SugarScope;
            var now = DateTime.Now;

            // 1) 工作流目录（一级目录）
            var wfMenu = db.Queryable<SysMenu>()
                .First(x => x.MenuType == "M" && x.Path == "workflow");
            if (wfMenu == null)
            {
                wfMenu = new SysMenu
                {
                    MenuName = "流程管理",
                    ParentId = 0,
                    OrderNum = 50,
                    Path = "workflow",
                    Component = null,
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "M",
                    Visible = "0",
                    Status = "0",
                    Perms = string.Empty,
                    Icon = "cascader",
                    RouteName = "Workflow",
                    Create_by = "system",
                    Create_time = now
                };
                wfMenu.MenuId = db.Insertable(wfMenu).ExecuteReturnIdentity();
            }

            // 2) 子页面 + 按钮权限定义（Component 唯一，作为幂等键；注意部分页面 Perms 相同，故不按 Perms 去重）
            var pages = new List<(string Name, string Path, string Component, string Perms, string Icon, int OrderNum, string RouteName, List<(string, string, int)> Buttons)>
            {
                ("流程定义", "definition", "workflow/flowDefinition/index", "workflow:definition:list", "", 1, "",
                    new() { ("新增", "workflow:definition:add", 1), ("修改", "workflow:definition:edit", 2), ("删除", "workflow:definition:delete", 3) }),
                ("我的流程", "my", "workflow/instance/index", "workflow:instance:list", "", 2, "",
                    new() { ("发起", "workflow:instance:start", 1), ("撤回", "workflow:instance:withdraw", 2) }),
                ("待我审批", "todo", "workflow/todo/index", "workflow:task:list", "", 3, "",
                    new() { ("通过", "workflow:task:approve", 1), ("驳回", "workflow:task:reject", 2), ("转办", "workflow:task:transfer", 3), ("加签", "workflow:task:addsign", 4) }),
                ("已办任务", "done", "workflow/done/index", "workflow:task:list", "", 4, "",
                    new()),
                ("审批记录", "record", "workflow/record/index", "workflow:record:list", "", 5, "",
                    new()),
                ("抄送给我", "cc", "workflow/cc/index", "workflow:record:cc", "", 6, "WfCc",
                    new() { ("查看", "workflow:record:cc", 1) }),
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
                        ParentId = wfMenu.MenuId,
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

            // 3) 数据面板（OrderNum=0 排最前，按 RouteName 幂等，挂工作流目录）
            var dash = db.Queryable<SysMenu>()
                .First(x => x.MenuType == "C" && x.RouteName == "WfDashboard");
            if (dash == null)
            {
                db.Insertable(new SysMenu
                {
                    MenuName = "数据面板",
                    ParentId = wfMenu.MenuId,
                    OrderNum = 0,
                    Path = "dashboard",
                    Component = "workflow/dashboard/index",
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "C",
                    Visible = "0",
                    Status = "0",
                    Perms = "workflow:instance:list",
                    Icon = "",
                    RouteName = "WfDashboard",
                    Create_by = "system",
                    Create_time = now
                }).ExecuteCommand();
                inserted++;
            }

            return $"[工作流菜单] 新增{inserted}条菜单/权限";
        }

        /// <summary>
        /// 单独初始化工作流模块：创建工作流菜单与按钮权限，并纳入默认套餐使其对租户可见。
        /// 供 InitDb=false 时通过 InitWorkflow 单独执行（不再随全量种子数据自动执行）。
        /// </summary>
        public List<string> InitMenuSeedData()
        {
            var result = new List<string>
            {
                EnsureMenuSeedData(),
                new SeedDataService().EnsureTenantPlanMenuSeedData()
            };
            return result;
        }
    }
}
