using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 系统菜单
    /// </summary>
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
        /// 获取菜单列表
        /// </summary>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:menu:list")]
        [HttpGet("list")]
        public IActionResult TreeMenuList([FromQuery] MenuQueryDto menu)
        {
            long userId = HttpContext.GetUId();
            return SUCCESS(sysMenuService.SelectTreeMenuList(menu, userId), "yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// 根据菜单编号获取详细信息
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
        /// 根据菜单编号获取菜单列表，菜单管理首次进入
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        [HttpGet("list/{menuId}")]
        [ActionPermissionFilter(Permission = "system:menu:query")]
        public IActionResult GetMenuList(int menuId = 0)
        {
            return SUCCESS(sysMenuService.GetMenusByMenuId(menuId), "yyyy-MM-dd HH:mm:ss");
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
            var menus = sysMenuService.SelectMenuList(new MenuQueryDto(), userId);
            var checkedKeys = sysRoleService.SelectUserRoleMenus(roleId);
            return SUCCESS(new
            {
                checkedKeys,
                menus = sysMenuService.BuildMenuTreeSelect(menus),
            });
        }

        /// <summary>
        /// 修改菜单
        /// </summary>
        /// <param name="menuDto"></param>
        /// <returns></returns>
        [HttpPost("edit")]
        [Log(Title = "菜单管理", BusinessType = BusinessType.UPDATE)]
        [ActionPermissionFilter(Permission = "system:menu:edit")]
        public IActionResult MenuEdit([FromBody] MenuDto menuDto)
        {
            if (menuDto == null) { return ToResponse(ApiResult.Error(101, "请求参数错误")); }
            //if (UserConstants.NOT_UNIQUE.Equals(sysMenuService.CheckMenuNameUnique(MenuDto)))
            //{
            //    return ToResponse(ApiResult.Error($"修改菜单'{MenuDto.menuName}'失败，菜单名称已存在"));
            //}
            var config = new TypeAdapterConfig();
            //映射规则
            config.ForType<SysMenu, MenuDto>()
                .NameMatchingStrategy(NameMatchingStrategy.IgnoreCase);//忽略字段名称的大小写;//忽略除以上配置的所有字段

            var modal = menuDto.Adapt<SysMenu>(config).ToUpdate(HttpContext);
            if (UserConstants.YES_FRAME.Equals(modal.isFrame) && !modal.path.StartsWith("http"))
            {
                return ToResponse(ApiResult.Error($"修改菜单'{modal.MenuName}'失败，地址必须以http(s)://开头"));
            }
            if (modal.MenuId.Equals(modal.parentId))
            {
                return ToResponse(ApiResult.Error($"修改菜单'{modal.MenuName}'失败，上级菜单不能选择自己"));
            }
            modal.Update_by = HttpContext.GetName();
            int result = sysMenuService.EditMenu(modal);

            return ToResponse(result);
        }

        /// <summary>
        /// 添加菜单
        /// </summary>
        /// <param name="menuDto"></param>
        /// <returns></returns>
        [HttpPut("add")]
        [Log(Title = "菜单管理", BusinessType = BusinessType.INSERT)]
        [ActionPermissionFilter(Permission = "system:menu:add")]
        public IActionResult MenuAdd([FromBody] MenuDto menuDto)
        {
            var config = new TypeAdapterConfig();
            //映射规则
            config.ForType<SysMenu, MenuDto>()
                .NameMatchingStrategy(NameMatchingStrategy.IgnoreCase);
            var menu = menuDto.Adapt<SysMenu>(config).ToCreate(HttpContext);

            if (menu == null) { return ToResponse(ApiResult.Error(101, "请求参数错误")); }
            if (UserConstants.NOT_UNIQUE.Equals(sysMenuService.CheckMenuNameUnique(menu)))
            {
                return ToResponse(ApiResult.Error($"新增菜单'{menu.MenuName}'失败，菜单名称已存在"));
            }
            if (UserConstants.YES_FRAME.Equals(menu.isFrame) && !menu.path.StartsWith("http"))
            {
                return ToResponse(ApiResult.Error($"新增菜单'{menu.MenuName}'失败，地址必须以http(s)://开头"));
            }

            menu.Create_by = HttpContext.GetName();
            int result = sysMenuService.AddMenu(menu);

            return ToResponse(result);
        }

        /// <summary>
        /// 菜单删除
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
        /// <param name="id"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:menu:update")]
        [HttpGet("ChangeSort")]
        [Log(Title = "保存排序", BusinessType = BusinessType.UPDATE)]
        public IActionResult ChangeSort(int id = 0, int value = 0)
        {
            MenuDto MenuDto = new()
            {
                MenuId = id,
                orderNum = value
            };
            if (MenuDto == null) { return ToResponse(ApiResult.Error(101, "请求参数错误")); }

            int result = sysMenuService.ChangeSortMenu(MenuDto);
            return ToResponse(result);
        }
    }
}
