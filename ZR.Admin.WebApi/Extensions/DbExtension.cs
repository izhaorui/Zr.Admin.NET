using Infrastructure;
using Infrastructure.Extensions;
using SqlSugar;
using SqlSugar.IOC;
using ZR.Model;
using ZR.Model.System;

namespace ZR.Admin.WebApi.Extensions
{
    /// <summary>
    /// sqlsugar 数据处理
    /// </summary>
    public static class DbExtension
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// 初始化db
        /// </summary>
        /// <param name="services"></param>
        /// <param name="Configuration"></param>
        /// <param name="environment"></param>
        public static void AddDb(this IServiceCollection services, IConfiguration Configuration, IWebHostEnvironment environment)
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
                    DataPermi.FilterData(0);
                    //ConfigId = 1的数据权限过滤
                    //DataPermi.FilterData1(1);
                }

                iocList.ForEach(iocConfig =>
                {
                    SetSugarAop(db, iocConfig, cache);
                });
            });

            if (Configuration["InitDb"].ParseToBool() == true && environment.IsDevelopment())
            {
                InitTable.InitDb();
            }
        }

        /// <summary>
        /// 数据库Aop设置
        /// </summary>
        /// <param name="db"></param>
        /// <param name="iocConfig"></param>
        /// <param name="cache"></param>
        private static void SetSugarAop(SqlSugarClient db, IocConfig iocConfig, ICacheService cache)
        {
            var config = db.GetConnectionScope(iocConfig.ConfigId).CurrentConnectionConfig;

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
            db.GetConnectionScope(configId).Aop.DataExecuting = (oldValue, entiyInfo) =>
            {
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
                    if (p.IsPrimarykey == true)//主键不能为null
                    {
                        p.IsNullable = false;
                    }
                    else if (p.ExtendedAttribute?.ToString() == ProteryConstant.NOTNULL.ToString())
                    {
                        p.IsNullable = false;
                    }
                    else//则否默认为null
                    {
                        p.IsNullable = true;
                    }

                    if (config.DbType == DbType.PostgreSQL)
                    {
                        if (c.Name == nameof(SysMenu.IsCache) || c.Name == nameof(SysMenu.IsFrame))
                        {
                            p.DataType = "char(1)";
                        }
                    }
                    #region 兼容Oracle
                    if (config.DbType == DbType.Oracle)
                    {
                        if (p.IsIdentity == true)
                        {
                            if (p.EntityName == nameof(SysUser))
                            {
                                p.OracleSequenceName = "SEQ_SYS_USER_USERID";
                            }
                            else if (p.EntityName == nameof(SysRole))
                            {
                                p.OracleSequenceName = "SEQ_SYS_ROLE_ROLEID";
                            }
                            else if (p.EntityName == nameof(SysDept))
                            {
                                p.OracleSequenceName = "SEQ_SYS_DEPT_DEPTID";
                            }
                            else if (p.EntityName == nameof(SysMenu))
                            {
                                p.OracleSequenceName = "SEQ_SYS_MENU_MENUID";
                            }
                            else
                            {
                                p.OracleSequenceName = "SEQ_ID";
                            }
                        }
                    }
                    #endregion
                }
            };
        }

        private static object GetParsValue(SugarParameter x)
        {
            if (x.DbType == System.Data.DbType.String || x.DbType == System.Data.DbType.DateTime || x.DbType == System.Data.DbType.String)
            {
                return "'" + x.Value + "'";
            }
            return x.Value;
        }
    }
}
