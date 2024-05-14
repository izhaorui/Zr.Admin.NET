using Infrastructure;
using SqlSugar.IOC;
using ZR.Model;
using ZR.Model.Models;
using ZR.Model.System;

namespace ZR.ServiceCore.SqlSugar
{
    public enum DataPermiEnum
    {
        None = 0,
        /// <summary>
        /// 全部数据权限
        /// </summary>
        All = 1,
        /// <summary>
        /// 仅本人数据权限
        /// </summary>
        SELF = 5,
        /// <summary>
        /// 部门数据权限
        /// </summary>
        DEPT = 3,
        /// <summary>
        /// 自定数据权限
        /// </summary>
        CUSTOM = 2,
        /// <summary>
        /// 部门及以下数据权限
        /// </summary>
        DEPT_CHILD = 4
    }
    /// <summary>
    /// 数据权限
    /// </summary>
    public class DataPermi
    {
        /// <summary>
        /// 数据过滤
        /// </summary>
        /// <param name="configId">多库id</param>
        public static void FilterData(int configId)
        {
            //获取当前用户的信息
            var user = JwtUtil.GetLoginUser(App.HttpContext);
            if (user == null) return;
            
            var db = DbScoped.SugarScope.GetConnectionScope(configId);
            var expUser = Expressionable.Create<SysUser>().And(it => it.DelFlag == 0);
            var expRole = Expressionable.Create<SysRole>();
            var expLoginlog = Expressionable.Create<SysLogininfor>();
            var expSysMsg = Expressionable.Create<SysUserMsg>().And(it => it.IsDelete == 0);
            
            db.QueryFilter.AddTableFilter(expSysMsg.ToExpression());
            //管理员不过滤
            if (user.RoleIds.Any(f => f.Equals(GlobalConstant.AdminRole))) return;

            foreach (var role in user.Roles.OrderBy(f => f.DataScope))
            {
                var dataScope = (DataPermiEnum)role.DataScope;
                if (DataPermiEnum.All.Equals(dataScope))//所有权限
                {
                    break;
                }
                else if (DataPermiEnum.CUSTOM.Equals(dataScope))//自定数据权限
                {
                    //" OR {}.dept_id IN ( SELECT dept_id FROM sys_role_dept WHERE role_id = {} ) ", deptAlias, role.getRoleId()));

                    expUser.Or(it => SqlFunc.Subqueryable<SysRoleDept>().Where(f => f.DeptId == it.DeptId && f.RoleId == role.RoleId).Any());
                }
                else if (DataPermiEnum.DEPT.Equals(dataScope))//本部门数据
                {
                    expUser.Or(it => it.DeptId == user.DeptId);
                }
                else if (DataPermiEnum.DEPT_CHILD.Equals(dataScope))//本部门及以下数据
                {
                    //SQl  OR {}.dept_id IN ( SELECT dept_id FROM sys_dept WHERE dept_id = {} or find_in_set( {} , ancestors ) )
                    var allChildDepts = db.Queryable<SysDept>().ToChildList(it => it.ParentId, user.DeptId);

                    expUser.Or(it => allChildDepts.Select(f => f.DeptId).ToList().Contains(it.DeptId));
                }
                else if (DataPermiEnum.SELF.Equals(dataScope))//仅本人数据
                {
                    expUser.Or(it => it.UserId == user.UserId);
                    expRole.Or(it => user.RoleIds.Contains(it.RoleKey));
                    expLoginlog.And(it => it.UserName == user.UserName);
                }
            }

            db.QueryFilter.AddTableFilter(expUser.ToExpression());
            db.QueryFilter.AddTableFilter(expRole.ToExpression());
            db.QueryFilter.AddTableFilter(expLoginlog.ToExpression());
            db.QueryFilter.AddTableFilter<UserOnlineLog>(f => f.UserId == user.UserId, QueryFilterProvider.FilterJoinPosition.Where);
        }
    }
}
