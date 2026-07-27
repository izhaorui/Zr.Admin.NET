using Infrastructure.Attribute;
using ZR.Mall.Model;
using ZR.Mall.Service.IService;
using ZR.Repository;

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
            var log = new OMSOrderLog
            {
                OrderId = orderId,
                OrderNo = orderNo,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                OperType = operType,
                Operator = oper,
                Remark = remark
            };
            Insertable(log).ExecuteReturnIdentity();
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
