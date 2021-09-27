using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Extensions;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZR.Common;
using ZR.Model.Models;
using ZR.Repository;

namespace ZR.Service.Business
{
    /// <summary>
    /// 代码生成演示Service业务层处理
    ///
    /// @author zhaorui
    /// @date 2021-09-24
    /// </summary>
    [AppService(ServiceType = typeof(IGendemoService), ServiceLifetime = LifeTime.Transient)]
    public class GendemoService : BaseService<Gendemo>, IGendemoService
    {
        //private readonly SqlSugarClient _client;

        //public GendemoService(SqlSugarClient client, int db =1) : base(db)
        //{
        //    _client = client;
        //}

        #region 业务逻辑代码

        #endregion
    }
}