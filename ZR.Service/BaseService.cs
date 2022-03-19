using ZR.Repository;

namespace ZR.Service
{
    /// <summary>
    /// 基础服务定义
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BaseService<T> : BaseRepository<T> where T : class, new()
    {
        //public IBaseRepository<T> baseRepository;

        //public BaseService(IBaseRepository<T> repository)
        //{
        //    this.baseRepository = repository ?? throw new ArgumentNullException(nameof(repository));
        //}
    }
}
