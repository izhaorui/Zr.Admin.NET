using Infrastructure.Attribute;
using System.Collections.Generic;
using System.Linq;
using ZR.Model.System;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    /// <summary>
    /// 角色菜单
    /// </summary>
    [AppService(ServiceType = typeof(ISysRoleMenuService), ServiceLifetime = LifeTime.Transient)]
    public class SysRoleMenuService : BaseService<SysRoleMenu>, ISysRoleMenuService
    {
        public int AddRoleMenu(List<SysRoleMenu> sysRoleMenus)
        {
            return Insert(sysRoleMenus);
        }

        public bool CheckMenuExistRole(long menuId)
        {
            return Count(it => it.Menu_id == menuId) > 0;
        }

        public int DeleteRoleMenuByRoleId(long roleId)
        {
            return Delete(roleId);
        }

        /// <summary>
        /// 根据角色获取菜单id
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public List<SysRoleMenu> SelectRoleMenuByRoleId(long roleId)
        {
            return GetList(f => f.Role_id == roleId);
        }

        /// <summary>
        /// 根据用户所有角色获取菜单
        /// </summary>
        /// <param name="roleIds"></param>
        /// <returns></returns>
        public List<SysRoleMenu> SelectRoleMenuByRoleIds(long[] roleIds)
        {
            return GetList(it => roleIds.Contains(it.Role_id));
        }
    }
}
