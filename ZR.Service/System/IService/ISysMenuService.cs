using System.Collections.Generic;
using ZR.Model.System.Dto;
using ZR.Model.System;
using ZR.Model.System.Vo;

namespace ZR.Service.System.IService
{
    public interface ISysMenuService : IBaseService<SysMenu>
    {
        //List<SysMenu> SelectMenuList(long userId);

        List<SysMenu> SelectMenuList(MenuQueryDto menu, long userId);
        List<SysMenu> SelectTreeMenuList(MenuQueryDto menu, long userId);

        SysMenu GetMenuByMenuId(int menuId);
        List<SysMenu> GetMenusByMenuId(int menuId, long userId);
        int AddMenu(SysMenu menu);

        int EditMenu(SysMenu menu);

        int DeleteMenuById(int menuId);

        string CheckMenuNameUnique(SysMenu menu);

        int ChangeSortMenu(MenuDto menuDto);

        bool HasChildByMenuId(long menuId);

        List<SysMenu> SelectMenuTreeByUserId(long userId);

        //List<SysMenu> SelectMenuPermsListByUserId(long userId);

        List<string> SelectMenuPermsByUserId(long userId);

        //bool CheckMenuExistRole(long menuId);

        List<RouterVo> BuildMenus(List<SysMenu> menus);

        List<TreeSelectVo> BuildMenuTreeSelect(List<SysMenu> menus);
    }

    /// <summary>
    /// 角色菜单
    /// </summary>
    public interface ISysRoleMenuService : IBaseService<SysRoleMenu>
    {
        bool CheckMenuExistRole(long menuId);
        /// <summary>
        /// 根据角色获取菜单id
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        List<SysRoleMenu> SelectRoleMenuByRoleId(long roleId);

        /// <summary>
        /// 根据用户所有角色获取菜单
        /// </summary>
        /// <param name="roleIds"></param>
        /// <returns></returns>
        List<SysRoleMenu> SelectRoleMenuByRoleIds(long[] roleIds);

        /// <summary>
        /// 批量插入用户菜单
        /// </summary>
        /// <param name="sysRoleMenus"></param>
        /// <returns></returns>
        int AddRoleMenu(List<SysRoleMenu> sysRoleMenus);

        /// <summary>
        /// 删除角色与菜单关联
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        int DeleteRoleMenuByRoleId(long roleId);
    }
}
