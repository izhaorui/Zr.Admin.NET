using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Model.System.Dto;
using ZR.Model.System;
using ZR.Service.System.IService;
using ZR.Model;

namespace ZR.Admin.WebApi.Controllers.System
{
    [Verify]
    [Route("/system/menu")]
    public class SysMenuController : BaseController
    {
        private readonly ISysRoleService sysRoleService;
        private readonly ISysMenuService sysMenuService;

        public SysMenuController(
            ISysRoleService sysRoleService,
            ISysMenuService sysMenuService)
        {
            this.sysRoleService = sysRoleService;
            this.sysMenuService = sysMenuService;
        }

        /// <summary>
        /// 获取菜单列表 √
        /// </summary>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:menu:list")]
        [HttpGet("list")]
        public IActionResult TreeMenuList([FromQuery] SysMenu menu)
        {
            long userId = HttpContext.GetUId();
            return SUCCESS(sysMenuService.SelectTreeMenuList(menu, userId), "yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// 根据菜单编号获取详细信息 √
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        [HttpGet("{menuId}")]
        [ActionPermissionFilter(Permission = "system:menu:query")]
        public IActionResult GetMenuInfo(int menuId = 0)
        {
            return SUCCESS(sysMenuService.GetMenuByMenuId(menuId), "yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// 获取菜单下拉树列表(分配角色所需菜单)
        /// </summary>
        /// <returns></returns>
        [HttpGet("treeSelect")]
        public IActionResult TreeSelect()
        {
            long userId = HttpContext.GetUId();
            var list = sysMenuService.SelectMenuList(new SysMenu(), userId).FindAll(f => f.visible == "0");
            var treeMenus = sysMenuService.BuildMenuTreeSelect(list);

            return SUCCESS(treeMenus);
        }

        /// <summary>
        /// 获取角色菜单信息
        /// 加载对应角色菜单列表树
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>        
        [HttpGet("roleMenuTreeselect/{roleId}")]
        public IActionResult RoleMenuTreeselect(int roleId)
        {
            long userId = HttpContext.GetUId();
            var menus = sysMenuService.SelectMenuList(new SysMenu(), userId);
            var checkedKeys = sysRoleService.SelectUserRoleMenus(roleId);
            return SUCCESS(new
            {
                checkedKeys,
                menus = sysMenuService.BuildMenuTreeSelect(menus),
            });
        }

        /// <summary>
        /// 修改菜单 √
        /// </summary>
        /// <param name="MenuDto"></param>
        /// <returns></returns>
        [HttpPost("edit")]
        [Log(Title = "菜单管理", BusinessType = BusinessType.UPDATE)]
        [ActionPermissionFilter(Permission = "system:menu:edit")]
        public IActionResult MenuEdit([FromBody] SysMenu MenuDto)
        {
            if (MenuDto == null) { return ToResponse(ApiResult.Error(101, "请求参数错误")); }
            //if (UserConstants.NOT_UNIQUE.Equals(sysMenuService.CheckMenuNameUnique(MenuDto)))
            //{
            //    return ToResponse(ApiResult.Error($"修改菜单'{MenuDto.menuName}'失败，菜单名称已存在"));
            //}
            if (UserConstants.YES_FRAME.Equals(MenuDto.isFrame) && !MenuDto.path.StartsWith("http"))
            {
                return ToResponse(ApiResult.Error($"修改菜单'{MenuDto.menuName}'失败，地址必须以http(s)://开头"));
            }
            if (MenuDto.menuId.Equals(MenuDto.parentId))
            {
                return ToResponse(ApiResult.Error($"修改菜单'{MenuDto.menuName}'失败，上级菜单不能选择自己"));
            }
            MenuDto.Update_by = User.Identity.Name;
            int result = sysMenuService.EditMenu(MenuDto);

            return ToResponse(result);
        }

        /// <summary>
        /// 添加菜单 √
        /// </summary>
        /// <param name="MenuDto"></param>
        /// <returns></returns>
        [HttpPut("add")]
        [Log(Title = "菜单管理", BusinessType = BusinessType.INSERT)]
        [ActionPermissionFilter(Permission = "system:menu:add")]
        public IActionResult MenuAdd([FromBody] SysMenu MenuDto)
        {
            if (MenuDto == null) { return ToResponse(ApiResult.Error(101, "请求参数错误")); }
            if (UserConstants.NOT_UNIQUE.Equals(sysMenuService.CheckMenuNameUnique(MenuDto)))
            {
                return ToResponse(ApiResult.Error($"新增菜单'{MenuDto.menuName}'失败，菜单名称已存在"));
            }
            if (UserConstants.YES_FRAME.Equals(MenuDto.isFrame) && !MenuDto.path.StartsWith("http"))
            {
                return ToResponse(ApiResult.Error($"新增菜单'{MenuDto.menuName}'失败，地址必须以http(s)://开头"));
            }

            MenuDto.Create_by = User.Identity.Name;
            int result = sysMenuService.AddMenu(MenuDto);

            return ToResponse(result);
        }

        /// <summary>
        /// 菜单删除 √
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        [HttpDelete("{menuId}")]
        [Log(Title = "菜单管理", BusinessType = BusinessType.DELETE)]
        [ActionPermissionFilter(Permission = "system:menu:remove")]
        public IActionResult Remove(int menuId = 0)
        {
            if (sysMenuService.HasChildByMenuId(menuId))
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "存在子菜单,不允许删除");
            }
            if (sysMenuService.CheckMenuExistRole(menuId))
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "菜单已分配,不允许删除");
            }
            int result = sysMenuService.DeleteMenuById(menuId);

            return ToResponse(result);
        }

        /// <summary>
        /// 保存排序
        /// </summary>
        /// <param name="MenuDto"></param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:menu:update")]
        [HttpPost("ChangeSort")]
        [Log(Title = "保存排序", BusinessType = BusinessType.UPDATE)]
        public IActionResult ChangeSort([FromBody] MenuDto MenuDto)
        {
            if (MenuDto == null) { return ToResponse(ApiResult.Error(101, "请求参数错误")); }

            int result = sysMenuService.ChangeSortMenu(MenuDto);
            return ToResponse(result);
        }
    }
}
