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
        /// 记录一条状态变更（异步版，自动写入 CreateTime）。
        /// 写入内容与 <see cref="AddLog"/> 完全一致，仅换用 SqlSugar 异步 API；
        /// 供 async 调用链使用，避免在异步方法内同步阻塞。同步调用方继续用 AddLog。
        /// </summary>
        Task AddLogAsync(long orderId, string orderNo, int fromStatus, int toStatus, string operType, string oper, string remark);

        /// <summary>
        /// 查询某订单的全部状态变更（按时间正序）
        /// </summary>
        List<OMSOrderLog> GetByOrder(long orderId);

        /// <summary>
        /// 按订单号查询全部状态变更（按时间正序）
        /// </summary>
        List<OMSOrderLog> GetByOrderNo(string orderNo);
    }
}
