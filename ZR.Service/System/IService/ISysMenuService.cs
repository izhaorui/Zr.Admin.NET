using System.Collections.Generic;
using ZR.Model.System.Dto;
using ZR.Model.System;
using ZR.Model.System.Vo;
using ZR.Model.Vo.System;

namespace ZR.Service.System.IService
{
    public interface ISysMenuService
    {
        //List<SysMenu> SelectMenuList(long userId);

        List<SysMenu> SelectMenuList(MenuQueryDto menu, long userId);
        List<SysMenu> SelectTreeMenuList(MenuQueryDto menu, long userId);

        SysMenu GetMenuByMenuId(int menuId);
        List<SysMenu> GetMenusByMenuId(int menuId);
        int AddMenu(SysMenu menu);

        int EditMenu(SysMenu menu);

        int DeleteMenuById(int menuId);

        string CheckMenuNameUnique(SysMenu menu);

        int ChangeSortMenu(MenuDto menuDto);

        bool HasChildByMenuId(long menuId);

        List<SysMenu> SelectMenuTreeByUserId(long userId);

        List<SysMenu> SelectMenuPermsListByUserId(long userId);

        List<string> SelectMenuPermsByUserId(long userId);

        bool CheckMenuExistRole(long menuId);

        List<RouterVo> BuildMenus(List<SysMenu> menus);

        List<TreeSelectVo> BuildMenuTreeSelect(List<SysMenu> menus);
    }
}
