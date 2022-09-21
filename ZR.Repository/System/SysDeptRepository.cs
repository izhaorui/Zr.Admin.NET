using Infrastructure.Attribute;
using System.Collections.Generic;
using ZR.Model.System;

namespace ZR.Repository.System
{
    /// <summary>
    /// 角色部门
    /// </summary>
    [AppService(ServiceLifetime = LifeTime.Transient)]
    public class SysRoleDeptRepository : BaseRepository<SysRoleDept>
    {
        /// <summary>
        /// 根据角色获取菜单id
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public List<SysRoleDept> SelectRoleDeptByRoleId(long roleId)
        {
            return Context.Queryable<SysRoleDept>().Where(it => it.RoleId == roleId).ToList();
        }
    }
}
