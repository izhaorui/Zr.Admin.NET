using Infrastructure;
using Infrastructure.Extensions;
using Mapster;
using SqlSugar;
using SqlSugar.IOC;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using ZR.Model;

namespace ZR.Repository
{
    /// <summary>
    /// 数据仓库类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BaseRepository<T> : SimpleClient<T> where T : class, new()
    {
        /// <summary>
        /// [Tenant] 特性中标记业务主库的 key（解析为 App.BizMainDbConfigId）
        /// </summary>
        public const string BizMainDbKey = "MainDb";

        public ITenant itenant = null;//多租户事务
        public BaseRepository(ISqlSugarClient context = null) : base(context)
        {
            // 多租户：IMainDbEntity 走系统库；[Tenant("MainDb")] 走业务主库；其余走当前租户库
            if (App.IsTenantEnabled())
            {
                if (typeof(IMainDbEntity).IsAssignableFrom(typeof(T)))
                {
                    Context = DbScoped.SugarScope.GetConnectionScope(App.SystemDbConfigId); // 系统库
                }
                else if (IsBizMainDbKey(typeof(T).GetCustomAttribute<TenantAttribute>()?.configId))
                {
                    Context = DbScoped.SugarScope.GetConnectionScope(App.BizMainDbConfigId); // 业务主库
                }
                else
                {
                    var tenantId = App.GetCurrentTenantId();
                    try
                    {
                        //Console.WriteLine($"当前用户租户={tenantId}");
                        Context = DbScoped.SugarScope.GetConnectionScope(tenantId);//根据类传入的ConfigId自动选择
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"无效的租户ID: {tenantId}", ex);
                    }
                }
            }
            else
            {
                //通过 [Tenant] 特性拿到 ConfigId，支持配置 key 引用
                // [Tenant("MallDb")] → App.MallDbConfigId（未配置 MallDb 时回退系统库 "0"，单库部署免配置）
                // [Tenant("1")]      → App.Configuration["1"] ?? "1"          → "1"（向后兼容）
                var tenantAttr = typeof(T).GetCustomAttribute<TenantAttribute>()?.configId;
                if (tenantAttr != null)
                {
                    Context = DbScoped.SugarScope.GetConnectionScope(ResolveDbConfigId(tenantAttr));
                }
                else
                {
                    Context = context ?? DbScoped.SugarScope.GetConnectionScope(App.SystemDbConfigId);//无标记默认系统库
                }
            }
            //Context = DbScoped.SugarScope.GetConnectionScopeWithAttr<T>();
            itenant = DbScoped.SugarScope;//设置租户接口
        }

        private static bool IsBizMainDbKey(object configId)
        {
            return string.Equals(configId?.ToString(), BizMainDbKey, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 解析 [Tenant] 特性标记的 ConfigId：
        /// MainDb → 业务主库；MallDb → 商城库（未配置时回退系统库 "0"）；
        /// 其他值按配置 key 引用解析，找不到时保持原值，便于暴露明确的配置错误。
        /// </summary>
        private static string ResolveDbConfigId(object configId)
        {
            var key = configId?.ToString();
            if (IsBizMainDbKey(key))
            {
                return App.BizMainDbConfigId;
            }

            if (string.Equals(key, "MallDb", StringComparison.OrdinalIgnoreCase))
            {
                return App.MallDbConfigId;
            }

            return App.Configuration[key] ?? key;
        }

        /// <summary>
        /// 解析租户数据库连接：优先使用传入租户ID，其次使用当前请求租户。
        /// 未启用多租户时回退当前 Context；SaaS 下租户为空或不在 dbConfigs 中直接失败，禁止回退主库。
        /// </summary>
        protected ISqlSugarClient ResolveTenantDb(string tenantId = null)
        {
            if (!App.IsTenantEnabled())
            {
                return Context;
            }

            var targetTenantId = string.IsNullOrWhiteSpace(tenantId)
                ? App.GetCurrentTenantId()
                : tenantId.Trim();

            if (string.IsNullOrWhiteSpace(targetTenantId))
            {
                throw new CustomException("无法解析租户数据库连接：租户标识为空");
            }

            var configs = App.OptionsSetting?.DbConfigs;
            var hasDbConfig = configs != null && configs.Exists(x =>
                string.Equals(x.ConfigId, targetTenantId, StringComparison.OrdinalIgnoreCase));

            if (!hasDbConfig)
            {
                throw new CustomException($"未找到租户[{targetTenantId}]对应的数据库配置，请先在dbConfigs中配置ConfigId");
            }

            return DbScoped.SugarScope.GetConnectionScope(targetTenantId);
        }

        /// <summary>
        /// 解析系统库连接（SystemDb 未配置时默认 "0"）。
        /// </summary>
        protected ISqlSugarClient ResolveMainDb()
        {
            return DbScoped.SugarScope.GetConnectionScope(App.SystemDbConfigId);
        }

        #region add

        /// <summary>
        /// 插入实体
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public int Add(T t, bool ignoreNull = true)
        {
            return Context.Insertable(t).IgnoreColumns(ignoreNullColumn: ignoreNull).ExecuteCommand();
        }

        public int Insert(List<T> t)
        {
            return InsertRange(t) ? 1 : 0;
        }
        public int Insert(T parm, Expression<Func<T, object>> iClumns = null, bool ignoreNull = true)
        {
            return Context.Insertable(parm).InsertColumns(iClumns).IgnoreColumns(ignoreNullColumn: ignoreNull).ExecuteCommand();
        }
        public IInsertable<T> Insertable(T t)
        {
            return Context.Insertable(t);
        }
        #endregion add

        #region update
        //public IUpdateable<T> Updateable(T entity)
        //{
        //    return Context.Updateable(entity);
        //}

        /// <summary>
        /// 实体根据主键更新
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="ignoreNullColumns"></param>
        /// <returns></returns>
        public int Update(T entity, bool ignoreNullColumns = false, object data = null)
        {
            return Context.Updateable(entity).IgnoreColumns(ignoreNullColumns)
                .EnableDiffLogEventIF(data.IsNotEmpty(), data).ExecuteCommand();
        }

        /// <summary>
        /// 实体根据主键更新指定字段
        /// return Update(new SysUser(){ Status = 1 }, t => new { t.NickName, }, true);
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="expression"></param>
        /// <param name="ignoreAllNull"></param>
        /// <returns></returns>
        public int Update(T entity, Expression<Func<T, object>> expression, bool ignoreAllNull = false)
        {
            return Context.Updateable(entity)
                .UpdateColumns(expression)
                .IgnoreColumns(ignoreAllNull)
                .RemoveDataCache()
                .ExecuteCommand();
        }

        /// <summary>
        /// 根据指定条件更新指定列 eg：Update(new SysUser(){ Status = 1 }, it => new { it.Status }, f => f.Userid == 1));
        /// 只更新Status列，条件是包含
        /// </summary>
        /// <param name="entity">实体类</param>
        /// <param name="expression">要更新列的表达式</param>
        /// <param name="where">where表达式</param>
        /// <returns></returns>
        public int Update(T entity, Expression<Func<T, object>> expression, Expression<Func<T, bool>> where)
        {
            return Context.Updateable(entity).UpdateColumns(expression).Where(where).ExecuteCommand();
        }

        /// <summary>
        /// 更新指定列 eg：Update(w => w.NoticeId == model.NoticeId, it => new SysNotice(){ Update_time = DateTime.Now, Title = "通知标题" });
        /// </summary>
        /// <param name="where"></param>
        /// <param name="columns"></param>
        /// <returns></returns>
        public int Update(Expression<Func<T, bool>> where, Expression<Func<T, T>> columns)
        {
            return Context.Updateable<T>().SetColumns(columns).Where(where).RemoveDataCache().ExecuteCommand();
        }
        public async Task<int> UpdateAsync(Expression<Func<T, bool>> where, Expression<Func<T, T>> columns)
        {
            return await Context.Updateable<T>().SetColumns(columns).Where(where).RemoveDataCache().ExecuteCommandAsync();
        }
        #endregion update

        /// <summary>
        /// 使用事务（单库）
        /// </summary>
        public DbResult<bool> UseTran(Action action)
        {
            try
            {
                return Context.Ado.UseTran(() => action());
            }
            catch (Exception ex)
            {
                Context.Ado.RollbackTran();
                Log.WriteLine(ConsoleColor.Red, $"[UseTran] Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 使用事务（多库/多租户）
        /// </summary>
        public DbResult<bool> UseTran(ISqlSugarClient client, Action action)
        {
            try
            {
                return client.AsTenant().UseTran(() => action());
            }
            catch (Exception ex)
            {
                client.AsTenant().RollbackTran();
                Log.WriteLine(ConsoleColor.Red, $"[UseTran] 事务异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 使用事务，返回是否成功
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public bool UseTran2(Action action)
        {
            Console.WriteLine("---事务开始---");
            var result = Context.Ado.UseTran(() => action());
            Console.WriteLine("---事务结束---");
            return result.IsSuccess;
        }

        #region delete
        public IDeleteable<T> Deleteable()
        {
            return Context.Deleteable<T>();
        }

        public int Delete(object id, string title = "")
        {
            return Context.Deleteable<T>(id).EnableDiffLogEventIF(title.IsNotEmpty(), title).ExecuteCommand();
        }
        public int DeleteTable()
        {
            return Context.Deleteable<T>().ExecuteCommand();
        }
        public bool Truncate()
        {
            return Context.DbMaintenance.TruncateTable<T>();
        }
        #endregion delete

        #region query

        public bool Any(Expression<Func<T, bool>> expression)
        {
            return Queryable().Where(expression).Any();
        }

        public ISugarQueryable<T> Queryable()
        {
            var query = Context.Queryable<T>();
            return ApplyScopeIfNeeded(query);
        }

        private static ISugarQueryable<T> ApplyScopeIfNeeded(ISugarQueryable<T> query)
        {
            return DataScopeExtensions.TryApplyScope(query, out var result) ? result : query;
        }

        public override T GetFirst(Expression<Func<T, bool>> whereExpression)
        {
            return Queryable().Where(whereExpression).First();
        }

        public override int Count(Expression<Func<T, bool>> whereExpression)
        {
            return Queryable().Where(whereExpression).Count();
        }

        public override List<T> GetList(Expression<Func<T, bool>> whereExpression)
        {
            return Queryable().Where(whereExpression).ToList();
        }

        public List<T> SqlQueryToList(string sql, object obj = null)
        {
            return Context.Ado.SqlQuery<T>(sql, obj);
        }

        /// <summary>
        /// 根据主值查询单条数据
        /// </summary>
        /// <param name="pkValue">主键值</param>
        /// <returns>泛型实体</returns>
        public T GetId(object pkValue)
        {
            return Queryable().InSingle(pkValue);
        }
        /// <summary>
        /// 根据条件查询分页数据
        /// </summary>
        /// <param name="where"></param>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<T> GetPages(Expression<Func<T, bool>> where, PagerInfo parm)
        {
            var source = Queryable().Where(where);

            return source.ToPage(parm);
        }

        /// <summary>
        /// 分页获取数据
        /// </summary>
        /// <param name="where">条件表达式</param>
        /// <param name="parm"></param>
        /// <param name="order"></param>
        /// <param name="orderEnum"></param>
        /// <returns></returns>
        public PagedInfo<T> GetPages(Expression<Func<T, bool>> where, PagerInfo parm, Expression<Func<T, object>> order, OrderByType orderEnum = OrderByType.Asc)
        {
            var source = Queryable()
                .Where(where)
                .OrderByIF(orderEnum == OrderByType.Asc, order, OrderByType.Asc)
                .OrderByIF(orderEnum == OrderByType.Desc, order, OrderByType.Desc);

            return source.ToPage(parm);
        }

        public PagedInfo<T> GetPages(Expression<Func<T, bool>> where, PagerInfo parm, Expression<Func<T, object>> order, string orderByType)
        {
            return GetPages(where, parm, order, orderByType == "desc" ? OrderByType.Desc : OrderByType.Asc);
        }

        /// <summary>
        /// 查询所有数据(无分页,请慎用)
        /// </summary>
        /// <returns></returns>
        public List<T> GetAll(bool useCache = false, int cacheSecond = 3600)
        {
            return Queryable().WithCacheIF(useCache, cacheSecond).ToList();
        }

        #endregion query

        /// <summary>
        /// 此方法不带output返回值
        /// var list = new List<SugarParameter>();
        /// list.Add(new SugarParameter(ParaName, ParaValue)); input
        /// </summary>
        /// <param name="procedureName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public DataTable UseStoredProcedureToDataTable(string procedureName, List<SugarParameter> parameters)
        {
            return Context.Ado.UseStoredProcedure().GetDataTable(procedureName, parameters);
        }

        /// <summary>
        /// 带output返回值
        /// var list = new List<SugarParameter>();
        /// list.Add(new SugarParameter(ParaName, ParaValue, true));  output
        /// list.Add(new SugarParameter(ParaName, ParaValue)); input
        /// </summary>
        /// <param name="procedureName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public (DataTable, List<SugarParameter>) UseStoredProcedureToTuple(string procedureName, List<SugarParameter> parameters)
        {
            var result = (Context.Ado.UseStoredProcedure().GetDataTable(procedureName, parameters), parameters);
            return result;
        }
    }

    /// <summary>
    /// 后台/fire-and-forget 任务数据库连接辅助。
    /// 约定：后台线程禁止复用请求级 scoped Context（请求结束后连接释放、租户上下文丢失，
    /// 与主事务共用同一连接会报 "This MySqlConnection is already in use"）。
    /// 用法：请求线程内 <see cref="CaptureTenantId"/> 捕获租户 Id，后台任务内 <see cref="CreateBackgroundDb"/> 建独立连接。
    /// </summary>
    public static class BackgroundDbHelper
    {
        /// <summary>
        /// 请求上下文内捕获当前租户 Id（未启用多租户返回 null）。
        /// 必须在请求线程内调用，结果作为闭包传给后台任务。
        /// </summary>
        public static string CaptureTenantId()
        {
            return App.IsTenantEnabled() ? App.GetCurrentTenantId() : null;
        }

        /// <summary>
        /// 后台线程内创建独立数据库连接（CopyNew），多租户时按捕获的租户 Id 路由到对应租户库。
        /// 必须与 <see cref="CaptureTenantId"/> 配对使用，在后台任务内调用。
        /// 注意：多租户 CopyNew 后官方要求用 GetConnection（而非 GetConnectionScope），见 SqlSugar 文档"偶发性错误"章节（版本 ≥ 5.1.4.106）。
        /// 该连接不参与调用方请求/事务上下文，仅用于后台独立任务（fire-and-forget 写回、Job 等）。
        /// </summary>
        public static ISqlSugarClient CreateBackgroundDb(string tenantId)
        {
            var scope = DbScoped.SugarScope.CopyNew();
            return App.IsTenantEnabled() && !string.IsNullOrWhiteSpace(tenantId)
                ? scope.AsTenant().GetConnection(tenantId)
                : scope;
        }
    }

    /// <summary>
    /// 分页查询扩展
    /// </summary>
    public static class QueryableExtension
    {
        /// <summary>
        /// 读取列表
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">查询表单式</param>
        /// <param name="parm">分页参数</param>
        /// <returns></returns>
        public static PagedInfo<T> ToPage<T>(this ISugarQueryable<T> source, PagerInfo parm)
        {
            var page = new PagedInfo<T>();
            var total = 0;
            page.PageSize = parm.PageSize;
            page.PageIndex = parm.PageNum;
            if (parm.Sort.IsNotEmpty())
            {
                source.OrderByPropertyName(parm.Sort, parm.SortType.Contains("desc") ? OrderByType.Desc : OrderByType.Asc);
            }
            page.Result = source
                //.OrderByIF(parm.Sort.IsNotEmpty(), $"{parm.Sort.ToSqlFilter()} {(!string.IsNullOrWhiteSpace(parm.SortType) && parm.SortType.Contains("desc") ? "desc" : "asc")}")
                .ToPageList(parm.PageNum, parm.PageSize, ref total);
            page.TotalNum = total;
            return page;
        }

        public static async Task<PagedInfo<T>> ToPageAsync<T>(this ISugarQueryable<T> source, PagerInfo parm)
        {
            var page = new PagedInfo<T>();
            RefAsync<int> total = 0;
            page.PageSize = parm.PageSize;
            page.PageIndex = parm.PageNum;
            if (parm.Sort.IsNotEmpty())
            {
                source.OrderByPropertyName(parm.Sort, parm.SortType.Contains("desc") ? OrderByType.Desc : OrderByType.Asc);
            }
            page.Result = await source.ToPageListAsync(parm.PageNum, parm.PageSize, total);
            page.TotalNum = total;
            return page;
        }

        /// <summary>
        /// 转指定实体类Dto
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="source"></param>
        /// <param name="parm"></param>
        /// <returns></returns>
        public static PagedInfo<T2> ToPage<T, T2>(this ISugarQueryable<T> source, PagerInfo parm)
        {
            var page = new PagedInfo<T2>();
            var total = 0;
            page.PageSize = parm.PageSize;
            page.PageIndex = parm.PageNum;
            if (parm.Sort.IsNotEmpty())
            {
                source.OrderByPropertyName(parm.Sort, parm.SortType.Contains("desc") ? OrderByType.Desc : OrderByType.Asc);
            }
            var result = source
                //.OrderByIF(parm.Sort.IsNotEmpty(), $"{parm.Sort.ToSqlFilter()} {(!string.IsNullOrWhiteSpace(parm.SortType) && parm.SortType.Contains("desc") ? "desc" : "asc")}")
                .ToPageList(parm.PageNum, parm.PageSize, ref total);

            page.TotalNum = total;
            page.Result = result.Adapt<List<T2>>();
            return page;
        }

        public static async Task<PagedInfo<T2>> ToPageAsync<T, T2>(this ISugarQueryable<T> source, PagerInfo parm)
        {
            var page = new PagedInfo<T2>();
            RefAsync<int> total = 0;
            page.PageSize = parm.PageSize;
            page.PageIndex = parm.PageNum;
            if (parm.Sort.IsNotEmpty())
            {
                source.OrderByPropertyName(parm.Sort, parm.SortType.Contains("desc") ? OrderByType.Desc : OrderByType.Asc);
            }
            var result = await source.ToPageListAsync(parm.PageNum, parm.PageSize, total);

            page.TotalNum = total;
            page.Result = result.Adapt<List<T2>>();
            return page;
        }
    }
}