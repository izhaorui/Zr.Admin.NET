using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using ZR.Common;
using ZR.Admin.WebApi.Filters;
using ZR.Model;
using ZR.Model.System;
using ZR.Service.System.IService;
using ZR.Admin.WebApi.Extensions;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 角色信息
    /// </summary>
    [Verify]
    [Route("system/role")]
    public class SysRoleController : BaseController
    {
        private readonly ISysRoleService sysRoleService;

        public SysRoleController(
            ISysRoleService sysRoleService)
        {
            this.sysRoleService = sysRoleService;
        }

        /// <summary>
        /// 获取系统角色管理
        /// </summary>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:role:list")]
        [HttpGet("list")]
        public IActionResult List([FromQuery] SysRole role, [FromQuery] PagerInfo pager)
        {
            var list = sysRoleService.SelectRoleList(role, pager);

            return SUCCESS(list, TIME_FORMAT_FULL);
        }

        /// <summary>
        /// 根据角色编号获取详细信息
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        [HttpGet("{roleId}")]
        public IActionResult GetInfo(long roleId = 0)
        {
            var info = sysRoleService.SelectRoleById(roleId);

            return SUCCESS(info, TIME_FORMAT_FULL);
        }

        /// <summary>
        /// 添加角色
        /// </summary>
        /// <param name="sysRoleDto"></param>
        /// <returns></returns>
        [HttpPost]
        [ActionPermissionFilter(Permission = "system:role:add")]
        [Log(Title = "角色管理", BusinessType = BusinessType.INSERT)]
        [Route("edit")]
        public IActionResult RoleAdd([FromBody] SysRole sysRoleDto)
        {
            if (sysRoleDto == null) return ToResponse(ApiResult.Error(101, "请求参数错误"));

            if (UserConstants.NOT_UNIQUE.Equals(sysRoleService.CheckRoleKeyUnique(sysRoleDto)))
            {
                return ToResponse(ApiResult.Error((int)ResultCode.CUSTOM_ERROR, $"新增角色'{sysRoleDto.RoleName}'失败，角色权限已存在"));
            }

            sysRoleDto.Create_by = HttpContext.GetName();
            long roleId = sysRoleService.InsertRole(sysRoleDto);

            return ToResponse(ToJson(roleId));
        }

        /// <summary>
        /// 修改角色 √
        /// </summary>
        /// <param name="sysRoleDto"></param>
        /// <returns></returns>
        [HttpPut]
        [ActionPermissionFilter(Permission = "system:role:edit")]
        [Log(Title = "角色管理", BusinessType = BusinessType.UPDATE)]
        [Route("edit")]
        public IActionResult RoleEdit([FromBody] SysRole sysRoleDto)
        {
            if (sysRoleDto == null || sysRoleDto.RoleId <= 0 || string.IsNullOrEmpty(sysRoleDto.RoleKey))
            {
                return ToResponse(ApiResult.Error(101, "请求参数错误"));
            }
            sysRoleService.CheckRoleAllowed(sysRoleDto);
            var info = sysRoleService.SelectRoleById(sysRoleDto.RoleId);
            if (info != null && info.RoleKey != sysRoleDto.RoleKey)
            {
                if (UserConstants.NOT_UNIQUE.Equals(sysRoleService.CheckRoleKeyUnique(sysRoleDto)))
                {
                    return ToResponse(ApiResult.Error($"编辑角色'{sysRoleDto.RoleName}'失败，角色权限已存在"));
                }
            }
            sysRoleDto.Update_by = HttpContext.GetName();
            int upResult = sysRoleService.UpdateRole(sysRoleDto);
            if (upResult > 0)
            {
                return SUCCESS(upResult);
            }
            return ToResponse(ApiResult.Error($"修改角色'{sysRoleDto.RoleName}'失败，请联系管理员"));
        }

        /// <summary>
        /// 根据角色分配菜单
        /// </summary>
        /// <param name="sysRoleDto"></param>
        /// <returns></returns>
        [HttpPut("dataScope")]
        [ActionPermissionFilter(Permission = "system:role:authorize")]
        [Log(Title = "角色管理", BusinessType = BusinessType.UPDATE)]
        public IActionResult DataScope([FromBody] SysRole sysRoleDto)
        {
            if (sysRoleDto == null || sysRoleDto.RoleId <= 0) return ToResponse(ApiResult.Error(101, "请求参数错误"));

            sysRoleDto.Create_by = HttpContext.GetName();
            sysRoleService.CheckRoleAllowed(sysRoleDto);

            bool result = sysRoleService.AuthDataScope(sysRoleDto);

            return SUCCESS(result);
        }

        /// <summary>
        /// 角色删除
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        [HttpDelete("{roleId}")]
        [Log(Title = "角色管理", BusinessType = BusinessType.DELETE)]
        [ActionPermissionFilter(Permission = "system:role:remove")]
        public IActionResult Remove(string roleId)
        {
            long[] roleIds = Tools.SpitLongArrary(roleId);
            int result = sysRoleService.DeleteRoleByRoleId(roleIds);

            return ToResponse(ToJson(result));
        }

        /// <summary>
        /// 修改角色状态
        /// </summary>
        /// <param name="roleDto">角色对象</param>
        /// <returns></returns>
        [HttpPut("changeStatus")]
        [Log(Title = "修改角色状态", BusinessType = BusinessType.UPDATE)]
        [ActionPermissionFilter(Permission = "system:role:edit")]
        public IActionResult ChangeStatus([FromBody] SysRole roleDto)
        {
            sysRoleService.CheckRoleAllowed(roleDto);
            int result = sysRoleService.UpdateRoleStatus(roleDto);

            return ToResponse(ToJson(result));
        }

        /// <summary>
        /// 角色导出
        /// </summary>
        /// <returns></returns>
        [Log(BusinessType = BusinessType.EXPORT, IsSaveResponseData = false, Title = "角色导出")]
        [HttpGet("export")]
        //[ActionPermissionFilter(Permission = "system:role:export")]
        public IActionResult Export()
        {
            var list = sysRoleService.SelectRoleAll();

            string sFileName = ExportExcel(list, "sysrole", "角色");
            return SUCCESS(new { path = "/export/" + sFileName, fileName = sFileName });
        }
    }
}
