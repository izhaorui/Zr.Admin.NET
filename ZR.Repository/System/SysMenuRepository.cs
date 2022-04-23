using Infrastructure.Attribute;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using ZR.Model.System.Dto;
using ZR.Model.System;

namespace ZR.Repository.System
{
    /// <summary>
    /// 系统菜单
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysMenuRepository : BaseRepository<SysMenu>
    {
        /// <summary>
        /// 获取所有菜单（菜单管理）
        /// </summary>
        /// <returns></returns>
        public List<SysMenu> SelectTreeMenuList(SysMenu menu)
        {
            return Context.Queryable<SysMenu>()
                .WhereIF(!string.IsNullOrEmpty(menu.MenuName), it => it.MenuName.Contains(menu.MenuName))
                .WhereIF(!string.IsNullOrEmpty(menu.visible), it => it.visible == menu.visible)
                .WhereIF(!string.IsNullOrEmpty(menu.status), it => it.status == menu.status)
                .OrderBy(it => new { it.parentId, it.orderNum })
                .ToTree(it => it.children, it => it.parentId, 0);
        }

        /// <summary>
        /// 根据用户查询系统菜单列表（菜单管理）
        /// </summary>
        /// <param name="sysMenu"></param>
        /// <param name="roles">用户角色集合</param>
        /// <returns></returns>
        public List<SysMenu> SelectTreeMenuListByUserId(SysMenu sysMenu, List<long> roles)
        {
            var roleMenus = Context.Queryable<SysRoleMenu>()
                .Where(r => roles.Contains(r.Role_id));

            return Context.Queryable<SysMenu>()
                .InnerJoin(roleMenus, (c, j) => c.MenuId == j.Menu_id)
                .WhereIF(!string.IsNullOrEmpty(sysMenu.MenuName), (c, j) => c.MenuName.Contains(sysMenu.MenuName))
                .WhereIF(!string.IsNullOrEmpty(sysMenu.visible), (c, j) => c.visible == sysMenu.visible)
                .WhereIF(!string.IsNullOrEmpty(sysMenu.status), (c, j) => c.status == sysMenu.status)
                .OrderBy((c, j) => new { c.parentId, c.orderNum })
                .Select(c => c)
                .ToTree(it => it.children, it => it.parentId, 0);
        }

        /// <summary>
        /// 获取所有菜单
        /// </summary>
        /// <returns></returns>
        public List<SysMenu> SelectMenuList(SysMenu menu)
        {
            return Context.Queryable<SysMenu>()
                .WhereIF(!string.IsNullOrEmpty(menu.MenuName), it => it.MenuName.Contains(menu.MenuName))
                .WhereIF(!string.IsNullOrEmpty(menu.visible), it => it.visible == menu.visible)
                .WhereIF(!string.IsNullOrEmpty(menu.status), it => it.status == menu.status)
                .OrderBy(it => new { it.parentId, it.orderNum })
                .ToList();
        }

        /// <summary>
        /// 根据用户查询系统菜单列表
        /// </summary>
        /// <param name="sysMenu"></param>
        /// <param name="roles">用户角色集合</param>
        /// <returns></returns>
        public List<SysMenu> SelectMenuListByRoles(SysMenu sysMenu, List<long> roles)
        {
            var roleMenus = Context.Queryable<SysRoleMenu>()
                .Where(r => roles.Contains(r.Role_id));

            return Context.Queryable<SysMenu>()
                .InnerJoin(roleMenus, (c, j) => c.MenuId == j.Menu_id)
                .Where((c, j) => c.status == "0")
                .WhereIF(!string.IsNullOrEmpty(sysMenu.MenuName), (c, j) => c.MenuName.Contains(sysMenu.MenuName))
                .WhereIF(!string.IsNullOrEmpty(sysMenu.visible), (c, j) => c.visible == sysMenu.visible)
                .OrderBy((c, j) => new { c.parentId, c.orderNum })
                .Select(c => c)
                .ToList();
        }

        #region 左侧菜单树

        /// <summary>
        /// 管理员获取左侧菜单树
        /// </summary>
        /// <returns></returns>
        public List<SysMenu> SelectMenuTreeAll()
        {
            var menuTypes = new string[] { "M", "C" };

            return Context.Queryable<SysMenu>()
                .Where(f => f.status == "0" && menuTypes.Contains(f.menuType))
                .OrderBy(it => new { it.parentId, it.orderNum }).ToList();
        }

        /// <summary>
        /// 根据用户角色获取左侧菜单树
        /// </summary>
        /// <param name="roleIds"></param>
        /// <returns></returns>
        public List<SysMenu> SelectMenuTreeByRoleIds(List<long> roleIds)
        {
            var menuTypes = new string[] { "M", "C" };
            return Context.Queryable<SysMenu>()
                .Where((menu) =>
                menuTypes.Contains(menu.menuType)
                    && menu.status == "0"
                    && SqlFunc.Subqueryable<SysRoleMenu>().Where( s => roleIds.Contains(s.Role_id) && s.Menu_id == menu.MenuId).Any())
                .OrderBy((menu) => new { menu.parentId, menu.orderNum })
                .Select((menu) => menu).ToList();
        }

        #endregion

        /// <summary>
        /// 获取菜单详情
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public SysMenu SelectMenuById(int menuId)
        {
            return Context.Queryable<SysMenu>().Where(it => it.MenuId == menuId).Single();
        }

        /// <summary>
        /// 添加菜单
        /// </summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        public int AddMenu(SysMenu menu)
        {
            var Db = Context;
            menu.Create_time = Db.GetDate();
            menu.MenuId = Db.Insertable(menu).ExecuteReturnIdentity();
            return 1;
        }

        /// <summary>
        /// 编辑菜单
        /// </summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        public int EditMenu(SysMenu menu)
        {
            return Context.Updateable(menu).ExecuteCommand();
        }

        /// <summary>
        /// 删除菜单
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public int DeleteMenuById(int menuId)
        {
            return Context.Deleteable<SysMenu>().Where(it => it.MenuId == menuId).ExecuteCommand();
        }

        /// <summary>
        /// 菜单排序
        /// </summary>
        /// <param name="menuDto">菜单Dto</param>
        /// <returns></returns>
        public int ChangeSortMenu(MenuDto menuDto)
        {
            var result = Context.Updateable(new SysMenu() { MenuId = menuDto.MenuId, orderNum = menuDto.orderNum })
                .UpdateColumns(it => new { it.orderNum }).ExecuteCommand();
            return result;
        }

        /// <summary>
        /// 查询菜单权限
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<SysMenu> SelectMenuPermsByUserId(long userId)
        {
            return Context.Queryable<SysMenu, SysRoleMenu, SysUserRole, SysRole>((m, rm, ur, r) => new JoinQueryInfos(
                JoinType.Left, m.MenuId == rm.Menu_id,
                JoinType.Left, rm.Role_id == ur.RoleId,
                JoinType.Left, ur.RoleId == r.RoleId
                ))
                //.Distinct()
                .Where((m, rm, ur, r) => m.status == "0" && r.Status == "0" && ur.UserId == userId)
                .Select((m, rm, ur, r) => m).ToList();
        }

        /// <summary>
        /// 校验菜单名称是否唯一
        /// </summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        public SysMenu CheckMenuNameUnique(SysMenu menu)
        {
            return Context.Queryable<SysMenu>()
                .Where(it => it.MenuName == menu.MenuName && it.parentId == menu.parentId).Single();
        }

        /// <summary>
        /// 是否存在菜单子节点
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public int HasChildByMenuId(long menuId)
        {
            return Context.Queryable<SysMenu>().Where(it => it.parentId == menuId).Count();
        }

        #region RoleMenu

        /// <summary>
        /// 查询菜单使用数量
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public int CheckMenuExistRole(long menuId)
        {
            return Context.Queryable<SysRoleMenu>().Where(it => it.Menu_id == menuId).Count();
        }

        #endregion
    }
}
