using System.Collections.Generic;
using ZR.Model;
using ZR.Model.System;

namespace ZR.Service.System.IService
{
    public interface ISysRoleService : IBaseService<SysRole>
    {
        /// <summary>
        /// 根据条件分页查询角色数据
        /// </summary>
        /// <param name="role">角色信息</param>
        /// <param name="pager"></param>
        /// <returns>角色数据集合信息</returns>
        public PagedInfo<SysRole> SelectRoleList(SysRole role, PagerInfo pager);

        /// <summary>
        /// 查询所有角色
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        public List<SysRole> SelectRoleAll();

        /// <summary>
        /// 根据用户查询
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<SysRole> SelectRolePermissionByUserId(long userId);

        /// <summary>
        /// 通过角色ID查询角色
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns>角色对象信息</returns>
        public SysRole SelectRoleById(long roleId);

        /// <summary>
        /// 批量删除角色信息
        /// </summary>
        /// <param name="roleIds">需要删除的角色ID</param>
        /// <returns></returns>
        public int DeleteRoleByRoleId(long[] roleIds);

        /// <summary>
        /// 更改角色权限状态
        /// </summary>
        /// <param name="roleDto"></param>
        /// <returns></returns>
        public int UpdateRoleStatus(SysRole roleDto);

        /// <summary>
        /// 校验角色权限是否唯一
        /// </summary>
        /// <param name="sysRole">角色信息</param>
        /// <returns></returns>
        public string CheckRoleKeyUnique(SysRole sysRole);

        /// <summary>
        /// 校验角色是否允许操作
        /// </summary>
        /// <param name="role"></param>
        public void CheckRoleAllowed(SysRole role);

        /// <summary>
        /// 新增保存角色信息
        /// </summary>
        /// <param name="sysRole">角色信息</param>
        /// <returns></returns>
        public long InsertRole(SysRole sysRole);

        /// <summary>
        /// 通过角色ID删除角色和菜单关联
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        public int DeleteRoleMenuByRoleId(long roleId);

        /// <summary>
        /// 授权数据范围
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        bool AuthDataScope(SysRole role);
        #region Service


        /// <summary>
        /// 新增角色菜单信息
        /// </summary>
        /// <param name="sysRoleDto"></param>
        /// <returns></returns>
        public int InsertRoleMenu(SysRole sysRoleDto);

        /// <summary>
        /// 判断是否是管理员
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public bool IsAdmin(long userid);

        /// <summary>
        /// 获取角色菜单id集合
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<long> SelectUserRoleMenus(long roleId);
        List<long> SelectRoleMenuByRoleIds(long[] roleIds);
        /// <summary>
        /// 获取用户角色列表
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<SysRole> SelectUserRoleListByUserId(long userId);

        /// <summary>
        /// 获取用户权限集合
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<long> SelectUserRoles(long userId);

        /// <summary>
        /// 获取用户权限字符串集合
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<string> SelectUserRoleKeys(long userId);

        public List<string> SelectUserRoleNames(long userId);

        #endregion

        /// <summary>
        /// 修改保存角色信息
        /// </summary>
        /// <param name="sysRole">角色信息</param>
        /// <returns></returns>
        public int UpdateRole(SysRole sysRole);
    }
}
