using Infrastructure;
using Infrastructure.Helper;
using SqlSugar;
using SqlSugar.IOC;
using System.Reflection;
using ZR.Admin.WebApi.Framework;
using ZR.Model.System;

namespace ZR.Admin.WebApi.Extensions
{
    public static class DbExtension
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        //全部数据权限
        public static long DATA_SCOPE_ALL = 1;
        //自定数据权限
        public static long DATA_SCOPE_CUSTOM = 2;
        //部门数据权限
        public static long DATA_SCOPE_DEPT = 3;
        //部门及以下数据权限
        public static long DATA_SCOPE_DEPT_AND_CHILD = 4;
        //仅本人数据权限
        public static long DATA_SCOPE_SELF = 5;

        /// <summary>
        /// 初始化db
        /// </summary>
        /// <param name="Configuration"></param>
        public static void AddDb(this IServiceCollection services, IConfiguration Configuration)
        {
            List<DbConfigs> dbConfigs = Configuration.GetSection("DbConfigs").Get<List<DbConfigs>>();

            var iocList = new List<IocConfig>();
            foreach (var item in dbConfigs)
            {
                iocList.Add(new IocConfig()
                {
                    ConfigId = item.ConfigId,
                    ConnectionString = item.Conn,
                    DbType = (IocDbType)item.DbType,
                    IsAutoCloseConnection = item.IsAutoCloseConnection
                });
            }
            SugarIocServices.AddSqlSugar(iocList);
            ICacheService cache = new SqlSugarCache();
            SugarIocServices.ConfigurationSugar(db =>
            {
                var u = App.User;
                if (u != null)
                {
                    FilterData(0);
                    //ConfigId = 1的数据权限过滤
                    //FilterData1(1);
                }

                iocList.ForEach(iocConfig =>
                {
                    SetSugarAop(db, iocConfig, cache);
                });
            });
        }

        /// <summary>
        /// 数据库Aop设置
        /// </summary>
        /// <param name="db"></param>
        /// <param name="iocConfig"></param>
        /// <param name="cache"></param>
        private static void SetSugarAop(SqlSugarClient db, IocConfig iocConfig, ICacheService cache)
        {
            var config = db.GetConnection(iocConfig.ConfigId).CurrentConnectionConfig;

            string configId = config.ConfigId;
            db.GetConnectionScope(configId).Aop.OnLogExecuting = (sql, pars) =>
            {
                string log = $"【db{configId} SQL语句】{UtilMethods.GetSqlString(config.DbType, sql, pars)}\n";
                if (sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                {
                    logger.Info(log);
                }
                else if (sql.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) || sql.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
                {
                    logger.Warn(log);
                }
                else if (sql.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) || sql.StartsWith("TRUNCATE", StringComparison.OrdinalIgnoreCase))
                {
                    logger.Error(log);
                }
                else
                {
                    log = $"【db{configId} SQL语句】dbo.{sql} {string.Join(", ", pars.Select(x => x.ParameterName + " = " + GetParsValue(x)))};\n";
                    logger.Info(log);
                }
            };

            db.GetConnectionScope(configId).Aop.OnError = (ex) =>
            {
                var pars = db.Utilities.SerializeObject(((SugarParameter[])ex.Parametres).ToDictionary(it => it.ParameterName, it => it.Value));

                string sql = "【错误SQL】" + UtilMethods.GetSqlString(config.DbType, ex.Sql, (SugarParameter[])ex.Parametres) + "\r\n";
                logger.Error(ex, $"{sql}\r\n{ex.Message}\r\n{ex.StackTrace}");
            };

            db.GetConnectionScope(configId).CurrentConnectionConfig.MoreSettings = new ConnMoreSettings()
            {
                IsAutoRemoveDataCache = true
            };
            db.GetConnectionScope(configId).CurrentConnectionConfig.ConfigureExternalServices = new ConfigureExternalServices()
            {
                DataInfoCacheService = cache,
                EntityService = (c, p) =>
                {
                    if (db.GetConnectionScope(configId).CurrentConnectionConfig.DbType == DbType.PostgreSQL && p.DataType != null && p.DataType.Contains("nvarchar"))
                    {
                        p.DataType = "text";
                    }
                }
            };
        }

        /// <summary>
        /// 创建db、表
        /// </summary>
        /// <param name="service"></param>
        public static void InitDb(this IServiceProvider service)
        {
            var db = DbScoped.SugarScope;
            //建库：如果不存在创建数据库存在不会重复创建 
            db.DbMaintenance.CreateDatabase();// 注意 ：Oracle和个别国产库需不支持该方法，需要手动建库 

            var baseType = typeof(SysBase);
            var entityes = AssemblyUtils.GetAllTypes().Where(p => !p.IsAbstract && p != baseType && /*p.IsAssignableTo(baseType) && */p.GetCustomAttribute<SugarTable>() != null).ToArray();
            db.CodeFirst.SetStringDefaultLength(200).InitTables(entityes);
        }

        private static object GetParsValue(SugarParameter x)
        {
            if (x.DbType == System.Data.DbType.String || x.DbType == System.Data.DbType.DateTime || x.DbType == System.Data.DbType.String)
            {
                return "'" + x.Value + "'";
            }
            return x.Value;
        }

        /// <summary>
        /// 数据过滤
        /// </summary>
        /// <param name="configId">多库id</param>
        private static void FilterData(int configId)
        {
            //获取当前用户的信息
            var user = JwtUtil.GetLoginUser(App.HttpContext);
            if (user == null) return;
            //管理员不过滤
            if (user.RoleIds.Any(f => f.Equals(GlobalConstant.AdminRole))) return;
            var db = DbScoped.SugarScope.GetConnectionScope(configId);
            var expUser = Expressionable.Create<SysUser>().Or(it => 1 == 1);
            var expRole = Expressionable.Create<SysRole>().Or(it => 1 == 1);
            var expLoginlog = Expressionable.Create<SysLogininfor>();

            foreach (var role in user.Roles.OrderBy(f => f.DataScope))
            {
                long dataScope = role.DataScope;
                if (DATA_SCOPE_ALL.Equals(dataScope))//所有权限
                {
                    break;
                }
                else if (DATA_SCOPE_CUSTOM.Equals(dataScope))//自定数据权限
                {
                    //" OR {}.dept_id IN ( SELECT dept_id FROM sys_role_dept WHERE role_id = {} ) ", deptAlias, role.getRoleId()));

                    expUser.Or(it => SqlFunc.Subqueryable<SysRoleDept>().Where(f => f.DeptId == it.DeptId && f.RoleId == role.RoleId).Any());
                }
                else if (DATA_SCOPE_DEPT.Equals(dataScope))//本部门数据
                {
                    expUser.Or(it => it.DeptId == user.DeptId);
                }
                else if (DATA_SCOPE_DEPT_AND_CHILD.Equals(dataScope))//本部门及以下数据
                {
                    //SQl  OR {}.dept_id IN ( SELECT dept_id FROM sys_dept WHERE dept_id = {} or find_in_set( {} , ancestors ) )
                    var allChildDepts = db.Queryable<SysDept>().ToChildList(it => it.ParentId, user.DeptId);

                    expUser.Or(it => allChildDepts.Select(f => f.DeptId).ToList().Contains(it.DeptId));
                }
                else if (DATA_SCOPE_SELF.Equals(dataScope))//仅本人数据
                {
                    expUser.Or(it => it.UserId == user.UserId);
                    expRole.Or(it => user.RoleIds.Contains(it.RoleKey));
                    expLoginlog.And(it => it.UserName == user.UserName);
                }
                db.QueryFilter.AddTableFilter(expUser.ToExpression());
                db.QueryFilter.AddTableFilter(expRole.ToExpression());
                db.QueryFilter.AddTableFilter(expLoginlog.ToExpression());
            }
        }

        private static void FilterData1(int configId)
        {
            //获取当前用户的信息
            var user = JwtUtil.GetLoginUser(App.HttpContext);
            if (user == null) return;
            var db = DbScoped.SugarScope.GetConnectionScope(configId);

            foreach (var role in user.Roles.OrderBy(f => f.DataScope))
            {
                long dataScope = role.DataScope;
                if (DATA_SCOPE_ALL.Equals(dataScope))//所有权限
                {
                    break;
                }
                else if (DATA_SCOPE_CUSTOM.Equals(dataScope))//自定数据权限
                {
                }
                else if (DATA_SCOPE_DEPT.Equals(dataScope))//本部门数据
                {
                }
                else if (DATA_SCOPE_DEPT_AND_CHILD.Equals(dataScope))//本部门及以下数据
                {

                }
                else if (DATA_SCOPE_SELF.Equals(dataScope))//仅本人数据
                {
                }
            }
        }
    }
}
