using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ZR.Mall.Model;

namespace ZR.Mall.Model.Dto
{
    /// <summary>
    /// 游客/C端下单请求
    /// </summary>
    public class CreateOrderDto
    {
        /// <summary>
        /// 订单项（至少一个）
        /// </summary>
        [MinLength(1, ErrorMessage = "订单至少包含一个商品")]
        public List<CreateOrderItemDto> Items { get; set; } = new();

        /// <summary>
        /// 收货信息（必填，游客无账号，手机号作为身份锚点）
        /// </summary>
        [Required(ErrorMessage = "收货信息不能为空")]
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

        /// <summary>
        /// 短信验证码：下单前需对本手机号获取并校验，证明手机号归属（防止填他人手机号下单）。
        /// 与 Address.Phone 配对使用。
        /// </summary>
        [Required(ErrorMessage = "请先获取并输入短信验证码")]
        public string Code { get; set; }
    }

    /// <summary>
    /// 游客取消自己订单（需短信验证码校验归属）
    /// </summary>
    public class FrontCancelOrderDto
    {
        /// <summary>
        /// 订单Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 下单手机号（与订单收货手机号一致）
        /// </summary>
        [Required(ErrorMessage = "手机号不能为空")]
        public string Phone { get; set; }

        /// <summary>
        /// 短信验证码（证明手机号归属）
        /// </summary>
        [Required(ErrorMessage = "验证码不能为空")]
        public string Code { get; set; }
    }

    /// <summary>
    /// 游客模拟支付请求（订单号 + 下单手机号双重匹配，防止误付他人订单）
    /// </summary>
    public class FrontPayOrderDto
    {
        /// <summary>
        /// 订单号
        /// </summary>
        [Required(ErrorMessage = "订单号不能为空")]
        public string OrderNo { get; set; }

        /// <summary>
        /// 下单手机号（与订单收货手机号一致）
        /// </summary>
        [Required(ErrorMessage = "手机号不能为空")]
        public string Phone { get; set; }
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

    /// <summary>
    /// 游客查询自己订单（按手机号）请求
    /// </summary>
    public class FrontOrderQueryDto : PagerInfo
    {
        /// <summary>
        /// 收货手机号（与下单时一致）
        /// </summary>
        [Required(ErrorMessage = "手机号不能为空")]
        public string Phone { get; set; }

        /// <summary>
        /// 短信验证码（证明手机号归属，防止查他人订单）
        /// </summary>
        [Required(ErrorMessage = "验证码不能为空")]
        public string Code { get; set; }
    }

    /// <summary>
    /// 发送验证码请求（type: 4=查询订单, 5=下单。复用同一缓存键按手机号隔离）
    /// </summary>
    public class SendCodeDto
    {
        /// <summary>
        /// 收货手机号
        /// </summary>
        [Required(ErrorMessage = "手机号不能为空")]
        public string Phone { get; set; }

        /// <summary>
        /// 验证码用途：4=查询订单（默认），5=下单。用于短信日志区分，校验时按手机号统一比对。
        /// </summary>
        public int Type { get; set; } = 4;
    }
}
