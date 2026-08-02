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
            //    visible 默认 "0"（侧边栏可见）；apply/resubmit 为跳转入口页，设 "1"（隐藏，仅用于动态路由注册）
            var pages = new List<(string Name, string Path, string Component, string Perms, string Icon, int OrderNum, string RouteName, string Visible, List<(string, string, int)> Buttons)>
            {
                ("流程定义", "definition", "workflow/flowDefinition/index", "workflow:definition:list", "", 1, "", "0",
                    new() { ("新增", "workflow:definition:add", 1), ("修改", "workflow:definition:edit", 2), ("删除", "workflow:definition:delete", 3) }),
                // 表单模板：可复用动态表单的管理页（供流程设计器"载入模板"复用）
                ("表单模板", "formTemplate", "workflow/formTemplate/index", "workflow:template:list", "", 2, "", "0",
                    new() { ("新增", "workflow:template:add", 1), ("修改", "workflow:template:edit", 2), ("删除", "workflow:template:delete", 3) }),
                // 作为工作流目录下的隐藏子页面（Visible="1"），仅用于动态路由注册，对应前端 edit.vue
                ("流程定义设计", "definition-edit", "workflow/flowDefinition/edit", "workflow:definition:edit", "build", 8, "WfFlowDefinitionEdit", "1",
                    new()),
                ("我的流程", "my", "workflow/instance/index", "workflow:instance:list", "", 3, "", "0",
                    new() { ("发起", "workflow:instance:start", 1), ("撤回", "workflow:instance:withdraw", 2) }),
                ("待我审批", "todo", "workflow/todo/index", "workflow:task:list", "", 4, "", "0",
                    new() { ("通过", "workflow:task:approve", 1), ("驳回", "workflow:task:reject", 2), ("转办", "workflow:task:transfer", 3), ("加签", "workflow:task:addsign", 4), ("评论", "workflow:comment:list", 5), ("发表评论", "workflow:comment:add", 6) }),
                ("已办任务", "done", "workflow/done/index", "workflow:task:list", "", 5, "", "0",
                    new()),
                ("审批记录", "record", "workflow/record/index", "workflow:record:list", "", 6, "", "0",
                    new()),
                ("抄送给我", "cc", "workflow/cc/index", "workflow:record:cc", "", 7, "WfCc", "0",
                    new()),
                // 跳转入口页：发起申请 / 重新提交（不在侧边栏展示，仅用于动态路由注册，前端不再写死静态路由）
                ("发起申请", "apply", "workflow/apply/index", "", "", 7, "WfApply", "1",
                    new()),
                ("重新提交", "resubmit", "workflow/resubmit/index", "", "", 8, "WfResubmit", "1",
                    new()),
                ("流程审批", "approval", "workflow/todo/approval", "", "", 8, "WfApproval", "1",
                    new()),
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

            // 4) 工作流所有按钮权限（F 类型，含上述 pages 里的全部 Buttons）默认授予所有角色。
            //    原因：Controller 里凡声明 [ActionPermissionFilter] 的接口，普通角色必须配套拥有该权限，
            //    否则访问即被过滤器拦截（401/403）。超管角色（admin）在过滤器中天然放行，此处补齐普通审批角色。
            //    覆盖范围：definition/template 的 add/edit/delete、task 的 approve/reject/transfer/addsign、
            //    instance 的 start/withdraw、record 的 cc、comment 的 list/add 等全部按钮权限。
            var allButtonPerms = pages.SelectMany(p => p.Buttons.Select(b => b.Item2)).Distinct().ToList();
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
