﻿using Microsoft.AspNetCore.Mvc;
using System.Collections;
using ZR.Admin.WebApi.Filters;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 部门
    /// </summary>
    [Verify]
    [Route("system/dept")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysDeptController(ISysDeptService deptService
            , ISysUserService userService) : BaseController
    {

        /// <summary>
        /// 获取部门列表
        /// </summary>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:dept:list")]
        [HttpGet("list")]
        public IActionResult List([FromQuery] SysDeptQueryDto dept)
        {
            return SUCCESS(deptService.GetList(dept));
        }

        /// <summary>
        /// 查询部门列表（排除节点）
        /// </summary>
        /// <param name="deptId"></param>
        /// <returns></returns>
        [HttpGet("list/exclude/{deptId}")]
        public IActionResult ExcludeChild(long deptId)
        {
            var depts = deptService.GetList(new SysDeptQueryDto());

            for (int i = 0; i < depts.Count; i++)
            {
                SysDeptDto d = depts[i];
                long[] deptIds = Tools.SpitLongArrary(d.Ancestors);
                if (d.DeptId == deptId || ((IList)deptIds).Contains(deptId))
                {
                    depts.Remove(d);
                }
            }
            return SUCCESS(depts);
        }

        /// <summary>
        /// 获取部门下拉树列表
        /// </summary>
        /// <param name="dept"></param>
        /// <returns></returns>
        [HttpGet("treeselect")]
        public IActionResult TreeSelect(SysDeptQueryDto dept)
        {
            var depts = deptService.GetSysDepts(dept);

            return SUCCESS(deptService.BuildDeptTreeSelect(depts), TIME_FORMAT_FULL);
        }

        /// <summary>
        /// 获取角色部门信息
        /// 加载对应角色部门列表树
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>        
        [HttpGet("roleDeptTreeselect/{roleId}")]
        public IActionResult RoleMenuTreeselect(int roleId)
        {
            var depts = deptService.GetSysDepts(new SysDeptQueryDto());
            var checkedKeys = deptService.SelectRoleDepts(roleId);
            return SUCCESS(new
            {
                checkedKeys,
                depts = deptService.BuildDeptTreeSelect(depts),
            });
        }

        /// <summary>
        /// 根据部门编号获取详细信息
        /// </summary>
        /// <returns></returns>
        [HttpGet("{deptId}")]
        [ActionPermissionFilter(Permission = "system:dept:query")]
        public IActionResult GetInfo(long deptId)
        {
            var info = deptService.GetFirst(f => f.DeptId == deptId);
            return SUCCESS(info);
        }

        /// <summary>
        /// 新增部门
        /// </summary>
        /// <param name="dept"></param>
        /// <returns></returns>
        [HttpPost]
        [Log(Title = "部门管理", BusinessType = BusinessType.INSERT)]
        [ActionPermissionFilter(Permission = "system:dept:add")]
        public IActionResult Add([FromBody] SysDept dept)
        {
            if (UserConstants.NOT_UNIQUE.Equals(deptService.CheckDeptNameUnique(dept)))
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, $"新增部门{dept.DeptName}失败，部门名称已存在");
            }
            dept.Create_by = HttpContext.GetName();
            return ToResponse(deptService.InsertDept(dept));
        }

        /// <summary>
        /// 修改部门
        /// </summary>
        /// <param name="dept"></param>
        /// <returns></returns>
        [HttpPut]
        [Log(Title = "部门管理", BusinessType = BusinessType.UPDATE)]
        [ActionPermissionFilter(Permission = "system:dept:update")]
        public IActionResult Update([FromBody] SysDept dept)
        {
            if (UserConstants.NOT_UNIQUE.Equals(deptService.CheckDeptNameUnique(dept)))
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, $"修改部门{dept.DeptName}失败，部门名称已存在");
            }
            else if (dept.ParentId.Equals(dept.DeptId))
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, $"修改部门{dept.DeptName}失败，上级部门不能是自己");
            }
            dept.Update_by = HttpContext.GetName();
            return ToResponse(deptService.UpdateDept(dept));
        }

        /// <summary>
        /// 删除部门
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{deptId}")]
        [ActionPermissionFilter(Permission = "system:dept:remove")]
        [Log(Title = "部门管理", BusinessType = BusinessType.DELETE)]
        public IActionResult Remove(long deptId)
        {
            if (deptService.Queryable().Count(it => it.ParentId == deptId && it.DelFlag == 0) > 0)
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, $"存在下级部门，不允许删除");
            }
            if (userService.Queryable().Count(it => it.DeptId == deptId && it.DelFlag == 0) > 0)
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, $"部门存在用户，不允许删除");
            }

            return SUCCESS(deptService.Delete(deptId));
        }
    }
}
