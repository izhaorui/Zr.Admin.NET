﻿using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Filters;
using ZR.Model.System.Dto;


namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 用户角色管理
    /// </summary>
    [Verify]
    [Route("system/userRole")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysUserRoleController(
        ISysUserRoleService sysUserRoleService) : BaseController
    {

        /// <summary>
        /// 根据角色编号获取已分配的用户
        /// </summary>
        /// <param name="roleUserQueryDto"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "system:roleusers:list")]
        public IActionResult GetList([FromQuery] RoleUserQueryDto roleUserQueryDto)
        {
            var list = sysUserRoleService.GetSysUsersByRoleId(roleUserQueryDto);

            return SUCCESS(list, TIME_FORMAT_FULL);
        }

        /// <summary>
        /// 添加角色用户
        /// </summary>
        /// <returns></returns>
        [HttpPost("create")]
        [ActionPermissionFilter(Permission = "system:roleusers:add")]
        [Log(Title = "添加角色用户", BusinessType = BusinessType.INSERT)]
        public IActionResult Create([FromBody] RoleUsersCreateDto roleUsersCreateDto)
        {
            var response = sysUserRoleService.InsertRoleUser(roleUsersCreateDto);

            return SUCCESS(response);
        }

        /// <summary>
        /// 删除角色用户
        /// </summary>
        /// <param name="roleUsersCreateDto"></param>
        /// <returns></returns>
        [HttpPost("delete")]
        [ActionPermissionFilter(Permission = "system:roleusers:remove")]
        [Log(Title = "删除角色用户", BusinessType = BusinessType.DELETE)]
        public IActionResult Delete([FromBody] RoleUsersCreateDto roleUsersCreateDto)
        {
            return SUCCESS(sysUserRoleService.DeleteRoleUserByUserIds(roleUsersCreateDto.RoleId, roleUsersCreateDto.UserIds));
        }

        /// <summary>
        /// 获取未分配用户角色
        /// </summary>
        /// <param name="roleUserQueryDto"></param>
        /// <returns></returns>
        [HttpGet("GetExcludeUsers")]
        public IActionResult GetExcludeUsers([FromQuery] RoleUserQueryDto roleUserQueryDto)
        {
            if (roleUserQueryDto.RoleId <= 0)
            {
                throw new CustomException(ResultCode.PARAM_ERROR, "roleId不能为空");
            }

            // 获取未添加用户
            var list = sysUserRoleService.GetExcludedSysUsersByRoleId(roleUserQueryDto);

            return SUCCESS(list, TIME_FORMAT_FULL);
        }
    }
}
