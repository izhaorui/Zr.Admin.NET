using Infrastructure;
using Infrastructure.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SqlSugar.IOC;
using ZR.Common;
using ZR.Model.System;

namespace ZR.ServiceCore.SqlSugar
{
    public static class SqlsugarSetup
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 初始化db
        /// </summary>
        /// <param name="services"></param>
        /// <param name="environment"></param>
        public static void AddDb(this IServiceCollection services, IWebHostEnvironment environment)
        {
            var options = App.OptionsSetting;
            List<DbConfigs> dbConfigs = options.DbConfigs;

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
            ICacheService cache;
            if (options.RedisServer.DbCache)
            {
                cache = new SqlSugarRedisCache();
            }
            else
            {
                cache = new SqlSugarCache();
            }
            SugarIocServices.ConfigurationSugar(db =>
            {
                var u = App.User;
                if (u != null)
                {
                    DataPermi.FilterData(0);
                    //ConfigId = 1的数据权限过滤
                    //DataPermiSevice.FilterData(1);
                }

                iocList.ForEach(iocConfig =>
                {
                    SetSugarAop(db, iocConfig, cache);
                });
            });

            if (environment.IsDevelopment())
            {
                InitTable.InitDb(options.InitDb);

                InitTable.InitNewTb();
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
            var showDbLog = AppSettings.Get<bool>("ShowDbLog");
            string configId = config.ConfigId;
            db.GetConnectionScope(configId).Aop.OnLogExecuting = (sql, pars) =>
            {
                if (showDbLog)
                {
                    string log = $"【db{configId} SQL】{UtilMethods.GetSqlString(config.DbType, sql, pars)}\n";
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
                }
            };
            db.GetConnectionScope(configId).Aop.OnError = (ex) =>
            {
                //var pars = db.Utilities.SerializeObject(((SugarParameter[])ex.Parametres).ToDictionary(it => it.ParameterName, it => it.Value));

                string sql = "【错误SQL】" + UtilMethods.GetSqlString(config.DbType, ex.Sql, (SugarParameter[])ex.Parametres) + "\r\n";
                logger.Error(ex, $"{sql}\r\n{ex.Message}\r\n{ex.StackTrace}");
            };
            db.GetConnectionScope(configId).Aop.DataExecuting = (oldValue, entiyInfo) =>
            {
            };
            //差异日志功能
            db.GetConnectionScope(configId).Aop.OnDiffLogEvent = it =>
            {
                //操作前记录  包含： 字段描述 列名 值 表名 表描述
                var editBeforeData = it.BeforeData;//插入Before为null，之前还没进库
                //操作后记录   包含： 字段描述 列名 值  表名 表描述
                var editAfterData = it.AfterData;
                var sql = it.Sql;
                var parameter = it.Parameters;
                var data = it.BusinessData;//这边会显示你传进来的对象
                var time = it.Time;
                var diffType = it.DiffType;//enum insert 、update and delete  
                string name = App.UserName;

                foreach (var item in editBeforeData)
                {
                    var pars = db.Utilities.SerializeObject(item.Columns.ToDictionary(it => it.ColumnName, it => it.Value));

                    SqlDiffLog log = new()
                    {
                        BeforeData = pars,
                        BusinessData = data?.ToString(),
                        DiffType = diffType.ToString(),
                        Sql = sql,
                        TableName = item.TableName,
                        UserName = name,
                        AddTime = DateTime.Now,
                        ConfigId = configId
                    };
                    if (editAfterData != null)
                    {
                        var afterData = editAfterData?.First(x => x.TableName == item.TableName);
                        var afterPars = db.Utilities.SerializeObject(afterData?.Columns.ToDictionary(it => it.ColumnName, it => it.Value));
                        log.AfterData = afterPars;
                    }
                    //logger.WithProperty("title", data).Info(pars);
                    db.GetConnectionScope(0)
                    .Insertable(log)
                    .ExecuteReturnSnowflakeId();
                }
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
            db.GetConnectionScope(configId).Aop.OnLogExecuted = (sql, pars) =>
            {
                var sqlExecutionTime = AppSettings.Get<int>("sqlExecutionTime");
                if (db.Ado.SqlExecutionTime.TotalSeconds > sqlExecutionTime)
                {
                    //代码CS文件名
                    var fileName = db.Ado.SqlStackTrace.FirstFileName;
                    //代码行数
                    var fileLine = db.Ado.SqlStackTrace.FirstLine;
                    //方法名
                    var FirstMethodName = db.Ado.SqlStackTrace.FirstMethodName;
                    var logInfo = $"Sql执行超时，用时{db.Ado.SqlExecutionTime.TotalSeconds}秒【{sql}】,fileName={fileName},line={fileLine},methodName={FirstMethodName}";
                    WxNoticeHelper.SendMsg("Sql请求时间过长", logInfo);
                    logger.Warn(logInfo);
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
