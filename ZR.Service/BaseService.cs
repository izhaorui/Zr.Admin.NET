using Infrastructure.Model;
using SqlSugar;
using SqlSugar.IOC;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using ZR.Model;
using ZR.Repository;

namespace ZR.Service
{
    /// <summary>
    /// 基础服务定义
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BaseService<T> : IBaseService<T> where T : class, new()
    {
        public IBaseRepository<T> baseRepository;

        public BaseService(IBaseRepository<T> repository)
        {
            this.baseRepository = repository ?? throw new ArgumentNullException(nameof(repository));
        }
        #region add
        /// <summary>
        /// 插入指定列使用
        /// </summary>
        /// <param name="parm"></param>
        /// <param name="iClumns"></param>
        /// <param name="ignoreNull"></param>
        /// <returns></returns>
        public int Insert(T parm, Expression<Func<T, object>> iClumns = null, bool ignoreNull = true)
        {
            return baseRepository.Insert(parm, iClumns, ignoreNull);
        }
        /// <summary>
        /// 插入实体
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public int Add(T t)
        {
            return baseRepository.Add(t);
        }
        public IInsertable<T> Insertable(T t)
        {
            return baseRepository.Insertable(t);
        }
        //public int Insert(SqlSugarClient client, T t)
        //{
        //    return client.Insertable(t).ExecuteCommand();
        //}

        //public long InsertBigIdentity(T t)
        //{
        //    return base.Context.Insertable(t).ExecuteReturnBigIdentity();
        //}

        public int Insert(List<T> t)
        {
            return baseRepository.Insert(t);
        }
        public long InsertReturnBigIdentity(T t)
        {
            return baseRepository.InsertReturnBigIdentity(t);
        }

        //public int InsertIgnoreNullColumn(List<T> t)
        //{
        //    return base.Context.Insertable(t).IgnoreColumns(true).ExecuteCommand();
        //}

        //public int InsertIgnoreNullColumn(List<T> t, params string[] columns)
        //{
        //    return base.Context.Insertable(t).IgnoreColumns(columns).ExecuteCommand();
        //}
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

        #endregion add

        #region update

        public int Update(T entity, bool ignoreNullColumns = false)
        {
            return baseRepository.Update(entity, ignoreNullColumns);
        }

        public int Update(T entity, Expression<Func<T, object>> expression, bool ignoreAllNull = false)
        {
            return baseRepository.Update(entity, expression, ignoreAllNull);
        }

        /// <summary>
        /// 根据实体类更新 eg：Update(dept, it => new { it.Status }, f => depts.Contains(f.DeptId));只更新Status列，条件是包含
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="expression"></param>
        /// <param name="where"></param>
        /// <returns></returns>
        public int Update(T entity, Expression<Func<T, object>> expression, Expression<Func<T, bool>> where)
        {
            return baseRepository.Update(entity, expression, where);
        }

        public int Update(SqlSugarClient client, T entity, Expression<Func<T, object>> expression, Expression<Func<T, bool>> where)
        {
            return client.Updateable(entity).UpdateColumns(expression).Where(where).ExecuteCommand();
        }

        ///// <summary>
        /////
        ///// </summary>
        ///// <param name="entity"></param>
        ///// <param name="list"></param>
        ///// <param name="isNull">默认为true</param>
        ///// <returns></returns>
        //public bool Update(T entity, List<string> list = null, bool isNull = true)
        //{
        //    if (list == null)
        //    {
        //        list = new List<string>()
        //    {
        //        "Create_By",
        //        "Create_time"
        //    };
        //    }
        //    //base.Context.Updateable(entity).IgnoreColumns(c => list.Contains(c)).Where(isNull).ExecuteCommand()
        //    return baseRepository.Update(entity, list, isNull);
        //}

        //public bool Update(List<T> entity)
        //{
        //    var result = base.Context.Ado.UseTran(() =>
        //    {
        //        base.Context.Updateable(entity).ExecuteCommand();
        //    });
        //    return result.IsSuccess;
        //}

        /// <summary>
        /// 更新指定列 eg：Update(w => w.NoticeId == model.NoticeId, it => new SysNotice(){ Update_time = DateTime.Now, Title = "通知标题" });
        /// </summary>
        /// <param name="where"></param>
        /// <param name="columns"></param>
        /// <returns></returns>
        public int Update(Expression<Func<T, bool>> where, Expression<Func<T, T>> columns)
        {
            return baseRepository.Update(where, columns);
        }
        #endregion update

        public DbResult<bool> UseTran(Action action)
        {
            var result = baseRepository.UseTran(action);
            return result;
        }

        public DbResult<bool> UseTran(SqlSugarClient client, Action action)
        {
            var result = client.Ado.UseTran(() => action());
            return result;
        }

        public bool UseTran2(Action action)
        {
            var result = baseRepository.UseTran2(action);
            return result;
        }

        #region delete

        /// <summary>
        /// 删除
        /// </summary>
        /// <returns></returns>
        public IDeleteable<T> Deleteable()
        {
            return baseRepository.Deleteable();
        }
        /// <summary>
        /// 删除表达式
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public int Delete(Expression<Func<T, bool>> expression)
        {
            return baseRepository.Delete(expression);
        }

        /// <summary>
        /// 批量删除
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int Delete(object[] obj)
        {
            return baseRepository.Delete(obj);
        }
        public int Delete(object id)
        {
            return baseRepository.Delete(id);
        }
        public int DeleteTable()
        {
            return baseRepository.DeleteTable();
        }

        #endregion delete

        #region query

        public bool Any(Expression<Func<T, bool>> expression)
        {
            return baseRepository.Any(expression);
        }

        public ISugarQueryable<T> Queryable()
        {
            return baseRepository.Queryable();
        }

        public List<T> GetList(Expression<Func<T, bool>> expression)
        {
            return baseRepository.GetList(expression);
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

        //public (List<T>, int) QueryableToPage(Expression<Func<T, bool>> expression, int pageIndex = 0, int pageSize = 10)
        //{
        //    int totalNumber = 0;
        //    var list = Context.Queryable<T>().Where(expression).ToPageList(pageIndex, pageSize, ref totalNumber);
        //    return (list, totalNumber);
        //}

        //public (List<T>, int) QueryableToPage(Expression<Func<T, bool>> expression, string order, int pageIndex = 0, int pageSize = 10)
        //{
        //    int totalNumber = 0;
        //    var list = Context.Queryable<T>().Where(expression).OrderBy(order).ToPageList(pageIndex, pageSize, ref totalNumber);
        //    return (list, totalNumber);
        //}

        //public (List<T>, int) QueryableToPage(Expression<Func<T, bool>> expression, Expression<Func<T, object>> orderFiled, string orderBy, int pageIndex = 0, int pageSize = 10)
        //{
        //    int totalNumber = 0;

        //    if (orderBy.Equals("DESC", StringComparison.OrdinalIgnoreCase))
        //    {
        //        var list = Context.Queryable<T>().Where(expression).OrderBy(orderFiled, OrderByType.Desc).ToPageList(pageIndex, pageSize, ref totalNumber);
        //        return (list, totalNumber);
        //    }
        //    else
        //    {
        //        var list = Context.Queryable<T>().Where(expression).OrderBy(orderFiled, OrderByType.Asc).ToPageList(pageIndex, pageSize, ref totalNumber);
        //        return (list, totalNumber);
        //    }
        //}

        //public List<T> SqlQueryToList(string sql, object obj = null)
        //{
        //    return Context.Ado.SqlQuery<T>(sql, obj);
        //}
        /// <summary>
        /// 获得一条数据
        /// </summary>
        /// <param name="where">Expression<Func<T, bool>></param>
        /// <returns></returns>
        public T GetFirst(Expression<Func<T, bool>> where)
        {
            return baseRepository.GetFirst(where);
        }

        /// <summary>
        /// 根据主值查询单条数据
        /// </summary>
        /// <param name="pkValue">主键值</param>
        /// <returns>泛型实体</returns>
        public T GetId(object pkValue)
        {
            return baseRepository.GetId(pkValue);
        }
        /// <summary>
        /// 根据条件查询分页数据
        /// </summary>
        /// <param name="where"></param>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<T> GetPages(Expression<Func<T, bool>> where, PagerInfo parm)
        {
            var source = baseRepository.GetPages(where, parm);

            return source;
        }

        public PagedInfo<T> GetPages(Expression<Func<T, bool>> where, PagerInfo parm, Expression<Func<T, object>> order, OrderByType orderEnum = OrderByType.Asc)
        {
            return baseRepository.GetPages(where, parm, order, orderEnum);
        }
        public PagedInfo<T> GetPages(Expression<Func<T, bool>> where, PagerInfo parm, Expression<Func<T, object>> order, string orderByType)
        {
            return baseRepository.GetPages(where, parm, order, orderByType == "desc" ? OrderByType.Desc : OrderByType.Asc);
        }
        /// <summary>
        /// 查询所有数据(无分页,请慎用)
        /// </summary>
        /// <returns></returns>
        public List<T> GetAll(bool useCache = false, int cacheSecond = 3600)
        {
            return baseRepository.GetAll(useCache, cacheSecond);
        }

        public int Count(Expression<Func<T, bool>> where)
        {
            return baseRepository.Count(where);
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
            return baseRepository.UseStoredProcedureToDataTable(procedureName, parameters);
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
            return baseRepository.UseStoredProcedureToTuple(procedureName, parameters);
        }

        //public string QueryableToJson(string select, Expression<Func<T, bool>> expressionWhere)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
