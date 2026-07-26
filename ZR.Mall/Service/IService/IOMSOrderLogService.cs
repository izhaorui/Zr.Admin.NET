using ZR.Mall.Model;
using ZR.Repository;

namespace ZR.Mall.Service.IService
{
    /// <summary>
    /// 订单状态变更日志Service接口
    /// </summary>
    public interface IOMSOrderLogService : IBaseService<OMSOrderLog>
    {
        /// <summary>
        /// 记录一条状态变更（自动写入 CreateTime）
        /// </summary>
        void AddLog(long orderId, string orderNo, int fromStatus, int toStatus, string operType, string oper, string remark);

        /// <summary>
        /// 查询某订单的全部状态变更（按时间正序）
        /// </summary>
        List<OMSOrderLog> GetByOrder(long orderId);
    }
}
