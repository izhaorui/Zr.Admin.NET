using Infrastructure.Model;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using ZR.Model;

namespace ZR.Service
{
    /// <summary>
    /// 基础服务定义
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IBaseService<T> where T : class, new()
    {
        #region add
        int Add(T t);

        //int Insert(SqlSugarClient client, T t);

        //long InsertBigIdentity(T t);
        IInsertable<T> Insertable(T t);
        int Insert(List<T> t);
        int Insert(T parm, Expression<Func<T, object>> iClumns = null, bool ignoreNull = true);

        //int InsertIgnoreNullColumn(List<T> t);

        //int InsertIgnoreNullColumn(List<T> t, params string[] columns);

        //DbResult<bool> InsertTran(T t);

        //DbResult<bool> InsertTran(List<T> t);
        long InsertReturnBigIdentity(T t);
        //T InsertReturnEntity(T t);

        //T InsertReturnEntity(T t, string sqlWith = SqlWith.UpdLock);

        //bool ExecuteCommand(string sql, object parameters);

        //bool ExecuteCommand(string sql, params SugarParameter[] parameters);

        //bool ExecuteCommand(string sql, List<SugarParameter> parameters);

        #endregion add

        #region update

        int Update(T entity, bool ignoreNullColumns = false);

        /// <summary>
        /// 只更新表达式的值
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        int Update(T entity, Expression<Func<T, object>> expression, bool ignoreAllNull = false);

        int Update(T entity, Expression<Func<T, object>> expression, Expression<Func<T, bool>> where);

        int Update(SqlSugarClient client, T entity, Expression<Func<T, object>> expression, Expression<Func<T, bool>> where);

        /// <summary>
        ///
        /// </summary>
        /// <param name="entity">T</param>
        /// <param name="list">忽略更新</param>
        /// <param name="isNull">Null不更新</param>
        /// <returns></returns>
        //bool Update(T entity, List<string> list = null, bool isNull = true);

        //bool Update(List<T> entity);
        int Update(Expression<Func<T, bool>> where, Expression<Func<T, T>> columns);

        #endregion update

        DbResult<bool> UseTran(Action action);

        DbResult<bool> UseTran(SqlSugarClient client, Action action);

        bool UseTran2(Action action);

        #region delete
        IDeleteable<T> Deleteable();
        int Delete(Expression<Func<T, bool>> expression);
        int Delete(object[] obj);
        int Delete(object id);
        int DeleteTable();

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
        PagedInfo<T> GetPages(Expression<Func<T, bool>> where, PagerInfo parm, Expression<Func<T, object>> order, string orderType);
        bool Any(Expression<Func<T, bool>> expression);

        ISugarQueryable<T> Queryable();
        List<T> GetAll(bool useCache = false, int cacheSecond = 3600);

        //ISugarQueryable<ExpandoObject> Queryable(string tableName, string shortName);

        //ISugarQueryable<T, T1, T2> Queryable<T1, T2>() where T1 : class where T2 : new();

        List<T> GetList(Expression<Func<T, bool>> expression);

        //Task<List<T>> QueryableToListAsync(Expression<Func<T, bool>> expression);

        //string QueryableToJson(string select, Expression<Func<T, bool>> expressionWhere);

        //List<T> QueryableToList(string tableName);

        //(List<T>, int) QueryableToPage(Expression<Func<T, bool>> expression, int pageIndex = 0, int pageSize = 10);

        //(List<T>, int) QueryableToPage(Expression<Func<T, bool>> expression, string order, int pageIndex = 0, int pageSize = 10);

        //(List<T>, int) QueryableToPage(Expression<Func<T, bool>> expression, Expression<Func<T, object>> orderFiled, string orderBy, int pageIndex = 0, int pageSize = 10);

        //(List<T>, int) QueryableToPage(Expression<Func<T, bool>> expression, Bootstrap.BootstrapParams bootstrap);

        //List<T> SqlQueryToList(string sql, object obj = null);
        /// <summary>
        /// 获得一条数据
        /// </summary>
        /// <param name="where">Expression<Func<T, bool>></param>
        /// <returns></returns>
        T GetFirst(Expression<Func<T, bool>> where);
        T GetId(object pkValue);

        /// <summary>
        /// 获得一条数据
        /// </summary>
        /// <param name="parm">string</param>
        /// <returns></returns>
        //T GetFirst(string parm);

        int Count(Expression<Func<T, bool>> where);

        #endregion query

        #region Procedure

        DataTable UseStoredProcedureToDataTable(string procedureName, List<SugarParameter> parameters);

        (DataTable, List<SugarParameter>) UseStoredProcedureToTuple(string procedureName, List<SugarParameter> parameters);

        #endregion Procedure
    }
}