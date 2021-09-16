using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZR.Model.System.Dto;
using ZR.Model.System;

namespace ZR.Service.IService
{
    public interface ISysUserRoleService
    {
        public int CountUserRoleByRoleId(long roleId);

        /// <summary>
        /// 删除用户角色
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public int DeleteUserRoleByUserId(int userId);

        /// <summary>
        /// 批量删除角色对应用户
        /// </summary>
        /// <param name="roleId"></param>
        /// <param name="userIds"></param>
        /// <returns></returns>
        public int DeleteRoleUserByUserIds(long roleId, List<long> userIds);

        /// <summary>
        /// 添加用户角色
        /// </summary>
        /// <param name="sysRoleMenus"></param>
        /// <returns></returns>
        public int AddUserRole(List<SysUserRole> sysUsers);

        public List<SysUser> GetSysUsersByRoleId(long roleId);

        /// <summary>
        /// 新增用户角色信息
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public int InsertUserRole(SysUser user);

        /// <summary>
        /// 新增加角色用户
        /// </summary>
        /// <param name="roleId">角色id</param>
        /// <param name="userids">用户ids</param>
        /// <returns></returns>
        public int InsertRoleUser(RoleUsersCreateDto roleUsersCreateDto);
    }
}
