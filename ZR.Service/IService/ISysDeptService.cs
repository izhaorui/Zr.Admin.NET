using System;
using System.Collections.Generic;
using System.Text;
using ZR.Model.System;
using ZR.Model.Vo.System;

namespace ZR.Service.IService
{
    public interface ISysDeptService : IBaseService<SysDept>
    {
        public List<SysDept> GetSysDepts(SysDept dept);
        public string CheckDeptNameUnique(SysDept dept);
        public int InsertDept(SysDept dept);
        public int UpdateDept(SysDept dept);
        public void UpdateDeptChildren(long deptId, string newAncestors, string oldAncestors);
        public List<SysDept> GetChildrenDepts(List<SysDept> depts, long deptId);
        public List<SysDept> BuildDeptTree(List<SysDept> depts);
        public List<TreeSelectVo> BuildDeptTreeSelect(List<SysDept> depts);
    }
}
