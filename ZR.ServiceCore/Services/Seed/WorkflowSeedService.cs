using SqlSugar.IOC;
using ZR.Model.System;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 工作流模块种子：菜单/按钮权限。建好后由 EnsureTenantPlanMenuSeedData 自动纳入默认套餐，租户开箱可见。
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

            // 1) 一级目录 A：流程管理（定义 / 配置相关）
            var wfMenu = db.Queryable<SysMenu>()
                .First(x => x.MenuType == "M" && x.Path == "wf-set");
            if (wfMenu == null)
            {
                wfMenu = new SysMenu
                {
                    MenuName = "流程管理",
                    ParentId = 0,
                    OrderNum = 50,
                    Path = "wf-set",
                    Component = null,
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "M",
                    Visible = "0",
                    Status = "0",
                    Perms = string.Empty,
                    Icon = "cascader",
                    RouteName = "WfFlowDefinition",
                    Create_by = "system",
                    Create_time = now
                };
                wfMenu.MenuId = db.Insertable(wfMenu).ExecuteReturnIdentity();
            }

            // 1.1) 一级目录 B：流程中心（运行态：我的流程 / 待办 / 已办 / 记录 / 抄送 / 数据面板）
            var wfRunMenu = db.Queryable<SysMenu>()
                .First(x => x.MenuType == "M" && x.Path == "workflow");
            if (wfRunMenu == null)
            {
                wfRunMenu = new SysMenu
                {
                    MenuName = "流程中心",
                    ParentId = 0,
                    OrderNum = 51,
                    Path = "workflow",
                    Component = null,
                    IsCache = "0",
                    IsFrame = "0",
                    MenuType = "M",
                    Visible = "0",
                    Status = "0",
                    Perms = string.Empty,
                    Icon = "database",
                    RouteName = "workflow",
                    Create_by = "system",
                    Create_time = now
                };
                wfRunMenu.MenuId = db.Insertable(wfRunMenu).ExecuteReturnIdentity();
            }

            // 2) 子页面 + 按钮权限定义（用 SeedPage / SeedButton 命名属性，新增字段无需改动此处）
            //    Category: 0=流程管理(定义/配置)，1=流程中心(运行态)
            //    Visible 默认 "0"（侧边栏可见）；apply/resubmit/approval 为跳转入口页，设 "1"（隐藏，仅用于动态路由注册）
            var pages = new List<SeedPage>
            {
                // —— 流程管理（定义 / 配置）——
                new(Name: "流程定义", Path: "definition", Component: "workflow/flowDefinition/index", Perms: "workflow:definition:list", OrderNum: 1, Category: 0, Icon: "dict",
                    Buttons: [
                        new("新增", "workflow:definition:add"), new("修改", "workflow:definition:edit"), new("删除", "workflow:definition:delete"),
                    ]),
                // 表单模板：可复用动态表单的管理页（供流程设计器"载入模板"复用）
                new(Name: "表单模板", Path: "formTemplate", Component: "workflow/formTemplate/index", Perms: "workflow:template:list", OrderNum: 2, Category: 0, Icon: "list",
                    Buttons: [
                        new("新增", "workflow:template:add"), new("修改", "workflow:template:edit"), new("删除", "workflow:template:delete"),
                    ]),
                // 作为工作流目录下的隐藏子页面（Visible="1"），仅用于动态路由注册，对应前端 edit.vue
                new(Name: "流程定义设计", Path: "definition-edit", Component: "workflow/flowDefinition/edit", Perms: "workflow:definition:edit", Icon: "build", OrderNum: 8, RouteName: "WfFlowDefinitionEdit", Visible: "1", Category: 0,
                    Buttons: []),
                // —— 流程中心（运行态）——
                new(Name: "数据面板", Path: "dashboard", Component: "workflow/dashboard/index", Perms: "workflow:instance:list", OrderNum: 0, RouteName: "WfDashboard", Category: 1, Icon: "dashboard",
                    Buttons: []),
                new(Name: "我发起的", Path: "my", Component: "workflow/instance/index", Perms: "workflow:instance:list", OrderNum: 1, Category: 1, Icon: "guide",
                    Buttons: [
                        new("发起", "workflow:instance:start"), 
                        new("撤回", "workflow:instance:withdraw")
                    ]),
                new(Name: "待我审批", Path: "todo", Component: "workflow/todo/index", Perms: "workflow:task:list", OrderNum: 2, Category: 1, Icon: "gonggao",
                    Buttons:
                    [
                        new("通过", "workflow:task:approve"), new("驳回", "workflow:task:reject"), new("转办", "workflow:task:transfer"),
                        new("加签", "workflow:task:addsign"), new("评论", "workflow:comment:list"), new("发表评论", "workflow:comment:add"),
                    ]),
                new(Name: "已办任务", Path: "done", Component: "workflow/done/index", Perms: "workflow:task:list", OrderNum: 3, Category: 1, Icon: "log",
                    Buttons: []),
                new(Name: "审批记录", Path: "record", Component: "workflow/record/index", Perms: "workflow:record:list", OrderNum: 4, Category: 1, Icon: "form",
                    Buttons: []),
                new(Name: "抄送给我", Path: "cc", Component: "workflow/cc/index", Perms: "workflow:record:cc", OrderNum: 5, RouteName: "WfCc", Category: 1, Icon: "guide",
                    Buttons: []),
                // 跳转入口页：发起申请 / 重新提交 / 流程审批（不在侧边栏展示，仅用于动态路由注册）
                new(Name: "发起申请", Path: "apply", Component: "workflow/apply/index", Perms: "", OrderNum: 6, RouteName: "WfApply", Visible: "1", Category: 1,
                    Buttons: []),
                new(Name: "重新提交", Path: "resubmit", Component: "workflow/resubmit/index", Perms: "", OrderNum: 7, RouteName: "WfResubmit", Visible: "1", Category: 1,
                    Buttons: []),
                new(Name: "流程审批", Path: "approval", Component: "workflow/todo/approval", Perms: "", OrderNum: 8, RouteName: "WfApproval", Visible: "1", Category: 1,
                    Buttons: []),
            };

            var inserted = 0;
            foreach (var p in pages)
            {
                var pageMenu = db.Queryable<SysMenu>()
                    .First(x => x.MenuType == "C" && x.Component == p.Component);
                var expectParent = p.Category == 0 ? wfMenu.MenuId : wfRunMenu.MenuId;
                if (pageMenu == null)
                {
                    pageMenu = new SysMenu
                    {
                        MenuName = p.Name,
                        ParentId = expectParent,
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

            // 3) 工作流所有按钮权限（F 类型，含上述 pages 里的全部 Buttons）默认授予所有角色。
            //    原因：Controller 里凡声明 [ActionPermissionFilter] 的接口，普通角色必须配套拥有该权限，
            //    否则访问即被过滤器拦截（401/403）。超管角色（admin）在过滤器中天然放行，此处补齐普通审批角色。
            //    覆盖范围：definition/template 的 add/edit/delete、task 的 approve/reject/transfer/addsign、
            //    instance 的 start/withdraw、record 的 cc、comment 的 list/add 等全部按钮权限。
            var allButtonPerms = pages.SelectMany(p => p.Buttons.Select(b => b.Perms)).Distinct().ToList();
            var buttonMenuIds = db.Queryable<SysMenu>()
                .Where(x => x.MenuType == "F" && allButtonPerms.Contains(x.Perms))
                .Select(x => x.MenuId)
                .ToList();
            if (buttonMenuIds.Count > 0)
            {
                var roleIds = db.Queryable<SysRole>().Select(r => r.RoleId).ToList();
                var existRoleMenus = db.Queryable<SysRoleMenu>()
                    .Where(rm => buttonMenuIds.Contains(rm.Menu_id))
                    .ToList();
                var toInsert = new List<SysRoleMenu>();
                foreach (var roleId in roleIds)
                {
                    foreach (var menuId in buttonMenuIds)
                    {
                        if (!existRoleMenus.Any(rm => rm.Role_id == roleId && rm.Menu_id == menuId))
                        {
                            toInsert.Add(new SysRoleMenu { Role_id = roleId, Menu_id = menuId, Create_by = "system", Create_time = now });
                        }
                    }
                }
                if (toInsert.Count > 0)
                {
                    db.Insertable(toInsert).ExecuteCommand();
                    inserted += toInsert.Count;
                }
            }

            return $"[工作流菜单] 新增{inserted}条菜单/权限";
        }

        /// <summary>
        /// 单独初始化工作流模块：创建工作流菜单与按钮权限，并纳入默认套餐使其对租户可见。
        /// 由 CLI --initdb 触发，并按 appsettings 的 InitWorkflow 开关决定是否执行。
        /// </summary>
        public List<string> InitMenuSeedData()
        {
            var result = new List<string>
            {
                EnsureMenuSeedData(),
            };
            return result;
        }
    }
}
