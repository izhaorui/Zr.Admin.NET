using ZR.Mall.Enum;

namespace ZR.Mall.Model.Dto
{
    /// <summary>
    /// 会员"我的订单"查询对象（会员身份取自令牌，无需传手机号）
    /// </summary>
    public class MemberOrderQueryDto : PagerInfo
    {
        /// <summary>
        /// 订单状态（可选，不传表示全部）
        /// </summary>
        public OrderStatusEnum? OrderStatus { get; set; }
    }
}
