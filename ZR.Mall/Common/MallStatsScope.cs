using ZR.Mall.Enum;
using ZR.Mall.Model;

namespace ZR.Mall.Common
{
    /// <summary>
    /// 统计口径：**有效订单**的唯一判定，全项目统计方法都从这里取。
    ///
    /// 为什么必须收口：销售总额、趋势、Top10 三个接口原先各写一遍
    /// <c>IsDelete == 0 &amp;&amp; PayTime != null &amp;&amp; OrderStatus ∉ {已取消, 已退款}</c>，
    /// 复制三份的结果是"改了一处忘了另一处"，最终出现"退款率大于 100%"这种自相矛盾的数字。
    /// 口径只有一处，指标之间才可能互相对得上。
    /// </summary>
    public static class MallStatsScope
    {
        /// <summary>
        /// 有效订单：已支付（<c>PayTime</c> 非空）且未作废（未软删、订单状态不是已取消/已退款）。
        ///
        /// 口径说明：
        /// - <c>IsDelete = 0</c>：软删的单是测试/误建，不该进任何经营数字；
        /// - <c>PayTime != null</c>：待付款单没有收入，只有下单未付的算"待付款"；
        /// - 排除 <c>Cancel</c>：已取消（未付款）从未产生收入，本就不该被 PayTime 之外的条件排除掉，
        ///   但"已支付后再取消"会走到这里，保留排除以免把退款中的单重复计入；
        /// - 排除 <c>Closed</c>：已退款/关闭的单全额退光，计入会把 GMV 算虚。
        /// </summary>
        public static ISugarQueryable<OMSOrder> ValidOrders(ISugarQueryable<OMSOrder> query)
        {
            return query.Where(o => o.IsDelete == 0
                && o.PayTime != null
                && o.OrderStatus != OrderStatusEnum.Cancel
                && o.OrderStatus != OrderStatusEnum.Closed);
        }
    }
}
