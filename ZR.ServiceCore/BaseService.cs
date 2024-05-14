using ZR.Repository;

namespace ZR.ServiceCore
{
    /// <summary>
    /// 基础服务定义
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BaseService<T> : BaseRepository<T> where T : class, new()
    {
        public override Task<bool> InsertAsync(T insertObj, CancellationToken cancellationToken)
        {
            return base.InsertAsync(insertObj, cancellationToken);
        }

        public override Task<bool> InsertRangeAsync(List<T> insertObjs, CancellationToken cancellationToken)
        {
            return base.InsertRangeAsync(insertObjs, cancellationToken);
        }
    }
}
