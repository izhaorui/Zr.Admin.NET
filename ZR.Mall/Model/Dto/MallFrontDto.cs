using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ZR.Mall.Model;

namespace ZR.Mall.Model.Dto
{
    /// <summary>
    /// C 端下单请求（**需登录**；收货手机号仅供联系，不作身份凭据）
    /// </summary>
    public class CreateOrderDto
    {
        /// <summary>
        /// 订单项（至少一个）
        /// </summary>
        [MinLength(1, ErrorMessage = "订单至少包含一个商品")]
        public List<CreateOrderItemDto> Items { get; set; } = new();

        /// <summary>
        /// 收货信息：实物单必填、虚拟单不需要。故不加 [Required]，必填由
        /// FrontOrderService.CreateOrder 按订单形态校验。手机号只是联系方式，不承担归属校验。
        /// </summary>
        public AddressSnapshot Address { get; set; }

        /// <summary>
        /// 订单备注（用户留言，可选）
        /// </summary>
        public string OrderNote { get; set; }

        /// <summary>
        /// 幂等键：前端每次下单意图生成的唯一值，用于防重复提交/网络重试。
        /// 不传则按“手机号 + 订单项 + 金额”自动去重。
        /// </summary>
        public string RequestId { get; set; }
    }

    /// <summary>
    /// C 端发起支付请求（归属由会员令牌判定，无需手机号）
    /// </summary>
    public class FrontPayOrderDto
    {
        /// <summary>
        /// 订单号
        /// </summary>
        [Required(ErrorMessage = "订单号不能为空")]
        public string OrderNo { get; set; }

        /// <summary>
        /// 支付通道：h5（默认，微信内/H5 浏览器跳转）/ miniProgram（微信小程序 JSAPI）/ app（App 原生支付）。
        /// 真实支付（WechatPay:Enabled=true）时按此字段选择预付单类型；模拟支付忽略。
        /// </summary>
        public string Channel { get; set; } = "h5";

        /// <summary>
        /// 微信小程序支付所需的用户 OpenId（channel=miniProgram 时必填）。
        /// 由前端通过 wx.login 换取后传入（见 shopping/front/order/wx/openid）。
        /// </summary>
        public string OpenId { get; set; }
    }

    /// <summary>
    /// 下单商品项
    /// </summary>
    public class CreateOrderItemDto
    {
        /// <summary>
        /// skuId
        /// </summary>
        [Required(ErrorMessage = "skuId不能为空")]
        public long SkuId { get; set; }

        /// <summary>
        /// 购买数量（>=1）
        /// </summary>
        [Range(1, int.MaxValue, ErrorMessage = "购买数量必须大于0")]
        public int Quantity { get; set; } = 1;
    }
}
