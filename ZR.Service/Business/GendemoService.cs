using Infrastructure;
using Infrastructure.Attribute;
using ZR.Model.Models;
using ZR.Repository;

namespace ZR.Service.Business
{
    /// <summary>
    /// 代码生成演示Service业务层处理
    ///
    /// @author zr
    /// @date 2021-11-24
    /// </summary>
    [AppService(ServiceType = typeof(IGendemoService), ServiceLifetime = LifeTime.Transient)]
    public class GendemoService: BaseService<Gendemo>, IGendemoService
    {
        private readonly GendemoRepository _Gendemorepository;
        public GendemoService(GendemoRepository repository)
        {
            _Gendemorepository = repository;
        }

        #region 业务逻辑代码

        #endregion
    }
}