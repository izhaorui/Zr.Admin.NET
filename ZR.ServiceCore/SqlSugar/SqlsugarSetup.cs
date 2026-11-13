using Infrastructure;
using Infrastructure.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar.IOC;
using ZR.Common;
using ZR.Model.AI;
using ZR.Model.Content;
using ZR.Model.Models;
using ZR.Model.System;
using ZR.Repository;

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
        /// <param name="checkMigration">
        /// 是否执行存量库幂等迁移（补多租户列 / 补业务表）。默认 true。
        /// 多实例部署时迁移属单实例动作：公网宿主传 false，由内网宿主（或 --initdb）负责，
        /// 避免多副本并发执行 DDL。传 false 的前提是内网宿主已完成过一次迁移。
        /// </param>
        public static void AddDb(this IServiceCollection services, IWebHostEnvironment environment, bool checkMigration = true)
        {
            WarnLegacyDbConfig();

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
                iocList.ForEach(iocConfig =>
                {
                    // 每个连接包一层带作用域的缓存：SqlSugar 的缓存 key 不含连接/租户标识，
                    // 不区分会让不同租户库、主库共享表按 TenantId 过滤的查询互相读到对方数据
                    SetSugarAop(db, iocConfig, new TenantScopedCache(iocConfig.ConfigId, cache));

                    var conn = db.GetConnectionScope(iocConfig.ConfigId);

                    // 租户隔离（主库共享实体）：仅主库连接且 SaaS 模式下注册，清单以 TenantFilter.Apply 为准
                    if (iocConfig.ConfigId == App.MainDbConfigId && App.IsTenantEnabled())
                    {
                        TenantFilter.Apply(conn);
                    }
                });
            });

            // 注册 BaseRepository 自动附加的数据权限过滤器（替代已移除的全局 AddTableFilter）
            DataScopeExtensions.RegisterScopeFilter<SysUser>(q => q.ApplyScope());
            DataScopeExtensions.RegisterScopeFilter<SysDept>(q => q.ApplyScope());
            DataScopeExtensions.RegisterScopeFilter<SysRole>(q => q.ApplyScope());
            DataScopeExtensions.RegisterScopeFilter<SysLogininfor>(q => q.ApplyScope());
            DataScopeExtensions.RegisterScopeFilter<UserOnlineLog>(q => q.ApplyScope());

            if (checkMigration)
            {
                // 先应用历史表名重命名（主库、幂等）：实体 [SugarTable] 改名后，存量库要先改名，
                // 否则下面的补列/补表会按新名建出空表、旧表残留。见 DbMigrationService.LegacyTableRenames。
                DbMigrationService.ApplyLegacyTableRenames();

                // 存量库迁移：补加多租户隔离列 TenantId（幂等，避免过滤器运行时报"未知列"）
                // 实体来源与 DbMigrationService.Migrate 共用显式注册表；新装库由 CodeFirst 自动建列。
                DbMigrationService.MigrateTenantColumns();
                DbMigrationService.EnsureTenantBusinessTables();
            }

            // 注意：全量"迁移 + 种子数据"已不再耦合进 Web 启动主链路，改由 CLI 参数 --initdb 显式触发
            // （见 Program.cs 的 --initdb 分支），避免每次启动被阻塞/拖慢。此处仅保留幂等补列。
        }

        /// <summary>
        /// 遗留库配置项兼容提示（不阻断启动）。
        /// 单库部署不需要配置 SystemDb / BizMainDb / MallDb，未配置时分别回退 "0" / "0" / "0"。
        /// </summary>
        private static void WarnLegacyDbConfig()
        {
            var configuration = App.Configuration;

            var legacySystemDb = configuration?["SystemDb"];
            if (!string.IsNullOrWhiteSpace(legacySystemDb)
                && !string.Equals(legacySystemDb.Trim(), App.SystemDbConfigId, StringComparison.OrdinalIgnoreCase))
            {
                Log.WriteLine(ConsoleColor.Yellow,
                    $"[多租户] 配置项 SystemDb={legacySystemDb} 已废弃并被忽略：系统库 ConfigId 固定为 \"{App.SystemDbConfigId}\"。"
                    + $"请删除该项，并把原系统库连接的 ConfigId 调整为 \"{App.SystemDbConfigId}\"。");
            }

            var legacyMainDb = configuration?["MainDb"];
            if (!string.IsNullOrWhiteSpace(legacyMainDb)
                && !string.Equals(legacyMainDb.Trim(), App.SystemDbConfigId, StringComparison.OrdinalIgnoreCase))
            {
                Log.WriteLine(ConsoleColor.Yellow,
                    $"[多租户] 配置项 MainDb={legacyMainDb} 已更名为 BizMainDb（旧键继续兼容）。"
                    + "单库部署请直接删除该项，未配置时回退 \"0\"。");
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
                if (!showDbLog) return;

                var cmd = sql.TrimStart();
                string log = $"【config={configId}】{UtilMethods.GetSqlString(config.DbType, sql, pars)}\n";
                if (cmd.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
                    logger.Info(log);
                else if (cmd.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) ||
                         cmd.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
                    logger.Info(log);
                else if (cmd.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase) ||
                         cmd.StartsWith("TRUNCATE", StringComparison.OrdinalIgnoreCase))
                    logger.Warn(log);
                else
                    logger.Info(log);

            };
            db.GetConnectionScope(configId).Aop.OnError = (ex) =>
            {
                //var pars = db.Utilities.SerializeObject(((SugarParameter[])ex.Parametres).ToDictionary(it => it.ParameterName, it => it.Value));

                string sql = $"【错误SQL,db={configId}】{UtilMethods.GetSqlString(config.DbType, ex.Sql, (SugarParameter[])ex.Parametres)}\r\n";
                logger.Error(ex, $"{sql}\r\n{ex.Message}\r\n{ex.StackTrace}");
            };
            db.GetConnectionScope(configId).Aop.DataExecuting = (oldValue, entityInfo) =>
            {
                WarnMissingTenantIdOnInsert(oldValue, entityInfo);
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
                    db.GetConnectionScopeWithAttr<SqlDiffLog>()
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
                    #region 兼容sqllite
                    if (config.DbType == DbType.Sqlite)
                    {
                        if (p.IsPrimarykey && c.PropertyType == typeof(long))
                        {
                            p.DataType = "INTEGER";
                        }
                    }
                    #endregion

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

                    #region Unicode 文本：实体声明 nvarchar(...)，其它数据库按类型适配
                    // SQL Server 下只有 nvarchar 才能正确承载中文正文与 ⚠️ 等 emoji（varchar 会毁成问号），
                    // 故实体上直接写 nvarchar 表达"需要 Unicode 文本"的意图；其它库的差异在这里按 DbType 适配
                    // （SqlSugar 官方推荐的多库类型差异处理方式：用实体 AOP）：
                    //   nvarchar(max) → MySQL longtext / PgSQL·SQLite text / Oracle clob；
                    //   nvarchar(n)  → Oracle 换 nvarchar2(n)（Oracle 无 nvarchar），MySQL/PgSQL/SQLite 原生支持保留。
                    // 泛化处理所有声明 nvarchar(...) 的列（原为 Article.Content 专用，现含 SysUserMsg.Content 等），
                    // 新增此类列无需再改这里。
                    var declaredType = p.DataType?.Trim();
                    if (declaredType != null && declaredType.StartsWith("nvarchar(", StringComparison.OrdinalIgnoreCase))
                    {
                        var isMax = declaredType.Equals("nvarchar(max)", StringComparison.OrdinalIgnoreCase);
                        p.DataType = config.DbType switch
                        {
                            DbType.MySql => isMax ? "longtext" : declaredType,
                            DbType.PostgreSQL => isMax ? "text" : declaredType,
                            DbType.Sqlite => isMax ? "text" : declaredType,
                            DbType.Oracle => isMax ? "clob" : declaredType.Replace("nvarchar(", "nvarchar2("),
                            _ => declaredType
                        };
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

        /// <summary>
        /// 主库共享表 INSERT 未带 TenantId 时只打警告，不自动补齐。
        /// 自动填会掩盖调用方漏赋值；平台表与 AiAccessPolicy（空字符串表示全局策略）不检查。
        /// </summary>
        private static void WarnMissingTenantIdOnInsert(object oldValue, DataFilterModel entityInfo)
        {
            if (!App.IsTenantEnabled()) return;
            if (entityInfo.OperationType != DataFilterType.InsertByObject) return;
            if (!string.Equals(entityInfo.PropertyName, "TenantId", StringComparison.Ordinal)) return;
            if (oldValue is string existing && !string.IsNullOrWhiteSpace(existing)) return;

            var entityType = entityInfo.EntityValue?.GetType();
            if (entityType == null) return;
            if (entityType.Name == nameof(AiAccessPolicy)) return;
            if (Array.IndexOf(TenantFilter.FilteredEntityTypes, entityType) < 0) return;

            logger.Warn("主库共享表 INSERT 未设置 TenantId: entity={Entity}, currentTenant={TenantId}",
                entityType.Name, App.GetCurrentTenantId());
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
