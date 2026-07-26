using Microsoft.AspNetCore.Mvc;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

//创建时间：2026-07-26
namespace ZR.Mall.Controllers
{
    /// <summary>
    /// 支付流水查询（后台对账/排查）
    /// </summary>
    [Route("shopping/Payment")]
    [ApiExplorerSettings(GroupName = "shopping")]
    public class PaymentController : BaseController
    {
        private readonly IOMSPaymentService _paymentService;

        public PaymentController(IOMSPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        /// <summary>
        /// 查询支付流水列表（按订单号/渠道/状态/时间）
        /// </summary>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "oms:payment:list")]
        public IActionResult QueryList([FromQuery] OMSPaymentQueryDto parm)
        {
            var response = _paymentService.GetList(parm);
            return SUCCESS(response);
        }
    }
}
