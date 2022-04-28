using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Extensions;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZR.Common;
using ZR.Model.System;
using ZR.Model.Vo.System;
using ZR.Repository.System;
using ZR.Service.System.IService;

namespace ZR.Service.System
{
    /// <summary>
    /// 部门管理
    /// </summary>
    [AppService(ServiceType = typeof(ISysDeptService), ServiceLifetime = LifeTime.Transient)]
    public class SysDeptService : BaseService<SysDept>, ISysDeptService
    {
        public SysDeptRepository DeptRepository;
        public SysRoleDeptRepository RoleDeptRepository;
        public SysDeptService(SysDeptRepository deptRepository, SysRoleDeptRepository roleDeptRepository)
        {
            DeptRepository = deptRepository;
            RoleDeptRepository = roleDeptRepository;
        }

        /// <summary>
        /// 查询部门管理数据
        /// </summary>
        /// <param name="dept"></param>
        /// <returns></returns>
        public List<SysDept> GetSysDepts(SysDept dept)
        {
            //开始拼装查询条件
            var predicate = Expressionable.Create<SysDept>();
            predicate = predicate.And(it => it.DelFlag == "0");
            predicate = predicate.AndIF(dept.DeptName.IfNotEmpty(), it => it.DeptName.Contains(dept.DeptName));
            predicate = predicate.AndIF(dept.Status.IfNotEmpty(), it => it.Status == dept.Status);

            var response = DeptRepository.GetList(predicate.ToExpression());

            return response;
        }

        /// <summary>
        /// 校验部门名称是否唯一
        /// </summary>
        /// <param name="dept"></param>
        /// <returns></returns>
        public string CheckDeptNameUnique(SysDept dept)
        {
            long deptId = dept.DeptId == 0 ? -1L : dept.DeptId;
            SysDept info = DeptRepository.GetFirst(it => it.DeptName == dept.DeptName && it.ParentId == dept.ParentId);
            if (info != null && info.DeptId != deptId)
            {
                return UserConstants.NOT_UNIQUE;
            }
            return UserConstants.UNIQUE;
        }

        /// <summary>
        /// 新增保存部门信息
        /// </summary>
        /// <param name="dept"></param>
        /// <returns></returns>
        public int InsertDept(SysDept dept)
        {
            SysDept info = DeptRepository.GetFirst(it => it.DeptId == dept.ParentId);
            //如果父节点不为正常状态,则不允许新增子节点
            if (info != null && !UserConstants.DEPT_NORMAL.Equals(info?.Status))
            {
                throw new CustomException("部门停用，不允许新增");
            }
            dept.Ancestors = "";
            if (info != null)
            {
                dept.Ancestors = info.Ancestors + "," + dept.ParentId;
            }
            return DeptRepository.Add(dept);
        }

        /// <summary>
        /// 修改保存部门信息
        /// </summary>
        /// <param name="dept"></param>
        /// <returns></returns>
        public int UpdateDept(SysDept dept)
        {
            SysDept newParentDept = DeptRepository.GetFirst(it => it.DeptId == dept.ParentId);
            SysDept oldDept = DeptRepository.GetFirst(m => m.DeptId == dept.DeptId);
            if (newParentDept != null && oldDept != null)
            {
                string newAncestors = newParentDept.Ancestors + "," + newParentDept.DeptId;
                string oldAncestors = oldDept.Ancestors;
                dept.Ancestors = newAncestors;
                UpdateDeptChildren(dept.DeptId, newAncestors, oldAncestors);
            }
            int result = DeptRepository.Context.Updateable(dept).ExecuteCommand();
            if (UserConstants.DEPT_NORMAL.Equals(dept.Status) && dept.Ancestors.IfNotEmpty()
                && !"0".Equals(dept.Ancestors))
            {
                // 如果该部门是启用状态，则启用该部门的所有上级部门
                UpdateParentDeptStatusNormal(dept);
            }
            return result;
        }

        /// <summary>
        /// 修改该部门的父级部门状态
        /// </summary>
        /// <param name="dept">当前部门</param>
        private void UpdateParentDeptStatusNormal(SysDept dept)
        {
            long[] depts = Tools.SpitLongArrary(dept.Ancestors);
            dept.Status = "0";
            dept.Update_time = DateTime.Now;

            DeptRepository.Update(dept, it => new { it.Update_by, it.Update_time, it.Status }, f => depts.Contains(f.DeptId));
        }

        /// <summary>
        /// 修改子元素关系
        /// </summary>
        /// <param name="deptId">被修改的部门ID</param>
        /// <param name="newAncestors">新的父ID集合</param>
        /// <param name="oldAncestors">旧的父ID集合</param>
        public void UpdateDeptChildren(long deptId, string newAncestors, string oldAncestors)
        {
            List<SysDept> children = GetChildrenDepts(GetSysDepts(new SysDept()), deptId);

            foreach (var child in children)
            {
                string ancestors = child.Ancestors.ReplaceFirst(oldAncestors, newAncestors);
                long[] ancestorsArr = Tools.SpitLongArrary(ancestors).Distinct().ToArray();
                child.Ancestors = string.Join(",", ancestorsArr);
            }
            if (children.Any())
            {
                DeptRepository.UdateDeptChildren(children);
            }
        }

        /// <summary>
        /// 获取所有子部门
        /// </summary>
        /// <param name="depts"></param>
        /// <param name="deptId"></param>
        /// <returns></returns>
        public List<SysDept> GetChildrenDepts(List<SysDept> depts, long deptId)
        {
            return depts.FindAll(delegate (SysDept item)
            {
                long[] pid = Tools.SpitLongArrary(item.Ancestors);
                return pid.Contains(deptId);
            });
        }

        /// <summary>
        /// 构建前端所需要树结构
        /// </summary>
        /// <param name="depts">部门列表</param>
        /// <returns></returns>
        public List<SysDept> BuildDeptTree(List<SysDept> depts)
        {
            List<SysDept> returnList = new List<SysDept>();
            List<long> tempList = depts.Select(f => f.DeptId).ToList();
            foreach (var dept in depts)
            {
                // 如果是顶级节点, 遍历该父节点的所有子节点
                if (!tempList.Contains(dept.ParentId))
                {
                    RecursionFn(depts, dept);
                    returnList.Add(dept);
                }
            }

            if (!returnList.Any())
            {
                returnList = depts;
            }
            return returnList;
        }

        /// <summary>
        /// 构建前端所需下拉树结构
        /// </summary>
        /// <param name="depts"></param>
        /// <returns></returns>
        public List<TreeSelectVo> BuildDeptTreeSelect(List<SysDept> depts)
        {
            List<SysDept> menuTrees = BuildDeptTree(depts);
            List<TreeSelectVo> treeMenuVos = new List<TreeSelectVo>();
            foreach (var item in menuTrees)
            {
                treeMenuVos.Add(new TreeSelectVo(item));
            }
            return treeMenuVos;
        }

        /// <summary>
        /// 递归列表
        /// </summary>
        /// <param name="list"></param>
        /// <param name="t"></param>
        private void RecursionFn(List<SysDept> list, SysDept t)
        {
            //得到子节点列表
            List<SysDept> childList = GetChildList(list, t);
            t.children = childList;
            foreach (var item in childList)
            {
                if (GetChildList(list, item).Count() > 0)
                {
                    RecursionFn(list, item);
                }
            }
        }
        /// <summary>
        /// 递归获取子菜单
        /// </summary>
        /// <param name="list">所有菜单</param>
        /// <param name="dept"></param>
        /// <returns></returns>
        private List<SysDept> GetChildList(List<SysDept> list, SysDept dept)
        {
            return list.Where(p => p.ParentId == dept.DeptId).ToList();
        }

        #region 角色部门

        /// <summary>
        /// 根据角色获取菜单id
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public List<SysRoleDept> SelectRoleDeptByRoleId(long roleId)
        {
            return RoleDeptRepository.SelectRoleDeptByRoleId(roleId);
        }

        /// <summary>
        /// 获取角色部门id集合
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public List<long> SelectRoleDepts(long roleId)
        {
            var list = SelectRoleDeptByRoleId(roleId);

            return list.Select(x => x.DeptId).Distinct().ToList();
        }

        /// <summary>
        /// 删除角色部门数据
        /// </summary>
        /// <param name="roleId"></param>
        /// <returns></returns>
        public bool DeleteRoleDeptByRoleId(long roleId)
        {
            return RoleDeptRepository.Delete(f => f.RoleId == roleId);
        }

        /// <summary>
        /// 批量插入角色部门
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        public int InsertRoleDepts(SysRole role)
        {
            int rows = 1;
            List<SysRoleDept> list = new();
            foreach (var item in role.DeptIds)
            {
                list.Add(new SysRoleDept() { DeptId = item, RoleId = role.RoleId });
            }
            if (list.Count > 0)
            {
                rows = RoleDeptRepository.Insert(list);
            }
            return rows;
        }
        #endregion
    }
}
