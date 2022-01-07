using Infrastructure;
using Microsoft.Extensions.Configuration;
using SqlSugar;
using SqlSugar.IOC;
using System;
using System.Collections.Generic;
using System.Linq;
using ZR.Admin.WebApi.Framework;
using ZR.Model.System;

namespace ZR.Admin.WebApi.Extensions
{
    public static class DbExtension
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public static void AddDb(IConfiguration Configuration)
        {
            string connStr = Configuration.GetConnectionString(OptionsSetting.ConnAdmin);
            string connStrBus = Configuration.GetConnectionString(OptionsSetting.ConnBus);
            string dbKey = Configuration[OptionsSetting.DbKey];
            int dbType = Convert.ToInt32(Configuration[OptionsSetting.ConnDbType]);
            int dbType_bus = Convert.ToInt32(Configuration[OptionsSetting.ConnBusDbType]);

            SugarIocServices.AddSqlSugar(new List<IocConfig>() {
               new IocConfig() {
                ConfigId = "0",
                ConnectionString = connStr,
                DbType = (IocDbType)dbType,
                IsAutoCloseConnection = true//自动释放
            }, new IocConfig() {
                ConfigId = "1",
                ConnectionString = connStrBus,
                DbType = (IocDbType)dbType_bus,
                IsAutoCloseConnection = true//自动释放
            }
            });
            //每次Sql执行前事件
            var db0 = DbScoped.SugarScope.GetConnection(0);
            db0.Aop.OnLogExecuting = (sql, pars) =>
            {
                var param = DbScoped.SugarScope.Utilities.SerializeObject(pars.ToDictionary(it => it.ParameterName, it => it.Value));

                FilterData(db0);

                logger.Info($"{sql}，{param}");
            };
            //出错打印日志
            db0.Aop.OnError = (e) =>
            {
                logger.Error(e, $"执行SQL出错：{e.Message}");
            };
            //SQL执行完
            db0.Aop.OnLogExecuted = (sql, pars) =>
            {
                //执行完了可以输出SQL执行时间 (OnLogExecutedDelegate) 
            };
            //Db1
            DbScoped.SugarScope.GetConnection(1).Aop.OnLogExecuting = (sql, pars) =>
            {
                var param = DbScoped.SugarScope.Utilities.SerializeObject(pars.ToDictionary(it => it.ParameterName, it => it.Value));
                
                logger.Info($"Sql语句：{sql}, {param}");
            };
            //Db1错误日志
            DbScoped.SugarScope.GetConnection(1).Aop.OnError = (e) =>
            {
                logger.Error($"执行Sql语句失败：{e.Sql}，原因：{e.Message}");
            };

        }

        private static void FilterData(SqlSugarProvider db0)
        {
            var u = App.User;
            if (u != null && u.Identity.IsAuthenticated)
            {
                //获取当前用户的信息
                var user = JwtUtil.GetLoginUser(App.HttpContext);
                if (user != null)
                {
                    //非管理员过滤数据权限
                    if (!user.RoleIds.Any(f => f.Equals("admin")))
                    {
                        //TODO 实现范围过滤
                        foreach (var role in user.Roles)
                        {
                            string dataScope = role.DataScope;
                            if ("1".Equals(dataScope))
                            {
                                break;
                            }
                            else if ("2".Equals(dataScope))
                            {
                                //var roleDepts = db0.Queryable<SysRoleDept>()
                                //.Where(f => f.RoleId == role.RoleId).Select(f => f.DeptId).ToList();
                                //var filter1 = new TableFilterItem<SysDept>(it => roleDepts.Contains(it.DeptId));
                            }
                            else if ("3".Equals(dataScope))
                            {
                                var filter1 = new TableFilterItem<SysDept>(it => it.DeptId == user.DeptId);
                            }
                            else if ("4".Equals(dataScope))
                            {

                            }
                            else if ("5".Equals(dataScope))
                            {
                                var filter1 = new TableFilterItem<SysUser>(it => it.UserId == user.UserId);
                            }
                        }
                    }
                }
            }
            //TODO 在此实现数据过滤
            //DbScoped.SugarScope.GetConnection(0).QueryFilter.Add(new TableFilterItem<SysUser>(it => it.DeptId == 333)); //为Order表置全局条件
        }
    }
}
