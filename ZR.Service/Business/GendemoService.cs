using Infrastructure;
using Infrastructure.Attribute;
using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.Models;
using ZR.Repository;
using ZR.Service.Business.IBusinessService;
using System;
using SqlSugar;
using System.Collections.Generic;

namespace ZR.Service.Business
{
    /// <summary>
    /// 演示Service业务层处理
    ///
    /// @author zz
    /// @date 2022-03-31
    /// </summary>
    [AppService(ServiceType = typeof(IGenDemoService), ServiceLifetime = LifeTime.Transient)]
    public class GenDemoService : BaseService<GenDemo>, IGenDemoService
    {
        private readonly GenDemoRepository _GenDemorepository;
        public GenDemoService(GenDemoRepository repository)
        {
            _GenDemorepository = repository;
        }

        #region 业务逻辑代码

        /// <summary>
        /// 查询演示列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        public PagedInfo<GenDemo> GetList(GenDemoQueryDto parm)
        {
            //开始拼装查询条件
            var predicate = Expressionable.Create<GenDemo>();

            //搜索条件查询语法参考Sqlsugar
            predicate = predicate.AndIF(parm.Id != null, it => it.Id == parm.Id);
            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.Name), it => it.Name == parm.Name);
            predicate = predicate.AndIF(parm.ShowStatus != null, it => it.ShowStatus == parm.ShowStatus);
            predicate = predicate.AndIF(parm.BeginAddTime == null, it => it.AddTime >= DateTime.Now.AddDays(-1));
            predicate = predicate.AndIF(parm.BeginAddTime != null, it => it.AddTime >= parm.BeginAddTime && it.AddTime <= parm.EndAddTime);
            var response = _GenDemorepository
                .Queryable()
                .Where(predicate.ToExpression())
                .ToPage(parm);
            return response;
        }

        #endregion
    }
}