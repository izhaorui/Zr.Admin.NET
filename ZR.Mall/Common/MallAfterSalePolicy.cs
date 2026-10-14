using ZR.Mall.Enum;
using ZR.Mall.Model;

namespace ZR.Mall.Common
{
    /// <summary>
    /// 买家售后申请的时效判定，目前只有「时效」这一条。
    ///
    /// 买家申请（拒绝并给出原因）与订单详情（是否显示「申请售后」入口）共用这里的结果，
    /// 两处各写一遍会出现入口与判定对不上的情况。
    ///
    /// 时限自确认收货时间起算，未签收不受限；天数读 MallSettings:AfterSaleDays，0 表示不限，默认 15。
    /// 虚拟商品不可售后属于商品形态问题（见 ProductTypeEnum），由调用方先行判断，不在这里判。
    /// </summary>
    public static class MallAfterSalePolicy
    {
        /// <summary>
        /// 售后申请期限（天）：读 MallSettings:AfterSaleDays，0 表示不限，默认 15。
        /// 读取与默认值统一在 <see cref="MallSettingsReader.AfterSaleDays"/>；缺失或非法不抛异常
        /// （售后是纠纷入口，宁可放行也不误拦）。
        /// </summary>
        public static int ResolveDays()
        {
            return MallSettingsReader.AfterSaleDays();
        }

        /// <summary>
        /// 是否已超过申请期限。
        /// </summary>
        /// <param name="order">订单（需带上确认收货时间）</param>
        /// <param name="now">当前时间：由调用方取库时间传入，本类不碰数据库</param>
        /// <param name="days">期限天数，见 <see cref="ResolveDays"/>；0 表示不限</param>
        /// <remarks>仅在订单已完成且有确认收货时间时判定；时间缺失的历史数据不视为超期。</remarks>
        public static bool IsExpired(OMSOrder order, DateTime now, int days)
        {
            if (order == null || days <= 0)
            {
                return false;
            }
            if (order.OrderStatus != OrderStatusEnum.Completed || !order.ConfirmTime.HasValue)
            {
                return false;
            }

            return order.ConfirmTime.Value.AddDays(days) < now;
        }
    }
}
