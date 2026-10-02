using ZR.Mall.Model;

namespace ZR.Mall.Common
{
    /// <summary>
    /// C 端订单归属判定（**唯一实现**，控制器与 Service 共用，避免多处各写一份而规则漂移）。
    ///
    /// 规则：订单归属**只认会员身份（MemberId）**，手机号一律不作为凭据。
    ///
    /// 为什么不能用手机号：收货手机号只是联系信息 —— 会员把货寄给家人（收货号 = 家人号码）时，
    /// 若按"手机号匹配"放行，家人就能在列表里看到、按自增 Id 读取详情、乃至取消该会员的订单。
    /// 商城已取消游客下单，订单必然绑定 MemberId，不存在"无会员身份"的合法订单。
    /// </summary>
    public static class OrderOwnership
    {
        /// <summary>
        /// 订单是否属于该会员。memberId 为 null（未携带会员令牌）时一律返回 false。
        /// </summary>
        public static bool IsOwnedBy(OMSOrder order, long? memberId)
            => order != null && IsOwnedBy(order.MemberId, memberId);

        /// <summary>
        /// 归属判定（按会员 Id）。
        /// </summary>
        /// <remarks>
        /// 售后单等「从订单派生的单据」复用这一份实现 —— 它们各自的 MemberId 是下单时从订单抄来的，
        /// 规则与订单完全一致，另写一份只会让两处慢慢漂移。
        /// </remarks>
        public static bool IsOwnedBy(long? ownerMemberId, long? memberId)
        {
            if (!ownerMemberId.HasValue || !memberId.HasValue || memberId.Value <= 0)
            {
                return false;
            }
            return ownerMemberId.Value == memberId.Value;
        }
    }
}
