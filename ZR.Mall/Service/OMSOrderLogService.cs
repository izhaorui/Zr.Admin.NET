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
        /// <summary>
        /// 记录一条状态变更（自动写入 CreateTime）
        /// </summary>
        public void AddLog(long orderId, string orderNo, int fromStatus, int toStatus, string operType, string oper, string remark)
        {
            Insertable(BuildLog(orderId, orderNo, fromStatus, toStatus, operType, oper, remark)).ExecuteReturnIdentity();
        }

        /// <summary>
        /// 记录一条状态变更（异步版）：与 AddLog 写入内容一致，仅换用 SqlSugar 异步 API
        /// </summary>
        public async Task AddLogAsync(long orderId, string orderNo, int fromStatus, int toStatus, string operType, string oper, string remark)
        {
            await Insertable(BuildLog(orderId, orderNo, fromStatus, toStatus, operType, oper, remark)).ExecuteReturnIdentityAsync();
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
