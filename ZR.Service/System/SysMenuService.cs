using Infrastructure.Attribute;
using System.Collections.Generic;
using System.Linq;
using ZR.Model.System.Dto;
using ZR.Model.System;
using ZR.Model.System.Vo;
using ZR.Model.Vo.System;
using ZR.Repository.System;
using ZR.Service.System.IService;
using ZR.Common;
using Infrastructure.Extensions;

namespace ZR.Service
{
    /// <summary>
    /// 菜单
    /// </summary>
    [AppService(ServiceType = typeof(ISysMenuService), ServiceLifetime = LifeTime.Transient)]
    public class SysMenuService : BaseService<SysMenu>, ISysMenuService
    {
        public SysMenuRepository MenuRepository;
        public ISysRoleService SysRoleService;

        public SysMenuService(
            SysMenuRepository menuRepository,
            ISysRoleService sysRoleService)
        {
            MenuRepository = menuRepository;
            SysRoleService = sysRoleService;
        }

        /// <summary>
        /// 获取所有菜单数（菜单管理）
        /// </summary>
        /// <returns></returns>
        public List<SysMenu> SelectTreeMenuList(MenuQueryDto menu, long userId)
        {
            List<SysMenu> menuList;
            if (SysRoleService.IsAdmin(userId))
            {
                menuList = MenuRepository.SelectTreeMenuList(menu);
            }
            else
            {
                var userRoles = SysRoleService.SelectUserRoles(userId);
                menuList = MenuRepository.SelectTreeMenuListByRoles(menu, userRoles);
            }
            return menuList;
        }

        /// <summary>
        /// 获取所有菜单列表
        /// </summary>
        /// <returns></returns>
        public List<SysMenu> SelectMenuList(MenuQueryDto menu, long userId)
        {
            List<SysMenu> menuList;
            if (SysRoleService.IsAdmin(userId))
            {
                menuList = MenuRepository.SelectMenuList(menu);
            }
            else
            {
                var userRoles = SysRoleService.SelectUserRoles(userId);
                menuList = MenuRepository.SelectMenuListByRoles(menu, userRoles);
            }
            return menuList;
        }

        /// <summary>
        /// 获取菜单详情
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public SysMenu GetMenuByMenuId(int menuId)
        {
            return MenuRepository.SelectMenuById(menuId);
        }

        /// <summary>
        /// 根据菜单id获取菜单列表
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public List<SysMenu> GetMenusByMenuId(int menuId)
        {
            var list = MenuRepository.GetList(f => f.parentId == menuId).OrderBy(f => f.orderNum).ToList();
            Context.ThenMapper(list, item =>
            {
                item.SubNum = Context.Queryable<SysMenu>().SetContext(x => x.parentId, () => item.MenuId, item).Count;
            });
            return list;
        }

        /// <summary>
        /// 添加菜单
        /// </summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        public int AddMenu(SysMenu menu)
        {
            return MenuRepository.AddMenu(menu);
        }

        /// <summary>
        /// 编辑菜单
        /// </summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        public int EditMenu(SysMenu menu)
        {
            menu.icon = string.IsNullOrEmpty(menu.icon) ? "" : menu.icon;
            return MenuRepository.EditMenu(menu);
        }

        /// <summary>
        /// 删除菜单管理信息
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public int DeleteMenuById(int menuId)
        {
            return MenuRepository.DeleteMenuById(menuId);
        }

        /// <summary>
        /// 校验菜单名称是否唯一
        /// </summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        public string CheckMenuNameUnique(SysMenu menu)
        {
            long menuId = menu.MenuId == 0 ? -1 : menu.MenuId;
            SysMenu info = MenuRepository.CheckMenuNameUnique(menu);

            //if (info != null && menuId != info.menuId && menu.menuName.Equals(info.menuName))
            //{
            //    return UserConstants.NOT_UNIQUE;
            //}
            if (info != null && info.MenuId != menu.MenuId)
            {
                return UserConstants.NOT_UNIQUE;
            }
            return UserConstants.UNIQUE;
        }

        /// <summary>
        /// 菜单排序
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public int ChangeSortMenu(MenuDto menuDto)
        {
            return MenuRepository.ChangeSortMenu(menuDto);
        }

        /// <summary>
        /// 是否存在菜单子节点
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public bool HasChildByMenuId(long menuId)
        {
            return MenuRepository.HasChildByMenuId(menuId) > 0;
        }

        /// <summary>
        /// 获取左边导航栏菜单树
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<SysMenu> SelectMenuTreeByUserId(long userId)
        {
            MenuQueryDto dto = new() { Status = "0", MenuTypeIds = "M,C" };
            if (SysRoleService.IsAdmin(userId))
            {
                return MenuRepository.SelectTreeMenuList(dto);
            }
            else
            {
                List<long> roleIds = SysRoleService.SelectUserRoles(userId);
                return MenuRepository.SelectTreeMenuListByRoles(dto, roleIds);
            }
        }

        /// <summary>
        /// 查询菜单权限
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<SysMenu> SelectMenuPermsListByUserId(long userId)
        {
            return MenuRepository.SelectMenuPermsByUserId(userId);
        }

        /// <summary>
        /// 查询精确到按钮的操作权限
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<string> SelectMenuPermsByUserId(long userId)
        {
            var menuList = SelectMenuPermsListByUserId(userId).Where(f => !string.IsNullOrEmpty(f.perms));

            return menuList.Select(x => x.perms).Distinct().ToList();
        }

        #region RoleMenu

        /// <summary>
        /// 查询菜单使用数量
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public bool CheckMenuExistRole(long menuId)
        {
            return MenuRepository.CheckMenuExistRole(menuId) > 0;
        }

        #endregion

        #region 方法

        ///// <summary>
        ///// 根据父节点的ID获取所有子节点
        ///// </summary>
        ///// <param name="list">分类表</param>
        ///// <param name="parentId">传入的父节点ID</param>
        ///// <returns></returns>
        //public List<SysMenu> GetChildPerms(List<SysMenu> list, int parentId)
        //{
        //    List<SysMenu> returnList = new List<SysMenu>();
        //    var data = list.FindAll(f => f.parentId == parentId);

        //    foreach (var item in data)
        //    {
        //        RecursionFn(list, item);

        //        returnList.Add(item);
        //    }
        //    return returnList;
        //}

        /// <summary>
        /// 递归列表
        /// </summary>
        /// <param name="list"></param>
        /// <param name="t"></param>
        private void RecursionFn(List<SysMenu> list, SysMenu t)
        {
            //得到子节点列表
            List<SysMenu> childList = GetChildList(list, t);
            t.children = childList;
            foreach (var item in childList)
            {
                if (GetChildList(list, item).Count() > 0)
                {
                    RecursionFn(list, item);
                }
            }
        }

        /// <summary>
        /// 递归获取子菜单
        /// </summary>
        /// <param name="list">所有菜单</param>
        /// <param name="sysMenu"></param>
        /// <returns></returns>
        private List<SysMenu> GetChildList(List<SysMenu> list, SysMenu sysMenu)
        {
            return list.Where(p => p.parentId == sysMenu.MenuId).ToList();
        }

        /// <summary>
        /// 获取路由侧边栏,动态生成
        /// </summary>
        /// <param name="menus"></param>
        /// <returns></returns>
        public List<RouterVo> BuildMenus(List<SysMenu> menus)
        {
            List<RouterVo> routers = new List<RouterVo>();

            foreach (var menu in menus)
            {
                RouterVo router = new()
                {
                    Hidden = "1".Equals(menu.visible),
                    Name = GetRouteName(menu),
                    Path = GetRoutePath(menu),
                    Component = GetComponent(menu),
                    Meta = new Meta(menu.MenuName, menu.icon, "1".Equals(menu.isCache), menu.MenuNameKey, menu.path)
                };

                List<SysMenu> cMenus = menu.children;
                //是目录并且有子菜单
                if (cMenus != null && cMenus.Count > 0 && (UserConstants.TYPE_DIR.Equals(menu.menuType)))
                {
                    router.AlwaysShow = true;
                    router.Redirect = "noRedirect";
                    router.Children = BuildMenus(cMenus);
                }
                else if (IsMeunFrame(menu))
                {
                    router.Meta = null;
                    List<RouterVo> childrenList = new();
                    RouterVo children = new()
                    {
                        Path = menu.path,
                        Component = menu.component,
                        Name = string.IsNullOrEmpty(menu.path) ? "" : menu.path.ToLower(),
                        Meta = new Meta(menu.MenuName, menu.icon, "1".Equals(menu.isCache), menu.MenuNameKey, menu.path)
                    };
                    childrenList.Add(children);
                    router.Children = childrenList;
                }
                else if (menu.parentId == 0 && IsInnerLink(menu))
                {
                    router.Meta = new Meta(menu.MenuName, menu.icon);
                    router.Path = "/";
                    List<RouterVo> childrenList = new();
                    RouterVo children = new();
                    string routerPath = InnerLinkReplaceEach(menu.path);
                    children.Path = routerPath;
                    children.Component = UserConstants.INNER_LINK;
                    children.Name = routerPath.ToLower();
                    children.Meta = new Meta(menu.MenuName, menu.icon, menu.path);
                    childrenList.Add(children);
                    router.Children = childrenList;
                }
                routers.Add(router);
            }

            return routers;
        }

        /// <summary>
        /// 构建前端所需要下拉树结构
        /// </summary>
        /// <param name="menus">菜单列表</param>
        /// <returns>下拉树结构列表</returns>
        public List<SysMenu> BuildMenuTree(List<SysMenu> menus)
        {
            List<SysMenu> returnList = new List<SysMenu>();
            List<long> tempList = menus.Select(f => f.MenuId).ToList();

            foreach (var menu in menus)
            {
                // 如果是顶级节点, 遍历该父节点的所有子节点
                if (!tempList.Contains(menu.parentId))
                {
                    RecursionFn(menus, menu);
                    returnList.Add(menu);
                }
            }
            if (!returnList.Any())
            {
                returnList = menus;
            }
            return returnList;
        }

        /// <summary>
        /// 构建前端所需要下拉树结构
        /// </summary>
        /// <param name="menus"></param>
        /// <returns></returns>
        public List<TreeSelectVo> BuildMenuTreeSelect(List<SysMenu> menus)
        {
            List<SysMenu> menuTrees = BuildMenuTree(menus);
            List<TreeSelectVo> treeMenuVos = new List<TreeSelectVo>();
            foreach (var item in menuTrees)
            {
                treeMenuVos.Add(new TreeSelectVo(item));
            }
            return treeMenuVos;
        }

        /// <summary>
        /// 获取路由名称
        /// </summary>
        /// <param name="menu">菜单信息</param>
        /// <returns>路由名称</returns>
        public string GetRouteName(SysMenu menu)
        {
            string routerName = menu.path.ToLower();
            // 非外链并且是一级目录（类型为目录）
            if (IsMeunFrame(menu))
            {
                routerName = string.Empty;
            }
            return routerName;
        }

        /// <summary>
        /// 获取路由路径
        /// </summary>
        /// <param name="menu">菜单信息</param>
        /// <returns>路由地址</returns>
        public string GetRoutePath(SysMenu menu)
        {
            string routerPath = menu.path;
            // 内链打开外网方式
            if (menu.parentId != 0 && IsInnerLink(menu))
            {
                routerPath = InnerLinkReplaceEach(routerPath);
            }
            // 非外链并且是一级目录（类型为目录）
            if (0 == menu.parentId && UserConstants.TYPE_DIR.Equals(menu.menuType)
                && UserConstants.NO_FRAME.Equals(menu.isFrame))
            {
                routerPath = "/" + menu.path;
            }
            else if (IsMeunFrame(menu))// 非外链并且是一级目录（类型为菜单）
            {
                routerPath = "/";
            }
            return routerPath;
        }

        /// <summary>
        /// 获取组件名称
        /// </summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        public string GetComponent(SysMenu menu)
        {
            string component = UserConstants.LAYOUT;
            if (!string.IsNullOrEmpty(menu.component) && !IsMeunFrame(menu))
            {
                component = menu.component;
            }
            else if (menu.component.IsEmpty() && menu.parentId != 0 && IsInnerLink(menu))
            {
                component = UserConstants.INNER_LINK;
            }
            else if (string.IsNullOrEmpty(menu.component) && IsParentView(menu))
            {
                component = UserConstants.PARENT_VIEW;
            }
            return component;
        }

        /// <summary>
        /// 是否为菜单内部跳转
        /// </summary>
        /// <param name="menu">菜单信息</param>
        /// <returns></returns>
        public bool IsMeunFrame(SysMenu menu)
        {
            return menu.parentId == 0 && UserConstants.TYPE_MENU.Equals(menu.menuType)
                && menu.isFrame.Equals(UserConstants.NO_FRAME);
        }

        /// <summary>
        /// 是否为内链组件
        /// </summary>
        /// <param name="menu">菜单信息</param>
        /// <returns>结果</returns>
        public bool IsInnerLink(SysMenu menu)
        {
            return menu.isFrame.Equals(UserConstants.NO_FRAME) && Tools.IsUrl(menu.path);
        }

        ///
        /// <summary>
        /// 是否为parent_view组件
        /// </summary>
        /// <param name="menu">菜单信息</param>
        /// <returns></returns>
        public bool IsParentView(SysMenu menu)
        {
            return menu.parentId != 0 && UserConstants.TYPE_DIR.Equals(menu.menuType);
        }

        /// <summary>
        /// 内链域名特殊字符替换
        /// </summary>
        /// <param name = "path" ></ param >
        /// < returns ></ returns >
        public string InnerLinkReplaceEach(string path)
        {
            return path.IsNotEmpty() ? path.Replace(UserConstants.HTTP, "").Replace(UserConstants.HTTPS, "") : path;
        }
        #endregion

    }
}
