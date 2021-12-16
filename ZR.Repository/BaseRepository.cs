﻿using Infrastructure.Model;
using SqlSugar;
using SqlSugar.IOC;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using ZR.Model;

namespace ZR.Repository
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BaseRepository<T> : IBaseRepository<T> where T : class, new()
    {
        public ISqlSugarClient Context;

        public BaseRepository(ISqlSugarClient client = null)
        {
            var configId = typeof(T).GetCustomAttribute<TenantAttribute>()?.configId;
            if(configId != null)
            {
                Context = DbScoped.SugarScope.GetConnection(configId);
            }
            else
            {
                Context = client ?? DbScoped.SugarScope.GetConnection(1);//根据类传入的ConfigId自动选择
            }
        }

        #region add

        /// <summary>
        /// 插入实体
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public int Add(T t)
        {
            return Context.Insertable(t).IgnoreColumns(true).ExecuteCommand();
        }

        //public int Insert(SqlSugarClient client, T t)
        //{
        //    return client.Insertable(t).ExecuteCommand();
        //}

        public int Insert(List<T> t)
        {
            return Context.Insertable(t).ExecuteCommand();
        }
        public long InsertReturnBigIdentity(T t)
        {
            return Context.Insertable(t).ExecuteReturnBigIdentity();
        }

        //public int InsertIgnoreNullColumn(List<T> t)
        //{
        //    return base.Context.Insertable(t).IgnoreColumns(true).ExecuteCommand();
        //}

        //public int InsertIgnoreNullColumn(List<T> t, params string[] columns)
        //{
        //    return base.Context.Insertable(t).IgnoreColumns(columns).ExecuteCommand();
        //}
        public int Insert(T parm, Expression<Func<T, object>> iClumns = null, bool ignoreNull = true)
        {
            return Context.Insertable(parm).InsertColumns(iClumns).IgnoreColumns(ignoreNullColumn: ignoreNull).ExecuteCommand();
        }
        //public DbResult<bool> InsertTran(T t)
        //{
        //    var result = base.Context.Ado.UseTran(() =>
        //    {
        //        base.Context.Insertable(t).ExecuteCommand();
        //    });
        //    return result;
        //}

        //public DbResult<bool> InsertTran(List<T> t)
        //{
        //    var result = base.Context.Ado.UseTran(() =>
        //    {
        //        base.Context.Insertable(t).ExecuteCommand();
        //    });
        //    return result;
        //}

        //public T InsertReturnEntity(T t)
        //{
        //    return base.Context.Insertable(t).ExecuteReturnEntity();
        //}

        //public T InsertReturnEntity(T t, string sqlWith = SqlWith.UpdLock)
        //{
        //    return base.Context.Insertable(t).With(sqlWith).ExecuteReturnEntity();
        //}

        //public bool ExecuteCommand(string sql, object parameters)
        //{
        //    return base.Context.Ado.ExecuteCommand(sql, parameters) > 0;
        //}

        //public bool ExecuteCommand(string sql, params SugarParameter[] parameters)
        //{
        //    return base.Context.Ado.ExecuteCommand(sql, parameters) > 0;
        //}

        //public bool ExecuteCommand(string sql, List<SugarParameter> parameters)
        //{
        //    return base.Context.Ado.ExecuteCommand(sql, parameters) > 0;
        //}
        public IInsertable<T> Insertable(T t)
        {
            return Context.Insertable<T>(t);
        }
        #endregion add

        #region update

        public int Update(T entity, bool ignoreNullColumns = false)
        {
            return Context.Updateable(entity).IgnoreColumns(ignoreNullColumns).ExecuteCommand();
        }

        public int Update(T entity, Expression<Func<T, object>> expression, bool ignoreAllNull = false)
        {
            return Context.Updateable(entity).UpdateColumns(expression).IgnoreColumns(ignoreAllNull).ExecuteCommand();
        }

        public int Update(T entity, Expression<Func<T, object>> expression, Expression<Func<T, bool>> where)
        {
            return Context.Updateable(entity).UpdateColumns(expression).Where(where).ExecuteCommand();
        }

        public int Update(SqlSugarClient client, T entity, Expression<Func<T, object>> expression, Expression<Func<T, bool>> where)
        {
            return client.Updateable(entity).UpdateColumns(expression).Where(where).ExecuteCommand();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="list"></param>
        /// <param name="isNull">默认为true</param>
        /// <returns></returns>
        public int Update(T entity, List<string> list = null, bool isNull = true)
        {
            if (list == null)
            {
                list = new List<string>()
            {
                "Create_By",
                "Create_time"
            };
            }
            return Context.Updateable(entity).IgnoreColumns(isNull).IgnoreColumns(list.ToArray()).ExecuteCommand();
        }

        //public bool Update(List<T> entity)
        //{
        //    var result = base.Context.Ado.UseTran(() =>
        //    {
        //        base.Context.Updateable(entity).ExecuteCommand();
        //    });
        //    return result.IsSuccess;
        //}
        public int Update(Expression<Func<T, bool>> where, Expression<Func<T, T>> columns)
        {
            return Context.Updateable<T>().SetColumns(columns).Where(where).RemoveDataCache().ExecuteCommand();
        }
        #endregion update

        public DbResult<bool> UseTran(Action action)
        {
            var result = Context.Ado.UseTran(() => action());
            return result;
        }

        public DbResult<bool> UseTran(SqlSugarClient client, Action action)
        {
            var result = client.Ado.UseTran(() => action());
            return result;
        }

        public bool UseTran2(Action action)
        {
            var result = Context.Ado.UseTran(() => action());
            return result.IsSuccess;
        }

        #region delete

        /// <summary>
        /// 删除表达式
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public int Delete(Expression<Func<T, bool>> expression)
        {
            return Context.Deleteable<T>().Where(expression).ExecuteCommand();
        }

        /// <summary>
        /// 批量删除
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int Delete(object[] obj)
        {
            return Context.Deleteable<T>().In(obj).ExecuteCommand();
        }
        public int Delete(object id)
        {
            return Context.Deleteable<T>(id).ExecuteCommand();
        }
        public int DeleteTable()
        {
            return Context.Deleteable<T>().ExecuteCommand();
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

        //public ISugarQueryable<ExpandoObject> Queryable(string tableName, string shortName)
        //{
        //    return Context.Queryable(tableName, shortName);
        //}

        public List<T> GetList(Expression<Func<T, bool>> expression)
        {
            return Context.Queryable<T>().Where(expression).ToList();
        }

        //public Task<List<T>> QueryableToListAsync(Expression<Func<T, bool>> expression)
        //{
        //    return Context.Queryable<T>().Where(expression).ToListAsync();
        //}

        //public string QueryableToJson(string select, Expression<Func<T, bool>> expressionWhere)
        //{
        //    var query = base.Context.Queryable<T>().Select(select).Where(expressionWhere).ToList();
        //    return query.JilToJson();
        //}

        //public List<T> QueryableToList(string tableName)
        //{
        //    return Context.Queryable<T>(tableName).ToList();
        //}

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

        public PagedInfo<T> GetPages(Expression<Func<T, bool>> where, PagerInfo parm, Expression<Func<T, object>> order, OrderByType orderEnum = OrderByType.Asc)
        {
            var source = Context.Queryable<T>().Where(where).OrderByIF(orderEnum == OrderByType.Asc, order, OrderByType.Asc).OrderByIF(orderEnum == OrderByType.Desc, order, OrderByType.Desc);

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

        public int Count(Expression<Func<T, bool>> where)
        {
            return Context.Queryable<T>().Count(where);
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
            var total = 0;
            page.PageSize = parm.PageSize;
            page.PageIndex = parm.PageNum;

            page.Result = source.OrderByIF(!string.IsNullOrEmpty(parm.Sort), $"{parm.OrderBy} {(parm.Sort == "desc" ? "desc" : "asc")}")
                .ToPageList(parm.PageNum, parm.PageSize, ref total);
            page.TotalNum = total;
            return page;
        }

    }
}
