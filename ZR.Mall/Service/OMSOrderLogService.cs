using ZR.Mall.Model;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Service
{
    /// <summary>
    /// 订单状态变更日志Service业务层处理
    /// </summary>
    [AppService(ServiceType = typeof(IOMSOrderLogService))]
    public class OMSOrderLogService : BaseService<OMSOrderLog>, IOMSOrderLogService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 记录一条状态变更
        /// </summary>
        public void AddLog(long orderId, string orderNo, int fromStatus, int toStatus, string operType, string oper, string remark)
        {
            try
            {
                Insertable(BuildLog(orderId, orderNo, fromStatus, toStatus, operType, oper, remark)).ExecuteCommand();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"写入订单操作日志失败（不影响订单状态，但该次状态变更无审计记录）：OrderNo={orderNo} 操作类型={operType} 状态变更={fromStatus}→{toStatus}");
            }
        }

        /// <summary>
        /// 记录一条状态变更（异步版）：与 AddLog 写入内容一致，仅换用 SqlSugar 异步 API。
        /// 同样吞掉异常（记日志告警），不影响主流程。
        /// </summary>
        public async Task AddLogAsync(long orderId, string orderNo, int fromStatus, int toStatus, string operType, string oper, string remark)
        {
            try
            {
                await Insertable(BuildLog(orderId, orderNo, fromStatus, toStatus, operType, oper, remark)).ExecuteCommandAsync();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"写入订单操作日志失败（异步，不影响订单状态，但该次状态变更无审计记录）：OrderNo={orderNo} 操作类型={operType} 状态变更={fromStatus}→{toStatus}");
            }
        }

        /// <summary>
        /// 构造状态变更日志实体（同步/异步两个入口共用，保证写入内容一致）
        /// </summary>
        private static OMSOrderLog BuildLog(long orderId, string orderNo, int fromStatus, int toStatus, string operType, string oper, string remark)
        {
            return new OMSOrderLog
            {
                OrderId = orderId,
                OrderNo = orderNo,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                OperType = operType,
                Operator = oper,
                Remark = remark
            };
        }

        /// <summary>
        /// 查询某订单的全部状态变更（按时间正序）
        /// </summary>
        public List<OMSOrderLog> GetByOrder(long orderId)
        {
            return Queryable()
                .Where(x => x.OrderId == orderId)
                .OrderBy(x => x.Id, OrderByType.Asc)
                .ToList();
        }

        public List<OMSOrderLog> GetByOrderNo(string orderNo)
        {
            return Queryable()
                .Where(x => x.OrderNo == orderNo)
                .OrderBy(x => x.Id, OrderByType.Asc)
                .ToList();
        }
    }
}
