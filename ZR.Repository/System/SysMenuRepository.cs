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
    public class SysMenuRepository : BaseRepository
    {
        /// <summary>
        /// 获取所有菜单（菜单管理）
        /// </summary>
        /// <returns></returns>
        public List<SysMenu> SelectMenuList(SysMenu menu)
        {
            return Db.Queryable<SysMenu>()
                .WhereIF(!string.IsNullOrEmpty(menu.menuName), it => it.menuName.Contains(menu.menuName))
                .WhereIF(!string.IsNullOrEmpty(menu.visible), it => it.visible == menu.visible)
                .WhereIF(!string.IsNullOrEmpty(menu.status), it => it.status == menu.status)
                .OrderBy(it => new { it.parentId, it.orderNum })
                .ToList();
        }

        /// <summary>
        /// 根据用户查询系统菜单列表
        /// </summary>
        /// <param name="sysMenu"></param>
        /// <param name="userId">用户id</param>
        /// <returns></returns>
        public List<SysMenu> SelectMenuListByUserId(SysMenu sysMenu, long userId)
        {
            return Db.Queryable<SysMenu, SysRoleMenu, SysUserRole, SysRole>((menu, roleMenu, userRole, role) => new JoinQueryInfos(
                 JoinType.Left, menu.menuId == roleMenu.Menu_id,
                 JoinType.Left, roleMenu.Role_id == userRole.RoleId,
                 JoinType.Left, userRole.RoleId == role.RoleId
                 ))
                .Where((menu, roleMenu, userRole, role) => userRole.UserId == userId)
                .WhereIF(!string.IsNullOrEmpty(sysMenu.menuName), (menu, roleMenu, userRole, role) => menu.menuName.Contains(sysMenu.menuName))
                .WhereIF(!string.IsNullOrEmpty(sysMenu.visible), (menu, roleMenu, userRole, role) => menu.visible == sysMenu.visible)
                .WhereIF(!string.IsNullOrEmpty(sysMenu.status), (menu, roleMenu, userRole, role) => menu.status == sysMenu.status)
                .OrderBy((menu, roleMenu, userRole, role) => new { menu.parentId, menu.orderNum })
                //.Distinct()
                .Select((menu, roleMenu, userRole, role) => menu).ToList();
        }

        #region 左侧菜单树

        /// <summary>
        /// 管理员获取左侧菜单树
        /// </summary>
        /// <returns></returns>
        public List<SysMenu> SelectMenuTreeAll()
        {
            var menuTypes = new string[] { "M", "C" };

            return Db.Queryable<SysMenu>()
                .Where(f => f.status == "0" && menuTypes.Contains(f.menuType))
                .OrderBy(it => new { it.parentId, it.orderNum }).ToList();
        }

        /// <summary>
        /// 根据用户角色获取左侧菜单树
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<SysMenu> SelectMenuTreeByRoleIds(List<long> roleIds)
        {
            var menuTypes = new string[] { "M", "C"};
            return Db.Queryable<SysMenu, SysRoleMenu>((menu, roleMenu) => new JoinQueryInfos(
                 JoinType.Left, menu.menuId == roleMenu.Menu_id
                 ))
                .Where((menu, roleMenu) => roleIds.Contains(roleMenu.Role_id) && menuTypes.Contains(menu.menuType) && menu.status == "0")
                .OrderBy((menu, roleMenu) => new { menu.parentId, menu.orderNum })
                .Select((menu, roleMenu) => menu).ToList();
        }

        #endregion

        /// <summary>
        /// 获取菜单详情
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public SysMenu SelectMenuById(int menuId)
        {
            return Db.Queryable<SysMenu>().Where(it => it.menuId == menuId).Single();
        }

        /// <summary>
        /// 添加菜单
        /// </summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        public int AddMenu(SysMenu menu)
        {
            menu.Create_time = Db.GetDate();
            menu.menuId = Db.Insertable(menu).ExecuteReturnIdentity();
            return 1;
        }

        /// <summary>
        /// 编辑菜单
        /// </summary>
        /// <param name="menu"></param>
        /// <returns></returns>
        public int EditMenu(SysMenu menu)
        {
            return Db.Updateable(menu).ExecuteCommand();
        }

        /// <summary>
        /// 删除菜单
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public int DeleteMenuById(int menuId)
        {
            return Db.Deleteable<SysMenu>().Where(it => it.menuId == menuId).ExecuteCommand();
        }

        /// <summary>
        /// 菜单排序
        /// </summary>
        /// <param name="menuDto">菜单Dto</param>
        /// <returns></returns>
        public int ChangeSortMenu(MenuDto menuDto)
        {
            var result = Db.Updateable(new SysMenu() { menuId = menuDto.MenuId, orderNum = menuDto.orderNum })
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
            return Db.Queryable<SysMenu, SysRoleMenu, SysUserRole, SysRole>((m, rm, ur, r) => new JoinQueryInfos(
                JoinType.Left, m.menuId == rm.Menu_id,
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
            return Db.Queryable<SysMenu>()
                .Where(it => it.menuName == menu.menuName && it.parentId == menu.parentId).Single();
        }

        /// <summary>
        /// 是否存在菜单子节点
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public int HasChildByMenuId(long menuId)
        {
            return Db.Queryable<SysMenu>().Where(it => it.parentId == menuId).Count();
        }

        #region RoleMenu

        /// <summary>
        /// 查询菜单使用数量
        /// </summary>
        /// <param name="menuId"></param>
        /// <returns></returns>
        public int CheckMenuExistRole(long menuId)
        {
            return Db.Queryable<SysRoleMenu>().Where(it => it.Menu_id == menuId).Count();
        }

        #endregion
    }
}
