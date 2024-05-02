using Infrastructure;
using Infrastructure.Attribute;
using System.Collections;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 角色
    /// </summary>
    [AppService(ServiceType = typeof(ISysRoleService), ServiceLifetime = LifeTime.Transient)]
    public class SysRoleService : BaseService<SysRole>, ISysRoleService
    {
        private ISysUserRoleService SysUserRoleService;
        private ISysDeptService DeptService;
        private ISysRoleMenuService RoleMenuService;
        public SysRoleService(
            ISysUserRoleService sysUserRoleService,
            ISysDeptService deptService,
            ISysRoleMenuService roleMenuService)
        {
            SysUserRoleService = sysUserRoleService;
            DeptService = deptService;
            RoleMenuService = roleMenuService;
        }

        /// <summary>
        /// 根据条件分页查询角色数据
        /// </summary>
        /// <param name="sysRole">角色信息</param>
        /// <param name="pager">分页信息</param>
        /// <returns>角色数据集合信息</returns>
        public PagedInfo<SysRole> SelectRoleList(SysRole sysRole, PagerInfo pager)
        {
            var exp = Expressionable.Create<SysRole>();
            exp.And(role => role.DelFlag == 0);
            exp.AndIF(!string.IsNullOrEmpty(sysRole.RoleName), role => role.RoleName.Contains(sysRole.RoleName));
            exp.AndIF(sysRole.Status != -1, role => role.Status == sysRole.Status);
            exp.AndIF(!string.IsNullOrEmpty(sysRole.RoleKey), role => role.RoleKey == sysRole.RoleKey);

            var query = Queryable()
                .Where(exp.ToExpression())
                .OrderBy(x => x.RoleSort)
                .Select((role) => new SysRole
                {
                    UserNum = SqlFunc.Subqueryable<SysUserRole>().Where(f => f.RoleId == role.RoleId).Count()
                }, true);

            return query.ToPage(pager);
        }

        /// <summary>
        /// 查询所有角色
        /// </summary>
        /// <returns></returns>
        public List<SysRole> SelectRoleAll()
        {
            return Queryable()
                .Where(role => role.DelFlag == 0)
                .OrderBy(role => role.RoleSort)
                .ToList();
        }

        /// <summary>
        /// 根据用户查询
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<SysRole> SelectRolePermissionByUserId(long userId)
        {
            return Queryable()
                .Where(role => role.DelFlag == 0)
                .Where(it => SqlFunc.Subqueryable<SysUserRole>().Where(s => s.UserId == userId).Any())
                .OrderBy(role => role.RoleSort)
                .ToList();
        }

        /// <summary>
        /// 通过角色ID查询角色
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns>角色对象信息</returns>
        public SysRole SelectRoleById(long roleId)
        {
            return GetId(roleId);
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
            return Delete(roleIds);
        }

        /// <summary>
        /// 更改角色权限状态
        /// </summary>
        /// <param name="roleDto"></param>
        /// <returns></returns>
        public int UpdateRoleStatus(SysRole roleDto)
        {
            return Update(roleDto, it => new { it.Status }, f => f.RoleId == roleDto.RoleId);
        }

        /// <summary>
        /// 校验角色权限是否唯一
        /// </summary>
        /// <param name="sysRole">角色信息</param>
        /// <returns></returns>
        public string CheckRoleKeyUnique(SysRole sysRole)
        {
            long roleId = 0 == sysRole.RoleId ? -1L : sysRole.RoleId;
            SysRole info = GetFirst(it => it.RoleKey == sysRole.RoleKey);
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
            sysRole.Create_time = DateTime.Now;
            sysRole.RoleId = InsertReturnBigIdentity(sysRole);
            //插入角色部门数据
            DeptService.InsertRoleDepts(sysRole);

            return sysRole.RoleId;
        }

        /// <summary>
        /// 通过角色ID删除角色和菜单关联
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns></returns>
        public int DeleteRoleMenuByRoleId(long roleId)
        {
            return RoleMenuService.DeleteRoleMenuByRoleId(roleId);
        }

        /// <summary>
        /// 修改数据权限信息
        /// </summary>
        /// <param name="sysRoleDto"></param>
        /// <returns></returns>
        public bool AuthDataScope(SysRoleDto sysRoleDto)
        {
            var result = UseTran(() =>
            {
                //删除角色菜单
                //DeleteRoleMenuByRoleId(sysRoleDto.RoleId);
                //InsertRoleMenu(sysRoleDto);
                var oldMenus = SelectUserRoleMenus(sysRoleDto.RoleId);
                var newMenus = sysRoleDto.MenuIds;

                //并集菜单
                var arr_c = oldMenus.Intersect(newMenus).ToArray();
                //获取减量菜单
                var delMenuIds = oldMenus.Where(c => !arr_c.Contains(c)).ToArray();
                //获取增量
                var addMenuIds = newMenus.Where(c => !arr_c.Contains(c)).ToArray();

                RoleMenuService.DeleteRoleMenuByRoleIdMenuIds(sysRoleDto.RoleId, delMenuIds);
                sysRoleDto.MenuIds = addMenuIds.ToList();
                sysRoleDto.DelMenuIds = delMenuIds.ToList();
                InsertRoleMenu(sysRoleDto);
                Console.WriteLine($"减少了{delMenuIds.Length},增加了{addMenuIds.Length}菜单");
            });
            return result.IsSuccess;
        }

        #region Service


        /// <summary>
        /// 批量新增角色菜单信息
        /// </summary>
        /// <param name="sysRoleDto"></param>
        /// <returns></returns>
        public int InsertRoleMenu(SysRoleDto sysRoleDto)
        {
            int rows = 1;
            // 新增用户与角色管理
            List<SysRoleMenu> sysRoleMenus = new();
            foreach (var item in sysRoleDto.MenuIds)
            {
                SysRoleMenu rm = new()
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
                rows = RoleMenuService.AddRoleMenu(sysRoleMenus);
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

            return ((IList)roles).Contains(GlobalConstant.AdminRole);
        }

        /// <summary>
        /// 判断是否是管理员
        /// </summary>
        /// <param name="roleid"></param>
        /// <returns></returns>
        public bool IsRoleAdmin(long roleid)
        {
            var roleInfo = GetFirst(x => x.RoleId == roleid);

            return roleInfo.RoleKey == GlobalConstant.AdminRole;
        }

        /// <summary>
        /// 获取角色菜单id集合
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public List<long> SelectUserRoleMenus(long roleId)
        {
            var list = RoleMenuService.SelectRoleMenuByRoleId(roleId);

            return list.Select(x => x.Menu_id).Distinct().ToList();
        }

        /// <summary>
        /// 根据用户所有角色获取菜单
        /// </summary>
        /// <param name="roleIds"></param>
        /// <returns></returns>
        public List<long> SelectRoleMenuByRoleIds(long[] roleIds)
        {
            return RoleMenuService.SelectRoleMenuByRoleIds(roleIds)
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
            return Context.Queryable<SysUserRole>()
                .LeftJoin<SysRole>((ur, r) => ur.RoleId == r.RoleId)
                .Where((ur, r) => ur.UserId == userId && r.RoleId > 0)
                .WithCache(60 * 10)
                .Select((ur, r) => r)
                .ToList();
        }

        /// <summary>
        /// 获取用户权限集合
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<long> SelectUserRoles(long userId)
        {
            var list = SelectUserRoleListByUserId(userId).Where(f => f.Status == 0);

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
                UpdateSysRole(sysRole);
                //删除角色与部门管理
                DeptService.DeleteRoleDeptByRoleId(sysRole.RoleId);
                //插入角色部门数据
                DeptService.InsertRoleDepts(sysRole);
            });

            return result.IsSuccess ? 1 : 0;
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
    }
}
