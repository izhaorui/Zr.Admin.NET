using Infrastructure.Attribute;
using Infrastructure.Model;
using SqlSugar;
using System.Collections.Generic;
using System.Linq;
using ZR.Model;
using ZR.Model.System;

namespace ZR.Repository.System
{
    /// <summary>
    /// 角色操作类
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysRoleRepository : BaseRepository<SysRole>
    {
        /// <summary>
        /// 查询所有角色数据
        /// </summary>
        /// <returns></returns>
        public List<SysRole> SelectRoleList()
        {
            return Context.Queryable<SysRole>()
                .Where(role => role.DelFlag == "0")
                .OrderBy(role => role.RoleSort)
                .ToList();
        }
        /// <summary>
        /// 根据条件分页查询角色数据
        /// </summary>
        /// <param name="sysRole"></param>
        /// <param name="pager"></param>
        /// <returns></returns>
        public PagedInfo<SysRole> SelectRoleList(SysRole sysRole, PagerInfo pager)
        {
            var exp = Expressionable.Create<SysRole>();
            exp.And(role => role.DelFlag == "0");
            exp.AndIF(!string.IsNullOrEmpty(sysRole.RoleName), role => role.RoleName.Contains(sysRole.RoleName));
            exp.AndIF(!string.IsNullOrEmpty(sysRole.Status), role => role.Status == sysRole.Status);
            exp.AndIF(!string.IsNullOrEmpty(sysRole.RoleKey), role => role.RoleKey == sysRole.RoleKey);

            var query = Context.Queryable<SysRole>()
                .Where(exp.ToExpression())
                .OrderBy(x => x.RoleSort)
                .Select((role) => new SysRole
                {
                    RoleId = role.RoleId.SelectAll(),
                    UserNum = SqlFunc.Subqueryable<SysUserRole>().Where(f => f.RoleId == role.RoleId).Count()
                });

            return query.ToPage(pager);
        }

        /// <summary>
        /// 根据用户查询
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<SysRole> SelectRolePermissionByUserId(long userId)
        {
            return Context.Queryable<SysRole>()
                .Where(role => role.DelFlag == "0")
                .Where(it => SqlFunc.Subqueryable<SysUserRole>().Where(s => s.UserId == userId).Any())
                .OrderBy(role => role.RoleSort)
                .ToList();
        }

        /// <summary>
        /// 通过角色ID查询角色
        /// </summary>
        /// <param name="roleId">角色编号</param>
        /// <returns></returns>
        public SysRole SelectRoleById(long roleId)
        {
            return Context.Queryable<SysRole>().InSingle(roleId);
        }

        /// <summary>
        /// 通过角色ID删除角色
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public int DeleteRoleByRoleIds(long[] roleId)
        {
            return Context.Deleteable<SysRole>().In(roleId).ExecuteCommand();
        }

        /// <summary>
        /// 获取用户所有角色信息
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<SysRole> SelectUserRoleListByUserId(long userId)
        {
            return Context.Queryable<SysUserRole, SysRole>((ur, r) => new JoinQueryInfos(
                    JoinType.Left, ur.RoleId == r.RoleId
                )).Where((ur, r) => ur.UserId == userId)
                .Select((ur, r) => r).ToList();
        }

        #region 用户角色对应菜单 用户N-1角色

        /// <summary>
        /// 根据角色获取菜单id
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public List<SysRoleMenu> SelectRoleMenuByRoleId(long roleId)
        {
            return Context.Queryable<SysRoleMenu>().Where(it => it.Role_id == roleId).ToList();
        }

        /// <summary>
        /// 根据用户所有角色获取菜单
        /// </summary>
        /// <param name="roleIds"></param>
        /// <returns></returns>
        public List<SysRoleMenu> SelectRoleMenuByRoleIds(long[] roleIds)
        {
            return Context.Queryable<SysRoleMenu>().Where(it => roleIds.Contains(it.Role_id)).ToList();
        }

        /// <summary>
        /// 批量插入用户菜单
        /// </summary>
        /// <param name="sysRoleMenus"></param>
        /// <returns></returns>
        public int AddRoleMenu(List<SysRoleMenu> sysRoleMenus)
        {
            return Context.Insertable(sysRoleMenus).ExecuteCommand();
        }

        /// <summary>
        /// 删除角色与菜单关联
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public int DeleteRoleMenuByRoleId(long roleId)
        {
            return Context.Deleteable<SysRoleMenu>().Where(it => it.Role_id == roleId).ExecuteCommand();
        }

        #endregion

        /// <summary>
        /// 添加角色
        /// </summary>
        /// <param name="sysRole"></param>
        /// <returns></returns>
        public long InsertRole(SysRole sysRole)
        {
            sysRole.Create_time = Context.GetDate();
            return Context.Insertable(sysRole).ExecuteReturnBigIdentity();
        }

        /// <summary>
        /// 修改用户角色
        /// </summary>
        /// <param name="sysRole"></param>
        /// <returns></returns>
        public int UpdateSysRole(SysRole sysRole)
        {
            var db = Context;
            sysRole.Update_time = db.GetDate();

            return db.Updateable<SysRole>()
            .SetColumns(it => it.Update_time == sysRole.Update_time)
            .SetColumns(it => it.DataScope == sysRole.DataScope)
            .SetColumns(it => it.Remark == sysRole.Remark)
            .SetColumns(it => it.Update_by == sysRole.Update_by)
            //.SetColumns(it => it.MenuCheckStrictly == sysRole.MenuCheckStrictly)
            .SetColumns(it => it.DeptCheckStrictly == sysRole.DeptCheckStrictly)
            .SetColumnsIF(!string.IsNullOrEmpty(sysRole.RoleName), it => it.RoleName == sysRole.RoleName)
            .SetColumnsIF(!string.IsNullOrEmpty(sysRole.RoleKey), it => it.RoleKey == sysRole.RoleKey)
            .SetColumnsIF(sysRole.RoleSort >= 0, it => it.RoleSort == sysRole.RoleSort)
            .Where(it => it.RoleId == sysRole.RoleId)
            .ExecuteCommand();
        }

        /// <summary>
        /// 更改角色权限状态
        /// </summary>
        /// <param name="roleId"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        public int UpdateRoleStatus(SysRole role)
        {
            return Context.Updateable(role).UpdateColumns(t => new { t.Status }).ExecuteCommand();
        }

        /// <summary>
        /// 检查角色权限是否存在
        /// </summary>
        /// <param name="roleKey">角色权限</param>
        /// <returns></returns>
        public SysRole CheckRoleKeyUnique(string roleKey)
        {
            return Context.Queryable<SysRole>().Where(it => it.RoleKey == roleKey).Single();
        }

        /// <summary>
        /// 校验角色名称是否唯一
        /// </summary>
        /// <param name="roleName">角色名称</param>
        /// <returns></returns>
        public SysRole CheckRoleNameUnique(string roleName)
        {
            return Context.Queryable<SysRole>().Where(it => it.RoleName == roleName).Single();
        }
    }
}
