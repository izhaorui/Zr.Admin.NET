namespace ZR.Mall.Model.Dto
{
    /// <summary>
    /// C 端展示用的运费信息（商品详情页的包邮标签）
    /// </summary>
    /// <remarks>
    /// 刻意只有「是否包邮 + 门槛」，**不含金额**：商品详情页既拿不到收货地区、也没有整单商品总额，
    /// 算出来的金额必然不准（还可能撞上「不配送地区」被拦）。运费金额一律以确认页试算与下单结果为准。
    /// </remarks>
    public class FrontFreightInfoDto
    {
        /// <summary>是否包邮（无条件包邮与满额包邮都为 true）</summary>
        public bool FreeShipping { get; set; }

        /// <summary>
        /// 包邮门槛（元）：0 表示无条件包邮，大于 0 表示满该金额包邮。
        /// <see cref="FreeShipping"/> 为 false 时无意义，恒为 0。
        /// </summary>
        public decimal FreeShippingThreshold { get; set; }
    }
}
