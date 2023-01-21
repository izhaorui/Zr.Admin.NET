using Infrastructure.Attribute;
using Infrastructure.Extensions;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using ZR.Common;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Model.System.Vo;
using ZR.Service.System.IService;

namespace ZR.Service
{
    /// <summary>
    /// 菜单
    /// </summary>
    [AppService(ServiceType = typeof(ISysMenuService), ServiceLifetime = LifeTime.Transient)]
    public class SysMenuService : BaseService<SysMenu>, ISysMenuService
    {
        public ISysRoleService SysRoleService;

        public SysMenuService(ISysRoleService sysRoleService)
        {
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
                menuList = SelectTreeMenuList(menu);
            }
            else
            {
                var userRoles = SysRoleService.SelectUserRoles(userId);
                menuList = SelectTreeMenuListByRoles(menu, userRoles);
            }
            return menuList ?? new List<SysMenu>();
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
                menuList = SelectAllMenuList(menu);
            }
            else
            {
                var userRoles = SysRoleService.SelectUserRoles(userId);
                menuList = SelectMenuListByRoles(menu, userRoles);
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
            return GetFirst(it => it.MenuId == menuId);
        }

        /// <summary>
        /// 根据菜单id获取菜单列表
        /// </summary>
        /// <param name="menuId"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<SysMenu> GetMenusByMenuId(int menuId, long userId)
        {
            var menuExpression = Expressionable.Create<SysMenu>();
            menuExpression.And(c => c.ParentId == menuId);
            
            if (!SysRoleService.IsAdmin(userId))
            {
                var userRoles = SysRoleService.SelectUserRoles(userId);
                var roleMenus = Context.Queryable<SysRoleMenu>()
                    .Where(r => userRoles.Contains(r.Role_id)).Select(s => s.Menu_id).ToList();

                menuExpression.And(c => roleMenus.Contains(c.MenuId));
            }
            var list = GetList(menuExpression.ToExpression()).OrderBy(f => f.OrderNum).ToList();
            Context.ThenMapper(list, item =>
            {
                item.SubNum = Context.Queryable<SysMenu>().SetContext(x => x.ParentId, () => item.MenuId, item).Count;
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
            menu.Create_time = DateTime.Now;
            return InsertReturnIdentity(menu);
        }

        /// <summary>
        /// 编辑菜单
        /// </summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        public int EditMenu(SysMenu menu)
        {
            menu.Icon = string.IsNullOrEmpty(menu.Icon) ? "" : menu.Icon;
            return Update(menu, false);
        }

        /// <summary>
        /// 删除菜单管理信息
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public int DeleteMenuById(int menuId)
        {
            return Delete(menuId);
        }

        /// <summary>
        /// 校验菜单名称是否唯一
        /// </summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        public string CheckMenuNameUnique(SysMenu menu)
        {
            long menuId = menu.MenuId == 0 ? -1 : menu.MenuId;
            SysMenu info = GetFirst(it => it.MenuName == menu.MenuName && it.ParentId == menu.ParentId);

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
        /// <param name="menuDto"></param>
        /// <returns></returns>
        public int ChangeSortMenu(MenuDto menuDto)
        {
            return Update(new SysMenu() { MenuId = menuDto.MenuId, OrderNum = menuDto.OrderNum }, it => new { it.OrderNum });
        }

        /// <summary>
        /// 是否存在菜单子节点
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public bool HasChildByMenuId(long menuId)
        {
            return Count(it => it.ParentId == menuId) > 0;
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
                return SelectTreeMenuList(dto);
            }
            else
            {
                List<long> roleIds = SysRoleService.SelectUserRoles(userId);
                return SelectTreeMenuListByRoles(dto, roleIds);
            }
        }

        /// <summary>
        /// 查询精确到按钮的操作权限
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<string> SelectMenuPermsByUserId(long userId)
        {
            var menus = Context.Queryable<SysMenu, SysRoleMenu, SysUserRole, SysRole>((m, rm, ur, r) => new JoinQueryInfos(
                JoinType.Left, m.MenuId == rm.Menu_id,
                JoinType.Left, rm.Role_id == ur.RoleId,
                JoinType.Left, ur.RoleId == r.RoleId
                ))
                .Where((m, rm, ur, r) => m.Status == "0" && r.Status == "0" && ur.UserId == userId)
                .Select((m, rm, ur, r) => m).ToList();
            var menuList = menus.Where(f => !string.IsNullOrEmpty(f.Perms));

            return menuList.Select(x => x.Perms).Distinct().ToList();
        }

        /// <summary>
        /// 根据用户查询系统菜单列表
        /// </summary>
        /// <param name="menu"></param>
        /// <param name="roles">用户角色集合</param>
        /// <returns></returns>
        private List<SysMenu> SelectTreeMenuListByRoles(MenuQueryDto menu, List<long> roles)
        {
            var roleMenus = Context.Queryable<SysRoleMenu>()
                .Where(r => roles.Contains(r.Role_id))
                .Select(f => f.Menu_id).Distinct().ToList();

            return Queryable()
                .Where(c => roleMenus.Contains(c.MenuId))
                .WhereIF(!string.IsNullOrEmpty(menu.MenuName), (c) => c.MenuName.Contains(menu.MenuName))
                .WhereIF(!string.IsNullOrEmpty(menu.Visible), (c) => c.Visible == menu.Visible)
                .WhereIF(!string.IsNullOrEmpty(menu.Status), (c) => c.Status == menu.Status)
                .WhereIF(!string.IsNullOrEmpty(menu.MenuTypeIds), c => menu.MenuTypeIdArr.Contains(c.MenuType))
                .OrderBy((c) => new { c.ParentId, c.OrderNum })
                .Select(c => c)
                .ToTree(it => it.Children, it => it.ParentId, 0);
        }

        /// <summary>
        /// 获取所有菜单
        /// </summary>
        /// <returns></returns>
        private List<SysMenu> SelectAllMenuList(MenuQueryDto menu)
        {
            return Queryable()
                .WhereIF(!string.IsNullOrEmpty(menu.MenuName), it => it.MenuName.Contains(menu.MenuName))
                .WhereIF(!string.IsNullOrEmpty(menu.Visible), it => it.Visible == menu.Visible)
                .WhereIF(!string.IsNullOrEmpty(menu.Status), it => it.Status == menu.Status)
                .WhereIF(menu.ParentId != null, it => it.ParentId == menu.ParentId)
                .OrderBy(it => new { it.ParentId, it.OrderNum })
                .ToList();
        }

        /// <summary>
        /// 根据用户查询系统菜单列表
        /// </summary>
        /// <param name="sysMenu"></param>
        /// <param name="roles">用户角色集合</param>
        /// <returns></returns>
        private List<SysMenu> SelectMenuListByRoles(MenuQueryDto sysMenu, List<long> roles)
        {
            var roleMenus = Context.Queryable<SysRoleMenu>()
                .Where(r => roles.Contains(r.Role_id));

            return Queryable()
                .InnerJoin(roleMenus, (c, j) => c.MenuId == j.Menu_id)
                .Where((c, j) => c.Status == "0")
                .WhereIF(!string.IsNullOrEmpty(sysMenu.MenuName), (c, j) => c.MenuName.Contains(sysMenu.MenuName))
                .WhereIF(!string.IsNullOrEmpty(sysMenu.Visible), (c, j) => c.Visible == sysMenu.Visible)
                .OrderBy((c, j) => new { c.ParentId, c.OrderNum })
                .Select(c => c)
                .ToList();
        }

        /// <summary>
        /// 获取所有菜单（菜单管理）
        /// </summary>
        /// <returns></returns>
        public List<SysMenu> SelectTreeMenuList(MenuQueryDto menu)
        {
            int parentId = menu.ParentId != null ? (int)menu.ParentId : 0;

            var list = Queryable()
                .WhereIF(!string.IsNullOrEmpty(menu.MenuName), it => it.MenuName.Contains(menu.MenuName))
                .WhereIF(!string.IsNullOrEmpty(menu.Visible), it => it.Visible == menu.Visible)
                .WhereIF(!string.IsNullOrEmpty(menu.Status), it => it.Status == menu.Status)
                .WhereIF(!string.IsNullOrEmpty(menu.MenuTypeIds), it => menu.MenuTypeIdArr.Contains(it.MenuType))
                .WhereIF(menu.ParentId != null, it => it.ParentId == menu.ParentId)
                .OrderBy(it => new { it.ParentId, it.OrderNum })
                .ToTree(it => it.Children, it => it.ParentId, parentId);

            return list;
        }
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
            t.Children = childList;
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
            return list.Where(p => p.ParentId == sysMenu.MenuId).ToList();
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
                    Hidden = "1".Equals(menu.Visible),
                    Name = GetRouteName(menu),
                    Path = GetRoutePath(menu),
                    Component = GetComponent(menu),
                    Meta = new Meta(menu.MenuName, menu.Icon, "1".Equals(menu.IsCache), menu.MenuNameKey, menu.Path)
                };

                List<SysMenu> cMenus = menu.Children;
                //是目录并且有子菜单
                if (cMenus != null && cMenus.Count > 0 && (UserConstants.TYPE_DIR.Equals(menu.MenuType)))
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
                        Path = menu.Path,
                        Component = menu.Component,
                        Name = string.IsNullOrEmpty(menu.Path) ? "" : menu.Path.ToLower(),
                        Meta = new Meta(menu.MenuName, menu.Icon, "1".Equals(menu.IsCache), menu.MenuNameKey, menu.Path)
                    };
                    childrenList.Add(children);
                    router.Children = childrenList;
                }
                else if (menu.ParentId == 0 && IsInnerLink(menu))
                {
                    router.Meta = new Meta(menu.MenuName, menu.Icon);
                    router.Path = "/";
                    List<RouterVo> childrenList = new();
                    RouterVo children = new();
                    string routerPath = InnerLinkReplaceEach(menu.Path);
                    children.Path = routerPath;
                    children.Component = UserConstants.INNER_LINK;
                    children.Name = routerPath.ToLower();
                    children.Meta = new Meta(menu.MenuName, menu.Icon, menu.Path);
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
                if (!tempList.Contains(menu.ParentId))
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
            string routerName = menu.Path.ToLower();
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
            string routerPath = menu.Path;
            // 内链打开外网方式
            if (menu.ParentId != 0 && IsInnerLink(menu))
            {
                routerPath = InnerLinkReplaceEach(routerPath);
            }
            // 非外链并且是一级目录（类型为目录）
            if (0 == menu.ParentId && UserConstants.TYPE_DIR.Equals(menu.MenuType)
                && UserConstants.NO_FRAME.Equals(menu.IsFrame))
            {
                routerPath = "/" + menu.Path;
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
            if (!string.IsNullOrEmpty(menu.Component) && !IsMeunFrame(menu))
            {
                component = menu.Component;
            }
            else if (menu.Component.IsEmpty() && menu.ParentId != 0 && IsInnerLink(menu))
            {
                component = UserConstants.INNER_LINK;
            }
            else if (string.IsNullOrEmpty(menu.Component) && IsParentView(menu))
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
            return menu.ParentId == 0 && UserConstants.TYPE_MENU.Equals(menu.MenuType)
                && menu.IsFrame.Equals(UserConstants.NO_FRAME);
        }

        /// <summary>
        /// 是否为内链组件
        /// </summary>
        /// <param name="menu">菜单信息</param>
        /// <returns>结果</returns>
        public bool IsInnerLink(SysMenu menu)
        {
            return menu.IsFrame.Equals(UserConstants.NO_FRAME) && Tools.IsUrl(menu.Path);
        }

        ///
        /// <summary>
        /// 是否为parent_view组件
        /// </summary>
        /// <param name="menu">菜单信息</param>
        /// <returns></returns>
        public bool IsParentView(SysMenu menu)
        {
            return menu.ParentId != 0 && UserConstants.TYPE_DIR.Equals(menu.MenuType);
        }

        /// <summary>
        /// 内链域名特殊字符替换
        /// </summary>
        /// <param name = "path" ></ param >
        /// < returns ></ returns >
        public string InnerLinkReplaceEach(string path)
        {
            return path.IsNotEmpty() ? path
                .Replace(UserConstants.HTTP, "")
                .Replace(UserConstants.HTTPS, "")
                .Replace(UserConstants.WWW, "")
                .Replace(".", "/") : path;
        }
        #endregion

    }
}
