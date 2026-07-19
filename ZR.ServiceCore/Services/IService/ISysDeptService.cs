using System.Collections.Generic;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Model.System.Vo;

namespace ZR.ServiceCore.Services
{
    public interface ISysDeptService : IBaseService<SysDept>
    {
        List<SysDeptDto> GetList(SysDeptQueryDto dept);
        List<SysDept> GetSysDepts(SysDeptQueryDto dept);
        string CheckDeptNameUnique(SysDept dept);
        int InsertDept(SysDept dept);
        int UpdateDept(SysDept dept);
        void UpdateDeptChildren(long deptId, string newAncestors, string oldAncestors);
        List<SysDept> GetChildrenDepts(List<SysDept> depts, long deptId);
        List<SysDept> BuildDeptTree(List<SysDept> depts);
        List<TreeSelectVo> BuildDeptTreeSelect(List<SysDept> depts);
        List<SysRoleDept> SelectRoleDeptByRoleId(long roleId);

        List<long> SelectRoleDepts(long roleId);
        /// <summary>批量获取多个角色的自定义部门 ID 集合</summary>
        List<long> SelectRoleDeptsBatch(List<long> roleIds);
        /// <summary>获取指定部门的所有子部门 ID（含自身），用于登录时预计算 DEPT_CHILD</summary>
        List<long> GetChildDeptIds(long deptId);
        bool DeleteRoleDeptByRoleId(long roleId);
        int InsertRoleDepts(SysRole role);
    }

    public interface ISysRoleDeptService : IBaseService<SysRoleDept>
    {
        List<SysRoleDept> SelectRoleDeptByRoleId(long roleId);
    }
}
