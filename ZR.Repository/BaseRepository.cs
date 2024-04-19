using Infrastructure.Extensions;
using Mapster;
using SqlSugar;
using SqlSugar.IOC;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using ZR.Model;

namespace ZR.Repository
{
    /// <summary>
    /// 数据仓库类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BaseRepository<T> : SimpleClient<T> where T : class, new()
    {
        public ITenant itenant = null;//多租户事务
        public BaseRepository(ISqlSugarClient context = null) : base(context)
        {
            //通过特性拿到ConfigId
            var configId = typeof(T).GetCustomAttribute<TenantAttribute>()?.configId;
            if (configId != null)
            {
                Context = DbScoped.SugarScope.GetConnectionScope(configId);//根据类传入的ConfigId自动选择
            }
            else
            {
                Context = context ?? DbScoped.SugarScope.GetConnectionScope(0);//没有默认db0
            }
            //Context = DbScoped.SugarScope.GetConnectionScopeWithAttr<T>();
            itenant = DbScoped.SugarScope;//设置租户接口
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
            return Context.Updateable(entity).UpdateColumns(expression).IgnoreColumns(ignoreAllNull).ExecuteCommand();
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
        #endregion update

        public DbResult<bool> UseTran(Action action)
        {
            try
            {
                var result = Context.Ado.UseTran(() => action());
                return result;
            }
            catch (Exception ex)
            {
                Context.Ado.RollbackTran();
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        /// <param name="action">增删改查方法</param>
        /// <returns></returns>
        public DbResult<bool> UseTran(ISqlSugarClient client, Action action)
        {
            try
            {
                var result = client.AsTenant().UseTran(() => action());
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("事务异常" + ex.Message);
                client.AsTenant().RollbackTran();
                throw;
            }
        }

        /// <summary>
        /// 使用事务
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
            return Context.Queryable<T>().Where(expression).Any();
        }

        public ISugarQueryable<T> Queryable()
        {
            return Context.Queryable<T>();
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
            var source = Context
                .Queryable<T>()
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
    }
}
