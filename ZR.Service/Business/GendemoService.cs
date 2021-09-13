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
using ZR.Service.IService;

namespace ZR.Service.Business
{
    /// <summary>
    /// 服务接口实现
    /// </summary>
    [AppService(ServiceType = typeof(IGendemoService), ServiceLifetime = LifeTime.Transient)]
    public class GendemoService: BaseService<Gendemo>, IGendemoService
    {
		private readonly GendemoRepository _repository;
        public GendemoService(GendemoRepository repository)
        {
			_repository = repository;
        }
    }
}