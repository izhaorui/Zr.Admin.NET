using System.Collections.Generic;
using ZR.Model.System.Dto;
using ZR.Model.System;
using ZR.Model.System.Vo;
using ZR.Model.Vo.System;

namespace ZR.Service.System.IService
{
    public interface ISysMenuService: IBaseService<SysMenu>
    {
        public List<SysMenu> SelectMenuList(long userId);

        public List<SysMenu> SelectMenuList(SysMenu menu, long userId);

        public SysMenu GetMenuByMenuId(int menuId);

        public int AddMenu(SysMenu menu);

        public int EditMenu(SysMenu menu);

        public int DeleteMenuById(int menuId);

        public string CheckMenuNameUnique(SysMenu menu);

        public int ChangeSortMenu(MenuDto menuDto);

        public bool HasChildByMenuId(long menuId);

        public List<SysMenu> SelectMenuTreeByUserId(long userId);

        public List<SysMenu> SelectMenuPermsListByUserId(long userId);

        public List<string> SelectMenuPermsByUserId(long userId);

        public bool CheckMenuExistRole(long menuId);

        public List<RouterVo> BuildMenus(List<SysMenu> menus);

        public List<TreeSelectVo> BuildMenuTreeSelect(List<SysMenu> menus);
    }
}
