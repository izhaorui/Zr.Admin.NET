using Infrastructure;
using Infrastructure.Attribute;
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
            if (menu.ParentId != null)
            {
                return GetMenusByMenuId(menu.ParentId.ParseToInt(), userId);
            }
            List<SysMenu> menuList = BuildMenuTree(SelectMenuList(menu, userId));
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
        public long AddMenu(SysMenu menu)
        {
            menu.Create_time = DateTime.Now;
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
                Context.Deleteable<SysRoleMenu>().Where(f => childMenu.Contains(f.Menu_id)).ExecuteCommand();
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
                .WithCache(60 * 10)
                .Where((m, rm, ur, r) => m.Status == "0" && r.Status == 0 && ur.UserId == userId)
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
        public List<SysMenu> SelectTreeMenuListByRoles(MenuQueryDto menu, List<long> roles)
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
        /// 根据用户查询系统菜单列表
        /// </summary>
        /// <param name="menu"></param>
        /// <param name="roleId">用户角色</param>
        /// <returns></returns>
        public List<RoleMenuExportDto> SelectRoleMenuListByRole(MenuQueryDto menu, int roleId)
        {
            var menuIds = Context.Queryable<SysRoleMenu>()
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
                .WithCache(60 * 10)
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
                    Hidden = "1".Equals(menu.Visible),
                    Name = GetRouteName(menu),
                    Path = GetRoutePath(menu),
                    Component = GetComponent(menu),
                    Meta = new Meta(menu.MenuName, menu.Icon, "1".Equals(menu.IsCache), menu.MenuNameKey, menu.Path, menu.Create_time)
                };

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
                    List<RouterVo> childrenList = new();
                    RouterVo children = new()
                    {
                        Path = menu.Path,
                        Component = menu.Component,
                        Name = string.IsNullOrEmpty(menu.Path) ? "" : menu.Path.ToLower(),
                        Meta = new Meta(menu.MenuName, menu.Icon, "1".Equals(menu.IsCache), menu.MenuNameKey, menu.Path, menu.Create_time)
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
        /// <returns></returns>
        public List<RouterVo> GetAppMenus(List<string> perms)
        {
            var router = new List<RouterVo>
            {
                new()
                {
                    Meta = new Meta("演示功能", ""){ IconColor = "#2389da"},
                    Children = new List<RouterVo>()
                {
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
                }
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
