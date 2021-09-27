using Infrastructure;
using Infrastructure.Model;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ZR.Model;

namespace ZR.Repository
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BaseRepository<T> : SimpleClient<T>, IBaseRepository<T> where T : class, new()
    {
        public BaseRepository(ISqlSugarClient dbContext = null, int configId = 0) : base(dbContext)
        {
            if (dbContext == null)
            {
                string connStr = ConfigUtils.Instance.GetConnectionStrings(OptionsSetting.ConnAdmin);
                string dbKey = ConfigUtils.Instance.GetAppConfig<string>(OptionsSetting.DbKey);
                int dbType = ConfigUtils.Instance.GetAppConfig<int>(OptionsSetting.ConnDbType);
                if (!string.IsNullOrEmpty(dbKey))
                {
                    connStr = NETCore.Encrypt.EncryptProvider.DESDecrypt(connStr, dbKey);
                }

                var Db = new SqlSugarClient(new List<ConnectionConfig>()
                {
                    new ConnectionConfig(){
                        ConnectionString = connStr,
                        DbType = (DbType)dbType,
                        IsAutoCloseConnection = true,//开启自动释放模式和EF原理一样
                        InitKeyType = InitKeyType.Attribute,//从特性读取主键和自增列信息
                        ConfigId = 0
                    },
                    new ConnectionConfig(){
                        ConnectionString = "",
                        DbType = DbType.SqlServer,
                        IsAutoCloseConnection = true,
                        InitKeyType = InitKeyType.Attribute,//从特性读取主键和自增列信息
                        ConfigId = 1
                    },
                });

                //调式代码 用来打印SQL 
                Db.GetConnection(0).Aop.OnLogExecuting = (sql, pars) =>
                {
                    Console.BackgroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("【SQL语句】" + sql.ToLower() + "\r\n" + Db.Utilities.SerializeObject(pars.ToDictionary(it => it.ParameterName, it => it.Value)));
                };
                //出错打印日志
                Db.GetConnection(0).Aop.OnError = (e) =>
                {
                    Console.WriteLine($"[执行Sql出错]{e.Message}，SQL={e.Sql}");
                    Console.WriteLine();
                };

                Context = Db.GetConnection(configId);//根据类传入的ConfigId自动选择
            }
        }

        #region add
        /// <summary>
        /// 插入指定列使用
        /// </summary>
        /// <param name="parm"></param>
        /// <param name="iClumns"></param>
        /// <param name="ignoreNull"></param>
        /// <returns></returns>
        public int Add(T parm, Expression<Func<T, object>> iClumns = null, bool ignoreNull = true)
        {
            return Context.Insertable(parm).InsertColumns(iClumns).IgnoreColumns(ignoreNullColumn: ignoreNull).ExecuteCommand();
        }
        /// <summary>
        /// 插入实体
        /// </summary>
        /// <param name="t"></param>
        /// <param name="IgnoreNullColumn">默认忽略null列</param>
        /// <returns></returns>
        public int Add(T t)
        {
            return Context.Insertable(t).ExecuteCommand();
        }

        public int InsertIgnoreNullColumn(T t)
        {
            return Context.Insertable(t).IgnoreColumns(true).ExecuteCommand();
        }

        public int InsertIgnoreNullColumn(T t, params string[] columns)
        {
            return Context.Insertable(t).IgnoreColumns(columns).ExecuteCommand();
        }

        //public int Insert(SqlSugarClient client, T t)
        //{
        //    return client.Insertable(t).ExecuteCommand();
        //}

        public long InsertBigIdentity(T t)
        {
            return base.Context.Insertable(t).ExecuteReturnBigIdentity();
        }

        public int Insert(List<T> t)
        {
            return base.Context.Insertable(t).ExecuteCommand();
        }

        public int InsertIgnoreNullColumn(List<T> t)
        {
            return base.Context.Insertable(t).IgnoreColumns(true).ExecuteCommand();
        }

        public int InsertIgnoreNullColumn(List<T> t, params string[] columns)
        {
            return base.Context.Insertable(t).IgnoreColumns(columns).ExecuteCommand();
        }
        public int Insert(T parm, Expression<Func<T, object>> iClumns = null, bool ignoreNull = true)
        {
            return base.Context.Insertable(parm).InsertColumns(iClumns).IgnoreColumns(ignoreNullColumn: ignoreNull).ExecuteCommand();
        }
        public DbResult<bool> InsertTran(T t)
        {
            var result = base.Context.Ado.UseTran(() =>
            {
                base.Context.Insertable(t).ExecuteCommand();
            });
            return result;
        }

        public DbResult<bool> InsertTran(List<T> t)
        {
            var result = base.Context.Ado.UseTran(() =>
            {
                base.Context.Insertable(t).ExecuteCommand();
            });
            return result;
        }

        public T InsertReturnEntity(T t)
        {
            return base.Context.Insertable(t).ExecuteReturnEntity();
        }

        public T InsertReturnEntity(T t, string sqlWith = SqlWith.UpdLock)
        {
            return base.Context.Insertable(t).With(sqlWith).ExecuteReturnEntity();
        }

        public bool ExecuteCommand(string sql, object parameters)
        {
            return base.Context.Ado.ExecuteCommand(sql, parameters) > 0;
        }

        public bool ExecuteCommand(string sql, params SugarParameter[] parameters)
        {
            return base.Context.Ado.ExecuteCommand(sql, parameters) > 0;
        }

        public bool ExecuteCommand(string sql, List<SugarParameter> parameters)
        {
            return base.Context.Ado.ExecuteCommand(sql, parameters) > 0;
        }

        #endregion add

        #region update

        public bool UpdateEntity(T entity, bool ignoreNullColumns = false)
        {
            return base.Context.Updateable(entity).IgnoreColumns(ignoreNullColumns).ExecuteCommand() > 0;
        }

        public bool Update(T entity, Expression<Func<T, bool>> expression)
        {
            return base.Context.Updateable(entity).Where(expression).ExecuteCommand() > 0;
        }

        public bool Update(T entity, Expression<Func<T, object>> expression, bool ignoreAllNull = false)
        {
            return base.Context.Updateable(entity).UpdateColumns(expression).IgnoreColumns(ignoreAllNull).ExecuteCommand() > 0;
        }

        public bool Update(T entity, Expression<Func<T, object>> expression, Expression<Func<T, bool>> where)
        {
            return base.Context.Updateable(entity).UpdateColumns(expression).Where(where).ExecuteCommand() > 0;
        }

        public bool Update(SqlSugarClient client, T entity, Expression<Func<T, object>> expression, Expression<Func<T, bool>> where)
        {
            return client.Updateable(entity).UpdateColumns(expression).Where(where).ExecuteCommand() > 0;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="list"></param>
        /// <param name="isNull">默认为true</param>
        /// <returns></returns>
        public bool Update(T entity, List<string> list = null, bool isNull = true)
        {
            if (list == null)
            {
                list = new List<string>()
            {
                "Create_By",
                "Create_time"
            };
            }
            //base.Context.Updateable(entity).IgnoreColumns(c => list.Contains(c)).Where(isNull).ExecuteCommand()
            return base.Context.Updateable(entity).IgnoreColumns(isNull).IgnoreColumns(list.ToArray()).ExecuteCommand() > 0;
        }

        public bool Update(List<T> entity)
        {
            var result = base.Context.Ado.UseTran(() =>
            {
                base.Context.Updateable(entity).ExecuteCommand();
            });
            return result.IsSuccess;
        }
        public bool Update(Expression<Func<T, bool>> where, Expression<Func<T, T>> columns)
        {
            return base.Context.Updateable<T>().SetColumns(columns).Where(where).RemoveDataCache().ExecuteCommand() > 0;
        }
        #endregion update

        //public DbResult<bool> UseTran(Action action)
        //{
        //    var result = base.Context.Ado.UseTran(() => action());
        //    return result;
        //}

        //public DbResult<bool> UseTran(SqlSugarClient client, Action action)
        //{
        //    var result = client.Ado.UseTran(() => action());
        //    return result;
        //}

        //public bool UseTran2(Action action)
        //{
        //    var result = base.Context.Ado.UseTran(() => action());
        //    return result.IsSuccess;
        //}

        #region delete

        public bool DeleteExp(Expression<Func<T, bool>> expression)
        {
            return Context.Deleteable<T>().Where(expression).ExecuteCommand() > 0;
        }

        //public bool Delete<PkType>(PkType[] primaryKeyValues)
        //{
        //    return base.Context.Deleteable<T>().In(primaryKeyValues).ExecuteCommand() > 0;
        //}

        public int Delete(object[] obj)
        {
            return Context.Deleteable<T>().In(obj).ExecuteCommand();
        }
        public int Delete(object id)
        {
            return Context.Deleteable<T>(id).ExecuteCommand();
        }
        public bool DeleteTable()
        {
            return Context.Deleteable<T>().ExecuteCommand() > 0;
        }

        #endregion delete

        #region query

        public bool Any(Expression<Func<T, bool>> expression)
        {
            return Context.Queryable<T>().Where(expression).Any();
        }

        public ISugarQueryable<T> Queryable()
        {
            return Context.Queryable<T>();
        }

        public ISugarQueryable<ExpandoObject> Queryable(string tableName, string shortName)
        {
            return Context.Queryable(tableName, shortName);
        }

        public List<T> QueryableToList(Expression<Func<T, bool>> expression)
        {
            return Context.Queryable<T>().Where(expression).ToList();
        }

        public Task<List<T>> QueryableToListAsync(Expression<Func<T, bool>> expression)
        {
            return Context.Queryable<T>().Where(expression).ToListAsync();
        }

        //public string QueryableToJson(string select, Expression<Func<T, bool>> expressionWhere)
        //{
        //    var query = base.Context.Queryable<T>().Select(select).Where(expressionWhere).ToList();
        //    return query.JilToJson();
        //}

        public List<T> QueryableToList(string tableName)
        {
            return Context.Queryable<T>(tableName).ToList();
        }

        public List<T> QueryableToList(string tableName, Expression<Func<T, bool>> expression)
        {
            return Context.Queryable<T>(tableName).Where(expression).ToList();
        }

        public (List<T>, int) QueryableToPage(Expression<Func<T, bool>> expression, int pageIndex = 0, int pageSize = 10)
        {
            int totalNumber = 0;
            var list = Context.Queryable<T>().Where(expression).ToPageList(pageIndex, pageSize, ref totalNumber);
            return (list, totalNumber);
        }

        public (List<T>, int) QueryableToPage(Expression<Func<T, bool>> expression, string order, int pageIndex = 0, int pageSize = 10)
        {
            int totalNumber = 0;
            var list = Context.Queryable<T>().Where(expression).OrderBy(order).ToPageList(pageIndex, pageSize, ref totalNumber);
            return (list, totalNumber);
        }

        public (List<T>, int) QueryableToPage(Expression<Func<T, bool>> expression, Expression<Func<T, object>> orderFiled, string orderBy, int pageIndex = 0, int pageSize = 10)
        {
            int totalNumber = 0;

            if (orderBy.Equals("DESC", StringComparison.OrdinalIgnoreCase))
            {
                var list = Context.Queryable<T>().Where(expression).OrderBy(orderFiled, OrderByType.Desc).ToPageList(pageIndex, pageSize, ref totalNumber);
                return (list, totalNumber);
            }
            else
            {
                var list = Context.Queryable<T>().Where(expression).OrderBy(orderFiled, OrderByType.Asc).ToPageList(pageIndex, pageSize, ref totalNumber);
                return (list, totalNumber);
            }
        }

        public List<T> SqlQueryToList(string sql, object obj = null)
        {
            return Context.Ado.SqlQuery<T>(sql, obj);
        }
        /// <summary>
        /// 获得一条数据
        /// </summary>
        /// <param name="where">Expression<Func<T, bool>></param>
        /// <returns></returns>
        public T GetFirst(Expression<Func<T, bool>> where)
        {
            return Context.Queryable<T>().Where(where).First();
        }

        /// <summary>
        /// 根据主值查询单条数据
        /// </summary>
        /// <param name="pkValue">主键值</param>
        /// <returns>泛型实体</returns>
        public T GetId(object pkValue)
        {
            return Context.Queryable<T>().InSingle(pkValue);
        }
        /// <summary>
        /// 根据条件查询分页数据
        /// </summary>
        /// <param name="where"></param>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<T> GetPages(Expression<Func<T, bool>> where, PagerInfo parm)
        {
            var source = Context.Queryable<T>().Where(where);

            return source.ToPage(parm);
        }

        public PagedInfo<T> GetPages(Expression<Func<T, bool>> where, PagerInfo parm, Expression<Func<T, object>> order, string orderEnum = "Asc")
        {
            var source = Context.Queryable<T>().Where(where).OrderByIF(orderEnum == "Asc", order, OrderByType.Asc).OrderByIF(orderEnum == "Desc", order, OrderByType.Desc);

            return source.ToPage(parm);
        }
        /// <summary>
        /// 查询所有数据(无分页,请慎用)
        /// </summary>
        /// <returns></returns>
        public List<T> GetAll(bool useCache = false, int cacheSecond = 3600)
        {
            return Context.Queryable<T>().WithCacheIF(useCache, cacheSecond).ToList();
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
        //public DataTable UseStoredProcedureToDataTable(string procedureName, List<SugarParameter> parameters)
        //{
        //    return base.Context.Ado.UseStoredProcedure().GetDataTable(procedureName, parameters);
        //}

        /// <summary>
        /// 带output返回值
        /// var list = new List<SugarParameter>();
        /// list.Add(new SugarParameter(ParaName, ParaValue, true));  output
        /// list.Add(new SugarParameter(ParaName, ParaValue)); input
        /// </summary>
        /// <param name="procedureName"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        //public (DataTable, List<SugarParameter>) UseStoredProcedureToTuple(string procedureName, List<SugarParameter> parameters)
        //{
        //    var result = (base.Context.Ado.UseStoredProcedure().GetDataTable(procedureName, parameters), parameters);
        //    return result;
        //}

        //public string QueryableToJson(string select, Expression<Func<T, bool>> expressionWhere)
        //{
        //    throw new NotImplementedException();
        //}
    }

    public static class QueryableExtension
    {
        /// <summary>
        /// 读取列表
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="parm"></param>
        /// <returns></returns>
        public static PagedInfo<T> ToPage<T>(this ISugarQueryable<T> source, PagerInfo parm)
        {
            var page = new PagedInfo<T>();
            var total = source.Count();
            page.TotalCount = total;
            page.PageSize = parm.PageSize;
            page.PageIndex = parm.PageNum;

            //page.DataSource = source.OrderByIF(!string.IsNullOrEmpty(parm.Sort), $"{parm.OrderBy} {(parm.Sort == "descending" ? "desc" : "asc")}").ToPageList(parm.PageNum, parm.PageSize);
            page.Result = source.ToPageList(parm.PageNum, parm.PageSize);
            return page;
        }

    }
}
