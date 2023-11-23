using ZR.Repository;

namespace ZR.ServiceCore
{
    /// <summary>
    /// 基础服务定义
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BaseService<T> : BaseRepository<T> where T : class, new()
    {
    }
}
