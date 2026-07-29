using Microsoft.AspNetCore.Mvc;
using ZR.Mall.Model;
using ZR.Mall.Service.IService;

//创建时间：2026-07-26
namespace ZR.Mall.Controllers
{
    /// <summary>
    /// 订单状态变更日志查询
    /// </summary>
    [Route("shopping/orderlog")]
    [ApiExplorerSettings(GroupName = "shopping")]
    public class OMSOrderLogController : BaseController
    {
        private readonly IOMSOrderLogService _OrderLogService;

        public OMSOrderLogController(IOMSOrderLogService orderLogService)
        {
            _OrderLogService = orderLogService;
        }

        /// <summary>
        /// 查询订单状态变更日志（按订单Id或订单号，按时间正序）
        /// </summary>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "oms:order:query")]
        public IActionResult QueryOrderLog([FromQuery] long? orderId, [FromQuery] string orderNo)
        {
            List<OMSOrderLog> list;
            if (orderId.HasValue && orderId.Value > 0)
            {
                list = _OrderLogService.GetByOrder(orderId.Value);
            }
            else if (!string.IsNullOrEmpty(orderNo))
            {
                list = _OrderLogService.GetByOrderNo(orderNo);
            }
            else
            {
                list = new List<OMSOrderLog>();
            }
            return SUCCESS(list);
        }
    }
}
