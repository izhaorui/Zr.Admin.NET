using Infrastructure;
using SqlSugar.IOC;
using ZR.ServiceCore.SqlSugar;

namespace ZR.ServiceCore
{
    public class DataPermiSevice
    {
        /// <summary>
        /// 你的业务库数据权限过滤方法
        /// </summary>
        /// <param name="configId"></param>
        public static void FilterData(int configId)
        {
            //获取当前用户的信息
            var user = JwtUtil.GetLoginUser(App.HttpContext);
            if (user == null) return;
            var db = DbScoped.SugarScope.GetConnectionScope(configId);

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
