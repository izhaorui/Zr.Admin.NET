using Microsoft.AspNetCore.Mvc;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Controllers
{
    /// <summary>
    /// 售后单管理（后台）：列表 / 详情 / 审核 / 确认收到退货 / 登记渠道退款 / 代客撤销
    /// </summary>
    /// <remarks>
    /// 权限前缀 shop:aftersale:*，与菜单种子（MallModuleInitProvider）一一对应。
    /// 渠道退款由运营在支付渠道完成后再回到「登记退款」登记（金额 + 渠道退款单号），
    /// 系统只负责流程与记账 —— 与项目既有的订单退款约定一致。
    /// </remarks>
    [Route("shopping/afterSale")]
    [ApiExplorerSettings(GroupName = "shopping")]
    public class OMSAfterSaleController : BaseController
    {
        private readonly IOMSAfterSaleService _afterSaleService;

        public OMSAfterSaleController(IOMSAfterSaleService afterSaleService)
        {
            _afterSaleService = afterSaleService;
        }

        /// <summary>售后单列表（按单号/订单号/状态/类型/时间筛选）</summary>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "shop:aftersale:list")]
        public IActionResult QueryList([FromQuery] AfterSaleQueryDto parm)
        {
            return SUCCESS(_afterSaleService.GetList(parm));
        }

        /// <summary>售后单详情（含订单实付/已退与商品摘要，登记退款时据此判断可退上限）</summary>
        [HttpGet("{id}")]
        [ActionPermissionFilter(Permission = "shop:aftersale:query")]
        public IActionResult GetDetail(long id)
        {
            return SUCCESS(_afterSaleService.GetDetail(id));
        }

        /// <summary>审核（通过 / 驳回；驳回必须填原因）</summary>
        [HttpPut("audit")]
        [ActionPermissionFilter(Permission = "shop:aftersale:audit")]
        [Log(Title = "售后审核", BusinessType = BusinessType.UPDATE)]
        public IActionResult Audit([FromBody] AfterSaleAuditDto dto)
        {
            var rows = _afterSaleService.Audit(dto);
            return ToResponse(rows > 0 ? ResultCode.SUCCESS : ResultCode.FAIL, rows > 0 ? "审核完成" : "审核失败");
        }

        /// <summary>确认收到退货（待商家收货 → 待退款）</summary>
        [HttpPut("receive/{id}")]
        [ActionPermissionFilter(Permission = "shop:aftersale:audit")]
        [Log(Title = "售后确认收货", BusinessType = BusinessType.UPDATE)]
        public IActionResult ConfirmReceive(long id, [FromQuery] string remark = null)
        {
            var rows = _afterSaleService.ConfirmReceive(id, remark);
            return ToResponse(rows > 0 ? ResultCode.SUCCESS : ResultCode.FAIL, rows > 0 ? "已确认收到退货" : "确认失败");
        }

        /// <summary>登记渠道退款并完成售后（金额 + 渠道退款单号）</summary>
        [HttpPut("refund")]
        [ActionPermissionFilter(Permission = "shop:aftersale:refund")]
        [Log(Title = "售后登记退款", BusinessType = BusinessType.UPDATE)]
        public IActionResult RegisterRefund([FromBody] AfterSaleRefundDto dto)
        {
            var rows = _afterSaleService.RegisterRefund(dto);
            return ToResponse(rows > 0 ? ResultCode.SUCCESS : ResultCode.FAIL, rows > 0 ? "退款已登记，售后完成" : "登记失败");
        }

        /// <summary>代客撤销售后（后台，无会员归属校验）</summary>
        [HttpPut("cancel/{id}")]
        [ActionPermissionFilter(Permission = "shop:aftersale:audit")]
        [Log(Title = "售后撤销", BusinessType = BusinessType.UPDATE)]
        public IActionResult Cancel(long id)
        {
            var rows = _afterSaleService.Cancel(id, null);
            return ToResponse(rows > 0 ? ResultCode.SUCCESS : ResultCode.FAIL, rows > 0 ? "已撤销" : "撤销失败");
        }
    }
}
