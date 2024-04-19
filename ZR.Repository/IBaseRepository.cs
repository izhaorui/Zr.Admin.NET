using SqlSugar;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using ZR.Model;

namespace ZR.Repository
{
    public interface IBaseRepository<T> : ISimpleClient<T> where T : class, new()
    {
        #region add
        int Add(T t, bool ignoreNull = true);

        int Insert(List<T> t);
        int Insert(T parm, Expression<Func<T, object>> iClumns = null, bool ignoreNull = true);

        IInsertable<T> Insertable(T t);
        #endregion add

        #region update
        int Update(T entity, bool ignoreNullColumns = false, object data = null);

        /// <summary>
        /// 只更新表达式的值
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        int Update(T entity, Expression<Func<T, object>> expression, bool ignoreAllNull = false);

        int Update(T entity, Expression<Func<T, object>> expression, Expression<Func<T, bool>> where);

        int Update(Expression<Func<T, bool>> where, Expression<Func<T, T>> columns);

        #endregion update
        DbResult<bool> UseTran(Action action);

        DbResult<bool> UseTran(ISqlSugarClient client, Action action);

        bool UseTran2(Action action);

        #region delete
        IDeleteable<T> Deleteable();
        int Delete(object id, string title = "");
        int DeleteTable();
        bool Truncate();

        #endregion delete

        #region query
        /// <summary>
        /// 根据条件查询分页数据
        /// </summary>
        /// <param name="where"></param>
        /// <param name="parm"></param>
        /// <returns></returns>
        PagedInfo<T> GetPages(Expression<Func<T, bool>> where, PagerInfo parm);

        PagedInfo<T> GetPages(Expression<Func<T, bool>> where, PagerInfo parm, Expression<Func<T, object>> order, OrderByType orderEnum = OrderByType.Asc);
        PagedInfo<T> GetPages(Expression<Func<T, bool>> where, PagerInfo parm, Expression<Func<T, object>> order, string orderByType);

        bool Any(Expression<Func<T, bool>> expression);

        ISugarQueryable<T> Queryable();
        List<T> GetAll(bool useCache = false, int cacheSecond = 3600);

        List<T> SqlQueryToList(string sql, object obj = null);

        T GetId(object pkValue);

        #endregion query

        #region Procedure

        DataTable UseStoredProcedureToDataTable(string procedureName, List<SugarParameter> parameters);

        (DataTable, List<SugarParameter>) UseStoredProcedureToTuple(string procedureName, List<SugarParameter> parameters);

        #endregion Procedure
    }
}
