using Infrastructure;
using Infrastructure.Attribute;
using SqlSugar.IOC;
using ZR.Common;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Model.System.Enums;
using ZR.Model.System.Generate;
using ZR.Model.System.Vo;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 菜单
    /// </summary>
    [AppService(ServiceType = typeof(ISysMenuService), ServiceLifetime = LifeTime.Transient)]
    public class SysMenuService : BaseService<SysMenu>, ISysMenuService
    {
        public ISysRoleService SysRoleService;
        public ISysTenantPlanMenuService PlanMenuService;

        public SysMenuService(ISysRoleService sysRoleService, ISysTenantPlanMenuService planMenuService)
        {
            SysRoleService = sysRoleService;
            PlanMenuService = planMenuService;
        }

        #region 多租户辅助方法

        /// <summary>
        /// 获取租户库连接（多租户模式返回当前租户库，否则返回当前 Context）
        /// </summary>
        private ISqlSugarClient TenantDb()
        {
            if (!App.IsTenantEnabled()) return Context;
            var tenantId = App.GetCurrentTenantId();
            return DbScoped.SugarScope.GetConnectionScope(tenantId);
        }

        /// <summary>
        /// 获取主库连接（多租户模式返回主库，否则返回当前 Context）
        /// </summary>
        private ISqlSugarClient MainDb()
        {
            if (!App.IsTenantEnabled()) return Context;
            var mainDbConfigId = App.MainDbConfigId;
            return DbScoped.SugarScope.GetConnectionScope(mainDbConfigId);
        }

        #endregion

        /// <summary>
        /// 获取所有菜单数（菜单管理）
        /// </summary>
        /// <returns></returns>
        public List<SysMenu> SelectTreeMenuList(MenuQueryDto menu, long userId)
        {
            if (menu.ParentId != null)
            {
                var query = new MenuQueryDto
                {
                    MenuName = menu.MenuName,
                    Visible = menu.Visible,
                    Status = menu.Status,
                    MenuTypeIds = menu.MenuTypeIds
                };

                var menuList = SelectMenuList(query, userId);
                var parentId = menu.ParentId.Value;
                var children = menuList.Where(x => x.ParentId == parentId).OrderBy(x => x.OrderNum).ToList();
                foreach (var child in children)
                {
                    RecursionFn(menuList, child);
                }
                return children;
            }

            return BuildMenuTree(SelectMenuList(menu, userId));
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
                menuList = SelectMenuList(menu);
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
                var roleMenus = TenantDb().Queryable<SysRoleMenu>()
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
        public long AddMenu(SysMenu menu)
        {
            return InsertReturnBigIdentity(menu);
        }

        /// <summary>
        /// 编辑菜单
        /// </summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        public long EditMenu(SysMenu menu)
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
        /// 删除所有菜单
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public int DeleteAllMenuById(int menuId)
        {
            var childMenu = Queryable().ToChildList(x => x.ParentId, menuId).Select(x => x.MenuId);
            var result = UseTran(() =>
            {
                Delete(childMenu.ToArray(), "删除菜单");
                TenantDb().Deleteable<SysRoleMenu>().Where(f => childMenu.Contains(f.Menu_id)).ExecuteCommand();
            });
            return result.IsSuccess ? 1 : 0;
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
            if (menu.MenuType == "C")
            {
                SysMenu info2 = GetFirst(it => it.Path == menu.Path);
                if (info2 != null)
                {
                    throw new CustomException($"{menu.Path}路由地址已存在");
                }
            }

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
            MenuQueryDto dto = new() { Status = "0", MenuTypeIds = "M,C,L" };
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
        /// 多租户模式：通过套餐菜单配置获取租户可见菜单
        /// 用户真实权限 = 角色菜单 ∩ 套餐菜单
        /// </summary>
        public List<SysMenu> SelectMenuTreeByUserIdForTenant(long userId, string tenantId)
        {
            var mdb = MainDb();
            var planMenuIds = PlanMenuService.GetMenuIdsByTenantId(tenantId);
            if (planMenuIds.Count == 0) return new List<SysMenu>();

            List<long> allowedMenuIds;
            if (SysRoleService.IsAdmin(userId))
            {
                allowedMenuIds = planMenuIds;
            }
            else
            {
                var roleIds = SysRoleService.SelectUserRoles(userId);
                var roleMenuIds = TenantDb().Queryable<SysRoleMenu>()
                    .Where(r => roleIds.Contains(r.Role_id))
                    .Select(s => s.Menu_id)
                    .Distinct()
                    .ToList();
                allowedMenuIds = roleMenuIds.Intersect(planMenuIds).ToList();
            }

            if (allowedMenuIds.Count == 0) return new List<SysMenu>();

            var menus = mdb.Queryable<SysMenu>()
                .Where(m => allowedMenuIds.Contains(m.MenuId))
                .Where(m => m.Status == "0")
                .Where(m => new[] { "M", "C", "L" }.Contains(m.MenuType))
                .OrderBy(m => new { m.ParentId, m.OrderNum })
                .ToList();

            return BuildMenuTree(menus);
        }

        /// <summary>
        /// 多租户模式：角色分配菜单时获取当前操作者可分配的菜单树（含 F 按钮）
        /// 可分配菜单 = 当前用户角色菜单 ∩ 租户套餐菜单
        /// </summary>
        public List<SysMenu> SelectMenuTreeForRoleAssign(long userId, string tenantId)
        {
            var mdb = MainDb();
            var planMenuIds = PlanMenuService.GetMenuIdsByTenantId(tenantId);
            if (planMenuIds.Count == 0) return new List<SysMenu>();

            List<long> allowedMenuIds;
            if (SysRoleService.IsAdmin(userId))
            {
                allowedMenuIds = planMenuIds;
            }
            else
            {
                var roleIds = SysRoleService.SelectUserRoles(userId);
                var roleMenuIds = TenantDb().Queryable<SysRoleMenu>()
                    .Where(r => roleIds.Contains(r.Role_id))
                    .Select(s => s.Menu_id)
                    .Distinct()
                    .ToList();
                allowedMenuIds = roleMenuIds.Intersect(planMenuIds).ToList();
            }

            if (allowedMenuIds.Count == 0) return new List<SysMenu>();

            var menus = mdb.Queryable<SysMenu>()
                .Where(m => allowedMenuIds.Contains(m.MenuId))
                .Where(m => m.Status == "0")
                .OrderBy(m => new { m.ParentId, m.OrderNum })
                .ToList();

            return menus;
        }


        /// <summary>
        /// 查询精确到按钮的操作权限
        /// 多租户模式下按套餐菜单过滤（角色菜单 ∩ 套餐菜单）
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<string> SelectMenuPermsByUserId(long userId)
        {
            if (!App.IsTenantEnabled())
            {
                var menus = Context.Queryable<SysMenu, SysRoleMenu, SysUserRole, SysRole>((m, rm, ur, r) => new JoinQueryInfos(
                    JoinType.Left, m.MenuId == rm.Menu_id,
                    JoinType.Left, rm.Role_id == ur.RoleId,
                    JoinType.Left, ur.RoleId == r.RoleId
                    ))
                    .WithCache(60 * 10)
                    .Where((m, rm, ur, r) => m.Status == "0" && r.Status == 0 && ur.UserId == userId)
                    .Select((m, rm, ur, r) => m).ToList();
                var menuList = menus.Where(f => !string.IsNullOrEmpty(f.Perms));
                return menuList.Select(x => x.Perms).Distinct().ToList();
            }

            var tenantId = App.GetCurrentTenantId();
            return SelectMenuPermsByUserIdForTenant(userId, tenantId);
        }

        /// <summary>
        /// 多租户模式：按套餐菜单过滤后的权限字符串
        /// </summary>
        public List<string> SelectMenuPermsByUserIdForTenant(long userId, string tenantId)
        {
            var tdb = TenantDb();
            var mdb = MainDb();

            var planMenuIds = PlanMenuService.GetMenuIdsByTenantId(tenantId);
            if (planMenuIds.Count == 0) return new List<string>();

            List<long> menuIds;
            if (SysRoleService.IsAdmin(userId))
            {
                menuIds = planMenuIds;
            }
            else
            {
                menuIds = tdb.Queryable<SysRoleMenu, SysUserRole, SysRole>((rm, ur, r) => new JoinQueryInfos(
                    JoinType.Left, rm.Role_id == ur.RoleId,
                    JoinType.Left, ur.RoleId == r.RoleId
                ))
                .WithCache(60 * 10)
                .Where((rm, ur, r) => r.Status == 0 && ur.UserId == userId)
                .Select((rm, ur, r) => rm.Menu_id)
                .Distinct()
                .ToList();

                menuIds = menuIds.Intersect(planMenuIds).ToList();
            }

            if (menuIds.Count == 0) return new List<string>();

            return mdb.Queryable<SysMenu>()
                .WithCache(60 * 10)
                .Where(m => menuIds.Contains(m.MenuId) && m.Status == "0" && !string.IsNullOrEmpty(m.Perms))
                .Select(m => m.Perms)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// 根据用户查询系统菜单列表
        /// </summary>
        /// <param name="menu"></param>
        /// <param name="roles">用户角色集合</param>
        /// <returns></returns>
        public List<SysMenu> SelectTreeMenuListByRoles(MenuQueryDto menu, List<long> roles)
        {
            var roleMenus = TenantDb().Queryable<SysRoleMenu>()
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
        /// 根据用户查询系统菜单列表
        /// </summary>
        /// <param name="menu"></param>
        /// <param name="roleId">用户角色</param>
        /// <returns></returns>
        public List<RoleMenuExportDto> SelectRoleMenuListByRole(MenuQueryDto menu, int roleId)
        {
            var menuIds = TenantDb().Queryable<SysRoleMenu>()
                .Where(r => r.Role_id == roleId)
                .Select(f => f.Menu_id).Distinct().ToList();

            return Context.Queryable<SysMenu>()
                .InnerJoin<SysMenu>((t1, t2) => t1.MenuId == t2.ParentId)
                .InnerJoin<SysMenu>((t1, t2, t3) => t2.MenuId == t3.ParentId)
                .Where((t1, t2, t3) => menuIds.Contains(t1.MenuId))
                .Select((t1, t2, t3) => new RoleMenuExportDto()
                {
                    MenuName = $"{t1.MenuName}->{t2.MenuName}->{t3.MenuName}",
                    Path = t2.Path,
                    Component = t2.Component,
                    Perms = t3.Perms,
                    MenuType = (MenuType)(object)t3.MenuType,
                    Status = (MenuStatus)(object)t3.Status
                }).ToList();
        }

        /// <summary>
        /// 获取所有菜单
        /// </summary>
        /// <returns></returns>
        private List<SysMenu> SelectMenuList(MenuQueryDto menu)
        {
            var menuExp = Expressionable.Create<SysMenu>();
            menuExp.AndIF(!string.IsNullOrEmpty(menu.MenuName), it => it.MenuName.Contains(menu.MenuName));
            menuExp.AndIF(!string.IsNullOrEmpty(menu.Visible), it => it.Visible == menu.Visible);
            menuExp.AndIF(!string.IsNullOrEmpty(menu.Status), it => it.Status == menu.Status);
            menuExp.AndIF(!string.IsNullOrEmpty(menu.MenuTypeIds), it => menu.MenuTypeIdArr.Contains(it.MenuType));
            menuExp.AndIF(menu.ParentId != null, it => it.ParentId == menu.ParentId);

            return Queryable()
            .Where(menuExp.ToExpression())
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
            var roleMenuIds = TenantDb().Queryable<SysRoleMenu>()
                .Where(r => roles.Contains(r.Role_id))
                .Select(f => f.Menu_id).Distinct().ToList();

            return Queryable()
                .Where(c => roleMenuIds.Contains(c.MenuId) && c.Status == "0")
                .WhereIF(!string.IsNullOrEmpty(sysMenu.MenuName), c => c.MenuName.Contains(sysMenu.MenuName))
                .WhereIF(!string.IsNullOrEmpty(sysMenu.Visible), c => c.Visible == sysMenu.Visible)
                .WhereIF(!string.IsNullOrEmpty(sysMenu.Status), c => c.Status == sysMenu.Status)
                .WhereIF(!string.IsNullOrEmpty(sysMenu.MenuTypeIds), c => sysMenu.MenuTypeIdArr.Contains(c.MenuType))
                .WhereIF(sysMenu.ParentId != null, c => c.ParentId == sysMenu.ParentId)
                .OrderBy(c => new { c.ParentId, c.OrderNum })
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
                //.WithCache(60 * 10)
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
            List<RouterVo> routers = new();
            if (menus == null) return routers;

            foreach (var menu in menus)
            {
                RouterVo router = new()
                {
                    Query = menu.Query,
                    Hidden = "1".Equals(menu.Visible),
                    Name = GetRouteName(menu),
                    Path = GetRoutePath(menu),
                    Component = GetComponent(menu),
                    Meta = new Meta(menu.MenuName, menu.Icon, "1".Equals(menu.IsCache), menu.MenuNameKey, menu.Path, menu.Create_time, menu.TagByQuery)
                };
                router.Meta.Permi = menu.Perms;

                List<SysMenu> cMenus = menu.Children;
                //是目录并且有子菜单
                if (cMenus != null && cMenus.Count > 0 && UserConstants.TYPE_DIR.Equals(menu.MenuType))
                {
                    router.AlwaysShow = true;
                    router.Redirect = "noRedirect";
                    router.Children = BuildMenus(cMenus);
                }
                else if (IsMeunFrame(menu))
                {
                    router.Meta = null;
                    List<RouterVo> childrenList = [];
                    RouterVo children = new()
                    {
                        Query = menu.Query,
                        Path = menu.Path,
                        Component = menu.Component,
                        Name = menu.RouteName.IsNotEmpty() ? menu.RouteName : menu.Path.ToLower(),
                        Meta = new Meta(menu.MenuName, menu.Icon, "1".Equals(menu.IsCache), menu.MenuNameKey, menu.Path, menu.Create_time, menu.TagByQuery)
                    };
                    children.Meta.Permi = menu.Perms;
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
                    children.Query = menu.Query;
                    children.Path = routerPath;
                    children.Component = UserConstants.INNER_LINK;
                    children.Name = menu.RouteName.IsNotEmpty() ? menu.RouteName : routerPath.ToLower();
                    children.Meta = new Meta(menu.MenuName, menu.Icon, menu.Path);
                    children.Meta.Permi = menu.Perms;
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
            List<SysMenu> returnList = new();
            List<long> tempList = menus.Select(f => f.MenuId).ToList();

            foreach (var menu in menus)
            {
                // 如果是顶级节点, 遍历该父节点的所有子节点
                if (!tempList.Contains(menu.ParentId))
                {
                    var menuInfo = menus.Find(f => f.MenuId == menu.MenuId);
                    //移除按钮没有上级
                    if (!tempList.Contains(menuInfo.ParentId) && menu.MenuType != "F")
                    {
                        RecursionFn(menus, menu);
                        returnList.Add(menu);
                    }
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
            if (menu.RouteName.IsNotEmpty())
            {
                routerName = menu.RouteName;
            }
            // Non-empty external links and top-level directories (type: directory)
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
            // Internal links open external network methods
            if (menu.ParentId != 0 && IsInnerLink(menu))
            {
                routerPath = InnerLinkReplaceEach(routerPath);
            }
            // Non-empty external links and top-level directories (type: directory)
            if (0 == menu.ParentId && UserConstants.TYPE_DIR.Equals(menu.MenuType)
                && UserConstants.NO_FRAME.Equals(menu.IsFrame))
            {
                routerPath = "/" + menu.Path;
            }
            else if (IsMeunFrame(menu))// Non-empty external links and top-level directories (type: menu)
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
            return menu.ParentId == 0 && UserConstants.TYPE_MENUS.Contains(menu.MenuType)
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


        public void AddSysMenu(GenTable genTableInfo, string permPrefix, bool showEdit, bool showExport, bool showImport)
        {
            var menu = GetFirst(f => f.MenuName == genTableInfo.FunctionName);
            if (menu is null)
            {
                menu = new()
                {
                    MenuName = genTableInfo.FunctionName,
                    ParentId = genTableInfo.Options.ParentMenuId,
                    OrderNum = 0,
                    Path = genTableInfo.BusinessName,
                    Component = $"{genTableInfo.ModuleName.FirstLowerCase()}/{genTableInfo.BusinessName}",
                    Perms = $"{permPrefix}:list",
                    IsCache = "1",
                    MenuType = "C",
                    Visible = "0",
                    Status = "0",
                    Icon = "icon1",
                    Create_by = "system",
                };
                menu.MenuId = AddMenu(menu);
            }
            else
            {

                menu.MenuName = genTableInfo.FunctionName;
                menu.ParentId = genTableInfo.Options.ParentMenuId;
                menu.Path = genTableInfo.BusinessName;
                menu.Component = $"{genTableInfo.ModuleName.FirstLowerCase()}/{genTableInfo.BusinessName}";
                menu.Perms = $"{permPrefix}:list";
                menu.IsCache = "1";
                menu.Visible = "0";
                menu.Status = "0";
                menu.Update_by = "system";
                menu.Update_time = DateTime.Now;
                EditMenu(menu);
            }

            List<SysMenu> menuList = new();

            SysMenu menuQuery = new()
            {
                MenuName = "查询",
                ParentId = menu.MenuId,
                OrderNum = 1,
                Perms = $"{permPrefix}:query",
                MenuType = "F",
                Visible = "0",
                Status = "0",
                Icon = "",
            };
            SysMenu menuAdd = new()
            {
                MenuName = "新增",
                ParentId = menu.MenuId,
                OrderNum = 2,
                Perms = $"{permPrefix}:add",
                MenuType = "F",
                Visible = "0",
                Status = "0",
                Icon = "",
            };
            SysMenu menuDel = new()
            {
                MenuName = "删除",
                ParentId = menu.MenuId,
                OrderNum = 3,
                Perms = $"{permPrefix}:delete",
                MenuType = "F",
                Visible = "0",
                Status = "0",
                Icon = "",
            };

            SysMenu menuEdit = new()
            {
                MenuName = "修改",
                ParentId = menu.MenuId,
                OrderNum = 4,
                Perms = $"{permPrefix}:edit",
                MenuType = "F",
                Visible = "0",
                Status = "0",
                Icon = "",
            };

            SysMenu menuExport = new()
            {
                MenuName = "导出",
                ParentId = menu.MenuId,
                OrderNum = 5,
                Perms = $"{permPrefix}:export",
                MenuType = "F",
                Visible = "0",
                Status = "0",
                Icon = "",
            };

            SysMenu menuImport = new()
            {
                MenuName = "导入",
                ParentId = menu.MenuId,
                OrderNum = 5,
                Perms = $"{permPrefix}:import",
                MenuType = "F",
                Visible = "0",
                Status = "0",
                Icon = "",
            };

            menuList.Add(menuQuery);
            menuList.Add(menuAdd);
            menuList.Add(menuDel);
            if (showEdit)
            {
                menuList.Add(menuEdit);
            }
            if (showExport)
            {
                menuList.Add(menuExport);
            }
            if (showImport)
            {
                menuList.Add(menuImport);
            }
            //Insert(menuList);

            var x = Context.Storageable(menuList)
                //.SplitInsert(it => !it.Any())
                //.SplitUpdate(it => !it.Any())
                .WhereColumns(it => new { it.MenuName, it.ParentId })
                .ToStorage();
            x.AsInsertable.ExecuteCommand();//插入可插入部分;
            x.AsUpdateable.ExecuteCommand();
        }

        /// <summary>
        /// 获取移动端工作台菜单
        /// </summary>
        /// <param name="perms"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        public List<RouterVo> GetAppMenus(List<string> perms, int v)
        {
            var demoList = new List<RouterVo>(){
                new()
                {
                    Path = "/pages/demo/index",
                    Meta = new Meta("功能演示", "bookmark")
                },
                    new()
                {
                    Path = "/pages/demo/table/table",
                    Meta = new Meta("列表表格", "grid")
                },
                    new()
                {
                    Path = "/pages/demo/table/table2",
                    Meta = new Meta("水平表格", "list")
                }
            };
            if (v >= 1)
            {
                demoList.Add(new RouterVo
                {
                    Path = "/pages/dashboard/index",
                    Meta = new Meta("控制台", "list")
                });
            }
            var router = new List<RouterVo>
            {
                new()
                {
                    Meta = new Meta("演示功能", ""){ IconColor = "#2389da"},
                    Children = demoList
                },

                new()
                {
                    Meta = new Meta("系统管理", ""){ IconColor = "#ff7d00"},
                    Children = new List<RouterVo>()
                {
                    new()
                    {
                        Path = "/pages/system/sysConfig/sysConfig",
                        Meta = new Meta("系统配置", "setting"){ Permi = "system:config:list"},
                    },
                     new()
                    {
                        Path = "/pages/monitor/tasks/index",
                        Meta = new Meta("任务管理", "hourglass-half-fill"){Permi = "monitor:job:list"}
                    },
                     new()
                    {
                        Path = "/pages/monitor/onlineuser",
                        Meta = new Meta("在线用户", "grid"){ Permi = "admin"}
                    },
                      new()
                    {
                        Path = "/pages/system/user/user",
                        Meta = new Meta("用户管理", "account"){ Permi = "system:user:list" }
                    },
                       new()
                    {
                        Path = "/pages/monitor/server",
                        Meta = new Meta("系统监控", "play-right"){ Permi = "monitor:server:list"}
                    },
                       new()
                    {
                        Path = "/pages/notice/index",
                        Meta = new Meta("通知公告", "volume")
                    }
                }
                },

                new()
                {
                    Meta = new Meta("日志管理", ""){ IconColor = "#8a8a8a"},
                    Children = new List<RouterVo>()
                {
                    new()
                    {
                        Path = "/pages/monitor/logininfo",
                        Meta = new Meta("登录日志", "clock")
                    },
                     new()
                    {
                        Path = "/pages/monitor/operlog",
                        Meta = new Meta("操作日志", "file-text"){ Permi = "monitor:operlog:list" }
                    }
                }
                },
            };
            if (perms.Contains(GlobalConstant.AdminPerm))
            {
                return router;
            }
            var newRouter = new List<RouterVo>();
            foreach (var item in router)
            {
                RouterVo routerVo = new()
                {
                    Children = new List<RouterVo>()
                };
                for (var i = 0; i < item.Children.Count; i++)
                {
                    var chd = item.Children[i];
                    if (chd.Meta.Permi != null && perms.Contains(chd.Meta.Permi))
                    {
                        routerVo.Children.Add(chd);
                    }
                    else if (chd.Meta.Permi == null)
                    {
                        routerVo.Children.Add(chd);
                    }
                }

                if (routerVo.Children != null && routerVo.Children.Count > 0)
                {
                    routerVo.Meta = item.Meta;
                    newRouter.Add(routerVo);
                }
            }
            return newRouter;
        }
    }
}
