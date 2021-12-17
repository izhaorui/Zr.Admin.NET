using Infrastructure.Attribute;
using System;
using System.Collections.Generic;
using System.Text;
using ZR.Model;
using ZR.Model.System.Dto;
using ZR.Model.System;
using ZR.Repository.System;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    /// <summary>
    /// 用户角色
    /// </summary>
    [AppService(ServiceType = typeof(ISysUserRoleService), ServiceLifetime = LifeTime.Transient)]
    public class SysUserRoleService : ISysUserRoleService
    {
        public SysUserRoleRepository SysUserRoleRepository;

        public SysUserRoleService(SysUserRoleRepository sysUserRoleRepository)
        {
            SysUserRoleRepository = sysUserRoleRepository;
        }


        /// <summary>
        /// 通过角色ID查询角色使用数量
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public int CountUserRoleByRoleId(long roleId)
        {
            return SysUserRoleRepository.CountUserRoleByRoleId(roleId);
        }

        /// <summary>
        /// 删除用户角色
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public int DeleteUserRoleByUserId(int userId)
        {
            return SysUserRoleRepository.DeleteUserRoleByUserId(userId);
        }

        /// <summary>
        /// 批量删除角色对应用户
        /// </summary>
        /// <param name="roleId"></param>
        /// <param name="userIds"></param>
        /// <returns></returns>
        public int DeleteRoleUserByUserIds(long roleId, List<long> userIds)
        {
            return SysUserRoleRepository.DeleteRoleUserByUserIds(roleId, userIds);
        }

        /// <summary>
        /// 添加用户角色
        /// </summary>
        /// <param name="sysRoleMenus"></param>
        /// <returns></returns>
        public int AddUserRole(List<SysUserRole> sysUsers)
        {
            return SysUserRoleRepository.AddUserRole(sysUsers);
        }

        /// <summary>
        /// 获取用户数据根据角色id
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public List<SysUser> GetSysUsersByRoleId(long roleId)
        {
            return SysUserRoleRepository.GetSysUsersByRoleId(roleId);
        }

        /// <summary>
        /// 获取用户数据根据角色id
        /// </summary>
        /// <param name="roleUserQueryDto"></param>
        /// <returns></returns>
        public PagedInfo<SysUser> GetSysUsersByRoleId(RoleUserQueryDto roleUserQueryDto)
        {
            return SysUserRoleRepository.GetSysUsersByRoleId(roleUserQueryDto);
        }

        /// <summary>
        /// 获取尚未指派的用户数据根据角色id
        /// </summary>
        /// <param name="roleUserQueryDto"></param>
        /// <returns></returns>
        public PagedInfo<SysUser> GetExcludedSysUsersByRoleId(RoleUserQueryDto roleUserQueryDto)
        {
            return SysUserRoleRepository.GetExcludedSysUsersByRoleId(roleUserQueryDto);
        }

        /// <summary>
        /// 新增用户角色信息
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public int InsertUserRole(SysUser user)
        {
            List<SysUserRole> userRoles = new List<SysUserRole>();
            foreach (var item in user.RoleIds)
            {
                userRoles.Add(new SysUserRole() { RoleId = item, UserId = user.UserId });
            }

            return userRoles.Count > 0 ? AddUserRole(userRoles) : 0;
        }

        /// <summary>
        /// 新增加角色用户
        /// </summary>
        /// <param name="roleId">角色id</param>
        /// <param name="userids">用户ids</param>
        /// <returns></returns>
        public int InsertRoleUser(RoleUsersCreateDto roleUsersCreateDto)
        {
            List<SysUserRole> userRoles = new List<SysUserRole>();
            foreach (var item in roleUsersCreateDto.UserIds)
            {
                userRoles.Add(new SysUserRole() { RoleId = roleUsersCreateDto.RoleId, UserId = item });
            }

            return userRoles.Count > 0 ? AddUserRole(userRoles) : 0;
        }
    }
}
