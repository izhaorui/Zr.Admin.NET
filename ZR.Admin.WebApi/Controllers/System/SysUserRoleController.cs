using Infrastructure;
using Infrastructure.Attribute;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using ZR.Admin.WebApi.Filters;
using ZR.Model;
using ZR.Model.Dto.System;
using ZR.Model.System;
using ZR.Service.IService;

namespace ZR.Admin.WebApi.Controllers.System
{
    [Verify]
    [Route("system/userRole")]
    public class SysUserRoleController : BaseController
    {
        private readonly ISysUserRoleService SysUserRoleService;
        private readonly ISysUserService UserService;

        public SysUserRoleController(
            ISysUserRoleService sysUserRoleService,
            ISysUserService userService)
        {
            SysUserRoleService = sysUserRoleService;
            UserService = userService;
        }

        /// <summary>
        /// 根据角色编号获取已分配的用户
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        [HttpGet("get/{roleId}")]
        [ActionPermissionFilter(Permission = "system:roleusers:query")]
        public IActionResult GetList(long roleId = 0)
        {
            var list = SysUserRoleService.GetSysUsersByRoleId(roleId);

            return SUCCESS(list, TIME_FORMAT_FULL);
        }

        /// <summary>
        /// 添加角色用户
        /// </summary>
        /// <returns></returns>
        [HttpPost("create")]
        [ActionPermissionFilter(Permission = "system:roleusers:add")]
        [Log(Title = "添加角色用户", BusinessType = Infrastructure.Enums.BusinessType.INSERT)]
        public IActionResult Create([FromBody] RoleUsersCreateDto roleUsersCreateDto)
        {
            var response = SysUserRoleService.InsertRoleUser(roleUsersCreateDto);

            return SUCCESS(response);
        }

        /// <summary>
        /// 删除角色用户
        /// </summary>
        /// <param name="roleUsersCreateDto"></param>
        /// <returns></returns>
        [HttpPost("delete")]
        [ActionPermissionFilter(Permission = "system:roleusers:del")]
        [Log(Title = "删除角色用户", BusinessType = Infrastructure.Enums.BusinessType.DELETE)]
        public IActionResult Delete([FromBody] RoleUsersCreateDto roleUsersCreateDto)
        {
            return SUCCESS(SysUserRoleService.DeleteRoleUserByUserIds(roleUsersCreateDto.RoleId, roleUsersCreateDto.UserIds));
        }

        /// <summary>
        /// 获取未分配用户角色
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        [HttpGet("GetExcludeUsers")]
        public IActionResult GetExcludeUsers(long roleId = 0)
        {
            if (roleId <= 0)
            {
                throw new CustomException(ResultCode.PARAM_ERROR, "roleId不能为空");
            }
            // 取得该角色所有添加的用户
            var userIds = SysUserRoleService.GetSysUsersByRoleId(roleId).Select(f => f.UserId);

            SysUser userQuery = new();
            userQuery.Status = "0";
            PagerInfo pager = new(1, 50);

            // 获取未添加用户
            var list = UserService.SelectUserList(userQuery, pager).Where(m => !userIds.Contains(m.UserId));
            return SUCCESS(list, TIME_FORMAT_FULL);
        }
    }
}
