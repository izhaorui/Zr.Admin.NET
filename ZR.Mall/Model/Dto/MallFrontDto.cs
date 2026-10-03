using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ZR.Mall.Enum;
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

        /// <summary>
        /// 表单填写值：key = 商品表单字段的 Field，value = 买家填的内容。
        ///
        /// 挂在**行**上而不是订单上：一单可含多个商品（甚至多个虚拟商品），各自的表单不同，
        /// 放订单级只能填一份。商品没配表单（FormItems 为空）时本字段会被整体忽略。
        ///
        /// 服务端按商品配置逐项校验（必填/格式/候选值），前端校验只是提示。
        /// 定义外的 key 会被丢弃，不落库。
        /// </summary>
        public Dictionary<string, string> FormValues { get; set; }
    }

    /// <summary>
    /// C 端按 SKU 批量取「下单表单定义」（结算页用）。
    ///
    /// 为什么需要批量接口：结算页要拿到**本次结算涉及的全部商品**的表单才能渲染输入框，
    /// 逐个调商品详情会产生 N 次请求；这里一次查询回全部，缺哪个字段由前端按空处理。
    /// </summary>
    public class FrontSkuFormDto
    {
        /// <summary>skuId</summary>
        public long SkuId { get; set; }

        public long ProductId { get; set; }

        public string ProductName { get; set; }

        /// <summary>规格快照（与 SKU 列表同一写法）</summary>
        public string SkuSpec { get; set; }

        /// <summary>
        /// 商品形态：1 实物 / 2 虚拟。前端据此决定这一行渲染「收货地址」还是「表单」。
        /// 服务端不采信它（形态由服务端按商品判定），只是让前端少一次判断就能少一处错。
        /// </summary>
        public ProductTypeEnum ProductType { get; set; }

        /// <summary>
        /// 已归一化的字段定义（type 落词表、label 空则回落 field、按 sort 排序），可直接渲染；
        /// 空数组表示该商品下单无需额外填写。
        /// </summary>
        public List<ProductFormItem> FormItems { get; set; }
    }
}
