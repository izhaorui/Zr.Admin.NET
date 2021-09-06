using Infrastructure.Attribute;
using SqlSugar;
using System.Collections.Generic;
using ZR.Model.System;

namespace ZR.Repository.System
{
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysUserRoleRepository : BaseRepository
    {
        /// <summary>
        /// 删除用户角色
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public int DeleteUserRoleByUserId(int userId)
        {
            return Db.Deleteable<SysUserRole>().Where(it => it.UserId == userId).ExecuteCommand();
        }

        /// <summary>
        /// 批量删除角色对应用户
        /// </summary>
        /// <param name="roleId">角色id</param>
        /// <param name="userIds">用户id集合</param>
        /// <returns></returns>
        public int DeleteRoleUserByUserIds(long roleId, List<long> userIds)
        {
            return Db.Deleteable<SysUserRole>().Where(it => it.RoleId == roleId && userIds.Contains(it.UserId))
                .ExecuteCommand();
        }

        /// <summary>
        /// 添加用户角色
        /// </summary>
        /// <param name="sysUsers"></param>
        /// <returns></returns>
        public int AddUserRole(List<SysUserRole> sysUsers)
        {
            return Db.Insertable(sysUsers).ExecuteCommand();
        }

        /// <summary>
        /// 删除角色关联的用户
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public int DeleteUserRoleByRoleId(int roleId)
        {
            return Db.Deleteable<SysUserRole>().In(roleId).ExecuteCommand();
        }

        /// <summary>
        /// 获取角色分配个数
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public int CountUserRoleByRoleId(long roleId)
        {
            return Db.Queryable<SysUserRole>().Where(it => it.RoleId == roleId).Count();
        }

        /// <summary>
        /// 获取用户数据根据角色id
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public List<SysUser> GetSysUsersByRoleId(long roleId)
        {
            return Db.Queryable<SysUserRole, SysUser>((t1, u) => new JoinQueryInfos(
                JoinType.Left, t1.UserId == u.UserId))
                .Where((t1, u) => t1.RoleId == roleId && u.DelFlag == "0")
                .Select((t1, u) => u)
                .ToList();
        }
    }
}
