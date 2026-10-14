namespace ZR.Mall.Common
{
    /// <summary>
    /// 商城参数读取（appsettings 的 <c>MallSettings</c> 段）。
    /// 集中在一处读取，避免各处散落字符串键与默认值。配置缺失或非法一律回落默认值，不抛异常。
    /// </summary>
    public static class MallSettingsReader
    {
        /// <summary>
        /// 超时关单每批的上限：回补库存时 <c>orderIds.Contains</c> 会展开成 IN 参数，
        /// 单批过大将触碰 SQL Server 2100 参数上限（并拉长单批事务）。配置超过此值一律按此值截断。
        /// </summary>
        private const int MaxClosePendingBatchSize = 1000;

        /// <summary>
        /// 是否允许模拟支付（无真实支付渠道时直接标记已付）。默认关闭，仅开发/联调环境显式开启。
        /// </summary>
        public static bool AllowMockPay()
        {
            return bool.TryParse(App.Configuration?["MallSettings:AllowMockPay"], out var allow) && allow;
        }

        /// <summary>
        /// 未支付订单超时自动关闭的分钟数（Job_ClosePendingOrder）。默认 30，配置非正数按默认。
        /// </summary>
        public static int ClosePendingMinutes()
        {
            return ReadPositiveInt("MallSettings:ClosePendingMinutes", 30);
        }

        /// <summary>
        /// 超时关单每轮最多处理的订单数，防止一轮长时间占用事务。默认 500；
        /// 配置非正数按默认，超过 <see cref="MaxClosePendingBatchSize"/> 按上限截断。
        /// </summary>
        public static int ClosePendingBatchSize()
        {
            var value = ReadPositiveInt("MallSettings:ClosePendingBatchSize", 500);
            return value > MaxClosePendingBatchSize ? MaxClosePendingBatchSize : value;
        }

        /// <summary>
        /// 发货后自动确认收货的天数（Job_AutoConfirmReceipt）。默认 7，配置 0 或非正数按默认。
        /// </summary>
        public static int AutoConfirmReceiptDays()
        {
            return ReadPositiveInt("MallSettings:AutoConfirmReceiptDays", 7);
        }

        /// <summary>
        /// 确认收货后可申请售后的天数（见 MallAfterSalePolicy）。默认 15；<c>0</c> 表示不限；
        /// 配置缺失或为负数按默认。
        /// </summary>
        public static int AfterSaleDays()
        {
            return ReadNonNegativeInt("MallSettings:AfterSaleDays", 15);
        }

        /// <summary>
        /// 待发货导出的上限行数（防止整表进入内存）。默认 50000，配置非正数按默认。
        /// </summary>
        public static int WaitDeliveryExportLimit()
        {
            return ReadPositiveInt("MallSettings:WaitDeliveryExportLimit", 50000);
        }

        private static int ReadPositiveInt(string key, int fallback)
        {
            var raw = App.Configuration?[key];
            return int.TryParse(raw, out var value) && value > 0 ? value : fallback;
        }

        private static int ReadNonNegativeInt(string key, int fallback)
        {
            var raw = App.Configuration?[key];
            return int.TryParse(raw, out var value) && value >= 0 ? value : fallback;
        }
    }
}
