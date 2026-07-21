using Infrastructure;
using SqlSugar.IOC;
using ZR.Model;
using ZR.Model.Models;
using ZR.Model.System;
using ZR.Model.System.Model;

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
        [Obsolete("数据权限过滤已迁移至 ZR.Repository.DataScopeExtensions，请勿在新代码中使用；获取当前用户请走 HttpContext.GetCurrentUser()")]
        public static void FilterData(string configId)
        {
            //获取当前用户的信息
            var user = App.HttpContext.GetCurrentUser();
            if (user == null || user.RoleKeys == null) return;

            var db = DbScoped.SugarScope.GetConnectionScope(configId);

            //管理员不过滤
            if (user.RoleKeys.Any(f => f.Equals(GlobalConstant.AdminRole))) return;

            foreach (var role in user.Roles.OrderBy(f => f.DataScope))
            {
                var dataScope = (DataPermiEnum)role.DataScope;
                if (DataPermiEnum.All.Equals(dataScope))//所有权限
                {
                    break;
                }
                else if (DataPermiEnum.CUSTOM.Equals(dataScope))//自定数据权限
                {
                }
                else if (DataPermiEnum.DEPT.Equals(dataScope))//本部门数据
                {
                }
                else if (DataPermiEnum.DEPT_CHILD.Equals(dataScope))//本部门及以下数据
                {

                }
                else if (DataPermiEnum.SELF.Equals(dataScope))//仅本人数据
                {
                }
            }
        }
    }
}
