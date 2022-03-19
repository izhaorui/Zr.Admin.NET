using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZR.Model;
using ZR.Model.System;
using ZR.Repository.System;
using ZR.Service.System.IService;

namespace ZR.Service
{
    /// <summary>
    /// 角色
    /// </summary>
    [AppService(ServiceType = typeof(ISysRoleService), ServiceLifetime = LifeTime.Transient)]
    public class SysRoleService : BaseService<SysRole>, ISysRoleService
    {
        private SysRoleRepository SysRoleRepository;
        private ISysUserRoleService SysUserRoleService;
        private ISysDeptService DeptService;

        public SysRoleService(
            SysRoleRepository sysRoleRepository,
            ISysUserRoleService sysUserRoleService,
            ISysDeptService deptService)
        {
            SysRoleRepository = sysRoleRepository;
            SysUserRoleService = sysUserRoleService;
            DeptService = deptService;
        }

        /// <summary>
        /// 根据条件分页查询角色数据
        /// </summary>
        /// <param name="role">角色信息</param>
        /// <param name="pager">分页信息</param>
        /// <returns>角色数据集合信息</returns>
        public PagedInfo<SysRole> SelectRoleList(SysRole role, PagerInfo pager)
        {
            return SysRoleRepository.SelectRoleList(role, pager);
        }

        /// <summary>
        /// 查询所有角色
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        public List<SysRole> SelectRoleAll()
        {
            return SysRoleRepository.SelectRoleList();
        }

        /// <summary>
        /// 根据用户查询
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<SysRole> SelectRolePermissionByUserId(long userId)
        {
            return SysRoleRepository.SelectRolePermissionByUserId(userId);
        }

        /// <summary>
        /// 通过角色ID查询角色
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns>角色对象信息</returns>
        public SysRole SelectRoleById(long roleId)
        {
            return SysRoleRepository.SelectRoleById(roleId);
        }

        /// <summary>
        /// 批量删除角色信息
        /// </summary>
        /// <param name="roleIds">需要删除的角色ID</param>
        /// <returns></returns>
        public int DeleteRoleByRoleId(long[] roleIds)
        {
            foreach (var item in roleIds)
            {
                CheckRoleAllowed(new SysRole(item));
                SysRole role = SelectRoleById(item);
                if (SysUserRoleService.CountUserRoleByRoleId(item) > 0)
                {
                    throw new CustomException($"{role.RoleName}已分配,不能删除");
                }
            }
            return SysRoleRepository.DeleteRoleByRoleIds(roleIds);
        }

        /// <summary>
        /// 更改角色权限状态
        /// </summary>
        /// <param name="SysRoleDto"></param>
        /// <returns></returns>
        public int UpdateRoleStatus(SysRole roleDto)
        {
            return SysRoleRepository.UpdateRoleStatus(roleDto);
        }

        /// <summary>
        /// 校验角色权限是否唯一
        /// </summary>
        /// <param name="sysRole">角色信息</param>
        /// <returns></returns>
        public string CheckRoleKeyUnique(SysRole sysRole)
        {
            long roleId = 0 == sysRole.RoleId ? -1L : sysRole.RoleId;
            SysRole info = SysRoleRepository.CheckRoleKeyUnique(sysRole.RoleKey);
            if (info != null && info.RoleId != roleId)
            {
                return UserConstants.NOT_UNIQUE;
            }
            return UserConstants.UNIQUE;
        }

        /// <summary>
        /// 校验角色是否允许操作
        /// </summary>
        /// <param name="role"></param>
        public void CheckRoleAllowed(SysRole role)
        {
            if (IsRoleAdmin(role.RoleId))
            {
                throw new CustomException("不允许操作超级管理员角色");
            }
        }

        /// <summary>
        /// 新增保存角色信息
        /// </summary>
        /// <param name="sysRole">角色信息</param>
        /// <returns></returns>
        public long InsertRole(SysRole sysRole)
        {
            return SysRoleRepository.InsertRole(sysRole);
            //return InsertRoleMenu(sysRole);
        }

        /// <summary>
        /// 通过角色ID删除角色和菜单关联
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        public int DeleteRoleMenuByRoleId(long roleId)
        {
            return SysRoleRepository.DeleteRoleMenuByRoleId(roleId);
        }

        /// <summary>
        /// 修改数据权限信息
        /// </summary>
        /// <param name="sysRoleDto"></param>
        /// <returns></returns>
        public bool AuthDataScope(SysRole sysRoleDto)
        {
            return UseTran2(() =>
            {
                //删除角色菜单
                DeleteRoleMenuByRoleId(sysRoleDto.RoleId);
                InsertRoleMenu(sysRoleDto);
            });
        }
        #region Service


        /// <summary>
        /// 批量新增角色菜单信息
        /// </summary>
        /// <param name="sysRoleDto"></param>
        /// <returns></returns>
        public int InsertRoleMenu(SysRole sysRoleDto)
        {
            int rows = 1;
            // 新增用户与角色管理
            List<SysRoleMenu> sysRoleMenus = new List<SysRoleMenu>();
            foreach (var item in sysRoleDto.MenuIds)
            {
                SysRoleMenu rm = new SysRoleMenu
                {
                    Menu_id = item,
                    Role_id = sysRoleDto.RoleId,
                    Create_by = sysRoleDto.Create_by,
                    Create_time = DateTime.Now
                };
                sysRoleMenus.Add(rm);
            }
            //添加角色菜单
            if (sysRoleMenus.Count > 0)
            {
                rows = SysRoleRepository.AddRoleMenu(sysRoleMenus);
            }

            return rows;
        }

        /// <summary>
        /// 判断是否是管理员
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        public bool IsAdmin(long userid)
        {
            List<string> roles = SelectUserRoleKeys(userid);

            return ((IList)roles).Contains("admin");
        }

        /// <summary>
        /// 判断是否是管理员
        /// </summary>
        /// <param name="roleid"></param>
        /// <returns></returns>
        public bool IsRoleAdmin(long roleid)
        {
            var roleInfo = GetFirst(x => x.RoleId == roleid);

            return roleInfo.RoleKey == "admin";
        }

        /// <summary>
        /// 获取角色菜单id集合
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public List<long> SelectUserRoleMenus(long roleId)
        {
            var list = SysRoleRepository.SelectRoleMenuByRoleId(roleId);

            return list.Select(x => x.Menu_id).Distinct().ToList();
        }

        /// <summary>
        /// 根据用户所有角色获取菜单
        /// </summary>
        /// <param name="roleIds"></param>
        /// <returns></returns>
        public List<long> SelectRoleMenuByRoleIds(long[] roleIds)
        {
            return SysRoleRepository.SelectRoleMenuByRoleIds(roleIds)
                .Select(x => x.Menu_id)
                .Distinct().ToList();
        }

        /// <summary>
        /// 获取用户角色列表
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<SysRole> SelectUserRoleListByUserId(long userId)
        {
            return SysRoleRepository.SelectUserRoleListByUserId(userId);
        }

        /// <summary>
        /// 获取用户权限集合
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<long> SelectUserRoles(long userId)
        {
            var list = SelectUserRoleListByUserId(userId).Where(f => f.Status == "0");

            return list.Select(x => x.RoleId).ToList();
        }

        /// <summary>
        /// 获取用户权限字符串集合
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<string> SelectUserRoleKeys(long userId)
        {
            var list = SelectUserRoleListByUserId(userId);
            return list.Select(x => x.RoleKey).ToList();
        }

        /// <summary>
        /// 获取用户所有角色名
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<string> SelectUserRoleNames(long userId)
        {
            var list = SelectUserRoleListByUserId(userId);
            return list.Select(x => x.RoleName).ToList();
        }
        #endregion

        /// <summary>
        /// 修改保存角色信息
        /// </summary>
        /// <param name="sysRole">角色信息</param>
        /// <returns></returns>
        public int UpdateRole(SysRole sysRole)
        {
            var result = UseTran(() =>
            {
                //修改角色信息
                SysRoleRepository.UpdateSysRole(sysRole);
                //删除角色与部门管理
                DeptService.DeleteRoleDeptByRoleId(sysRole.RoleId);
                //插入角色部门数据
                DeptService.InsertRoleDepts(sysRole);
            });

            return result.IsSuccess ? 1 : 0;
        }
    }
}
