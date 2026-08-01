using Microsoft.AspNetCore.Mvc;
using ZR.Model.System.Dto;
using ZR.Model.System.Vo;
using ZR.ServiceCore.Services;

namespace ZR.Workflow.Controllers
{
    /// <summary>
    /// 工作流移动端菜单
    /// 独立于后台 /getRouters（Vue3 用），供 uni-app 工作台通过 /getAppRouters 之外的单独路由拉取，
    /// 返回结构与移动端 RouterVo 完全一致，作为工作流 App 工作台独立、唯一的菜单源（不与通用菜单合并）。
    /// </summary>
    [Route("workflow/menu")]
    [ApiExplorerSettings(GroupName = "workflow")]
    public class WfMenuController : BaseController
    {
        private readonly ISysPermissionService _permissionService;

        public WfMenuController(ISysPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        /// <summary>
        /// 获取工作流移动端工作台菜单
        /// </summary>
        [HttpGet]
        public IActionResult GetMenu()
        {
            long uid = HttpContext.GetUId();
            var perms = _permissionService.GetMenuPermission(new SysUserDto { UserId = uid });

            var children = new List<RouterVo>
            {
                new() { Path = "/pages/work/definition", Meta = new Meta("流程定义", "list") { Permi = "workflow:definition:list" } },
                new() { Path = "/pages/work/my", Meta = new Meta("我的流程", "file-text") { Permi = "workflow:instance:list" } },
                new() { Path = "/pages/work/todo", Meta = new Meta("待办任务", "checkmark-circle") { Permi = "workflow:task:list" } },
                new() { Path = "/pages/work/done", Meta = new Meta("已办任务", "checkmark") { Permi = "workflow:task:list" } },
                new() { Path = "/pages/work/record", Meta = new Meta("审批记录", "clock") { Permi = "workflow:record:list" } },
                new() { Path = "/pages/work/cc", Meta = new Meta("抄送给我", "chat") { Permi = "workflow:record:cc" } },
            };

            // 非管理员按权限过滤，无权限的菜单不展示
            if (!perms.Contains(GlobalConstant.AdminPerm))
            {
                children = children.Where(c => c.Meta.Permi == null || perms.Contains(c.Meta.Permi)).ToList();
            }

            var menu = new List<RouterVo>
            {
                new()
                {
                    Meta = new Meta("工作流", "") { IconColor = "#1890ff" },
                    Children = children
                }
            };

            return SUCCESS(menu);
        }
    }
}
