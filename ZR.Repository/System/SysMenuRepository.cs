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
        public List<SysMenu> SelectTreeMenuList(MenuQueryDto menu)
        {
            return Context.Queryable<SysMenu>()
                .WhereIF(!string.IsNullOrEmpty(menu.MenuName), it => it.MenuName.Contains(menu.MenuName))
                .WhereIF(!string.IsNullOrEmpty(menu.Visible), it => it.visible == menu.Visible)
                .WhereIF(!string.IsNullOrEmpty(menu.Status), it => it.status == menu.Status)
                .WhereIF(!string.IsNullOrEmpty(menu.MenuTypeIds), it => menu.MenuTypeIdArr.Contains(it.menuType))
                .OrderBy(it => new { it.parentId, it.orderNum })
                .ToTree(it => it.children, it => it.parentId, 0);
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

            return Context.Queryable<SysMenu>()
                .Where(c => roleMenus.Contains(c.MenuId))
                .WhereIF(!string.IsNullOrEmpty(menu.MenuName), (c) => c.MenuName.Contains(menu.MenuName))
                .WhereIF(!string.IsNullOrEmpty(menu.Visible), (c) => c.visible == menu.Visible)
                .WhereIF(!string.IsNullOrEmpty(menu.Status), (c) => c.status == menu.Status)
                .WhereIF(!string.IsNullOrEmpty(menu.MenuTypeIds), c => menu.MenuTypeIdArr.Contains(c.menuType))
                .OrderBy((c) => new { c.parentId, c.orderNum })
                .Select(c => c)
                .ToTree(it => it.children, it => it.parentId, 0);
        }

        /// <summary>
        /// 获取所有菜单
        /// </summary>
        /// <returns></returns>
        public List<SysMenu> SelectMenuList(MenuQueryDto menu)
        {
            return Context.Queryable<SysMenu>()
                .WhereIF(!string.IsNullOrEmpty(menu.MenuName), it => it.MenuName.Contains(menu.MenuName))
                .WhereIF(!string.IsNullOrEmpty(menu.Visible), it => it.visible == menu.Visible)
                .WhereIF(!string.IsNullOrEmpty(menu.Status), it => it.status == menu.Status)
                .WhereIF(menu.ParentId != null, it => it.parentId == menu.ParentId)
                .OrderBy(it => new { it.parentId, it.orderNum })
                .ToList();
        }

        /// <summary>
        /// 根据用户查询系统菜单列表
        /// </summary>
        /// <param name="sysMenu"></param>
        /// <param name="roles">用户角色集合</param>
        /// <returns></returns>
        public List<SysMenu> SelectMenuListByRoles(MenuQueryDto sysMenu, List<long> roles)
        {
            var roleMenus = Context.Queryable<SysRoleMenu>()
                .Where(r => roles.Contains(r.Role_id));

            return Context.Queryable<SysMenu>()
                .InnerJoin(roleMenus, (c, j) => c.MenuId == j.Menu_id)
                .Where((c, j) => c.status == "0")
                .WhereIF(!string.IsNullOrEmpty(sysMenu.MenuName), (c, j) => c.MenuName.Contains(sysMenu.MenuName))
                .WhereIF(!string.IsNullOrEmpty(sysMenu.Visible), (c, j) => c.visible == sysMenu.Visible)
                .OrderBy((c, j) => new { c.parentId, c.orderNum })
                .Select(c => c)
                .ToList();
        }

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
