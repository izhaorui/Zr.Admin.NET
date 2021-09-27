using Infrastructure.Attribute;
using Infrastructure.Model;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ZR.Model;
using ZR.Repository;
using ZR.Repository.DbProvider;

namespace ZR.Service
{
    /// <summary>
    /// 基础服务定义
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BaseService<T> : BaseRepository<T> where T : class,new()//, IBaseService<T> where T : class, new()
    {
        //private readonly IBaseRepository<T> BaseRepository;
        //public BaseService(IBaseRepository<T> baseRepository)
        //{
        //    BaseRepository = baseRepository;
        //}

        #region 事务

        ///// <summary>
        ///// 启用事务
        ///// </summary>
        //public void BeginTran()
        //{
        //    Context.Ado.BeginTran();
        //}

        ///// <summary>
        ///// 提交事务
        ///// </summary>
        //public void CommitTran()
        //{
        //    Context.Ado.CommitTran();
        //}

        ///// <summary>
        ///// 回滚事务
        ///// </summary>
        //public void RollbackTran()
        //{
        //    Context.Ado.RollbackTran();
        //}

        #endregion

        #region 添加操作
        /// <summary>
        /// 添加一条数据
        /// </summary>
        /// <param name="parm">T</param>
        /// <returns></returns>
        public int Add(T parm)
        {
            return base.Add(parm);// Context.Insertable(parm).RemoveDataCache().ExecuteCommand();
        }

        /// <summary>
        /// 添加
        /// </summary>
        /// <param name="parm"></param>
        /// <param name="iClumns">插入列</param>
        /// <param name="ignoreNull">忽略null列</param>
        /// <returns></returns>
        //public int Add(T parm, Expression<Func<T, object>> iClumns = null, bool ignoreNull = true)
        //{
        //    return Add(parm);
        //}

        /// <summary>
        /// 批量添加数据
        /// </summary>
        /// <param name="parm">List<T></param>
        /// <returns></returns>
        public int Add(List<T> parm)
        {
            return Context.Insertable(parm).RemoveDataCache().ExecuteCommand();
        }

        /// <summary>
        /// 添加或更新数据，不推荐使用了
        /// </summary>
        /// <param name="parm">List<T></param>
        /// <returns></returns>
        public T Saveable(T parm, Expression<Func<T, object>> uClumns = null, Expression<Func<T, object>> iColumns = null)
        {
            var command = Context.Saveable(parm);

            if (uClumns != null)
            {
                command = command.UpdateIgnoreColumns(uClumns);
            }

            if (iColumns != null)
            {
                command = command.InsertIgnoreColumns(iColumns);
            }

            return command.ExecuteReturnEntity();
        }

        /// <summary>
        /// 批量添加或更新数据
        /// </summary>
        /// <param name="parm">List<T></param>
        /// <returns></returns>
        public List<T> Saveable(List<T> parm, Expression<Func<T, object>> uClumns = null, Expression<Func<T, object>> iColumns = null)
        {
            var command = Context.Saveable(parm);

            if (uClumns != null)
            {
                command = command.UpdateIgnoreColumns(uClumns);
            }

            if (iColumns != null)
            {
                command = command.InsertIgnoreColumns(iColumns);
            }

            return command.ExecuteReturnList();
        }
        #endregion

        #region 查询操作

        /// <summary>
        /// 根据条件查询数据是否存在
        /// </summary>
        /// <param name="where">条件表达式树</param>
        /// <returns></returns>
        //public bool Any(Expression<Func<T, bool>> where)
        //{
        //    return base.Context.Any(where);
        //}

        /// <summary>
        /// 根据条件合计字段
        /// </summary>
        /// <param name="where">条件表达式树</param>
        /// <returns></returns>
        public TResult Sum<TResult>(Expression<Func<T, bool>> where, Expression<Func<T, TResult>> field)
        {
            return base.Context.Queryable<T>().Where(where).Sum(field);
        }

        /// <summary>
        /// 根据主值查询单条数据
        /// </summary>
        /// <param name="pkValue">主键值</param>
        /// <returns>泛型实体</returns>
        //public T GetId(object pkValue)
        //{
        //    return base.Context.Queryable<T>().InSingle(pkValue);
        //}

        /// <summary>
        /// 根据主键查询多条数据
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public List<T> GetIn(object[] ids)
        {
            return Context.Queryable<T>().In(ids).ToList();
        }

        /// <summary>
        /// 根据条件取条数
        /// </summary>
        /// <param name="where">条件表达式树</param>
        /// <returns></returns>
        public int GetCount(Expression<Func<T, bool>> where)
        {
            return Context.Queryable<T>().Count(where);

        }

        /// <summary>
        /// 查询所有数据(无分页,请慎用)
        /// </summary>
        /// <returns></returns>
        public List<T> GetAll(bool useCache = false, int cacheSecond = 3600)
        {
            return Context.Queryable<T>().WithCacheIF(useCache, cacheSecond).ToList();
        }

        /// <summary>
        /// 获得一条数据
        /// </summary>
        /// <param name="where">Expression<Func<T, bool>></param>
        /// <returns></returns>
        public T GetFirst2(Expression<Func<T, bool>> where)
        {
            return base.GetFirst(where);// Context.Queryable<T>().Where(where).First();
        }

        /// <summary>
        /// 获得一条数据
        /// </summary>
        /// <param name="parm">string</param>
        /// <returns></returns>
        //public T GetFirst(string parm)
        //{
        //    return Context.Queryable<T>().Where(parm).First();
        //}

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
        /// 根据条件查询数据
        /// </summary>
        /// <param name="where">条件表达式树</param>
        /// <returns></returns>
        public List<T> GetWhere(Expression<Func<T, bool>> where, bool useCache = false, int cacheSecond = 3600)
        {
            var query = Context.Queryable<T>().Where(where).WithCacheIF(useCache, cacheSecond);
            return query.ToList();
        }

        /// <summary>
		/// 根据条件查询数据
		/// </summary>
		/// <param name="where">条件表达式树</param>
		/// <returns></returns>
        public List<T> GetWhere(Expression<Func<T, bool>> where, Expression<Func<T, object>> order, string orderEnum = "Asc", bool useCache = false, int cacheSecond = 3600)
        {
            var query = Context.Queryable<T>().Where(where).OrderByIF(orderEnum == "Asc", order, OrderByType.Asc).OrderByIF(orderEnum == "Desc", order, OrderByType.Desc).WithCacheIF(useCache, cacheSecond);
            return query.ToList();
        }

        #endregion

        #region 修改操作

        ///// <summary>
        ///// 修改一条数据
        ///// </summary>
        ///// <param name="parm">T</param>
        ///// <returns></returns>
        //public int Update(T parm)
        //{
        //    return Context.Updateable(parm).RemoveDataCache().ExecuteCommand();
        //}

        ///// <summary>
        ///// 批量修改
        ///// </summary>
        ///// <param name="parm">T</param>
        ///// <returns></returns>
        //public int Update(List<T> parm)
        //{
        //    return Context.Updateable(parm).RemoveDataCache().ExecuteCommand();
        //}

        ///// <summary>
        ///// 按查询条件更新
        ///// </summary>
        ///// <param name="where"></param>
        ///// <param name="columns"></param>
        ///// <returns></returns>
        //public int Update(Expression<Func<T, bool>> where, Expression<Func<T, T>> columns)
        //{
        //    return Context.Updateable<T>().SetColumns(columns).Where(where).RemoveDataCache().ExecuteCommand();
        //}

        #endregion

        #region 删除操作

        ///// <summary>
        ///// 删除一条或多条数据
        ///// </summary>
        ///// <param name="parm">string</param>
        ///// <returns></returns>
        //public int Delete(object id)
        //{
        //    return Context.Deleteable<T>(id).RemoveDataCache().ExecuteCommand();
        //}

        ///// <summary>
        ///// 删除一条或多条数据
        ///// </summary>
        ///// <param name="parm">string</param>
        ///// <returns></returns>
        //public int Delete(object[] ids)
        //{
        //    return Context.Deleteable<T>().In(ids).RemoveDataCache().ExecuteCommand();
        //}

        ///// <summary>
        ///// 根据条件删除一条或多条数据
        ///// </summary>
        ///// <param name="where">过滤条件</param>
        ///// <returns></returns>
        //public int Delete(Expression<Func<T, bool>> where)
        //{
        //    return Context.Deleteable<T>().Where(where).RemoveDataCache().ExecuteCommand();
        //}

        public int DeleteTable()
        {
            return Context.Deleteable<T>().RemoveDataCache().ExecuteCommand();
        }
        #endregion

    }
}
