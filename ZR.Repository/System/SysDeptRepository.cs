using Infrastructure.Attribute;
using System.Collections.Generic;
using ZR.Model.System;

namespace ZR.Repository.System
{
    /// <summary>
    /// 部门管理
    /// </summary>
    [AppService(ServiceLifetime=  LifeTime.Transient)]
    public class SysDeptRepository : BaseRepository
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="deptId"></param>
        /// <returns></returns>
        public List<SysDept> SelectChildrenDeptById(long deptId)
        {
            string sql = "select * from sys_dept where find_in_set(@deptId, ancestors)";

            return Db.SqlQueryable<SysDept>(sql).AddParameters(new { @deptId = deptId }).ToList();
        }

        public int UdateDeptChildren(SysDept dept)
        {
            return Db.Updateable(dept).UpdateColumns(it => new { it.Ancestors }).ExecuteCommand();
        }
    }
}
