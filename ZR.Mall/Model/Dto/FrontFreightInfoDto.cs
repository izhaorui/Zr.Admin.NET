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

        /// <summary>
        /// 计费方式：1 按件 / 2 按重（见 FreightChargeTypeEnum）。无生效模板时为 0，前端据此不展示规则文案。
        /// </summary>
        public int ChargeType { get; set; }

        /// <summary>首件 / 首重数量（单位由 <see cref="ChargeType"/> 决定：件 或 kg）</summary>
        public decimal FirstUnit { get; set; }

        /// <summary>续件 / 续重单位（每满该数量加收一份 <see cref="Freight"/>）</summary>
        public decimal AddUnit { get; set; }

        /// <summary>首件 / 首重那一份的运费（元），同时也是每续一单位的加收金额</summary>
        public decimal Freight { get; set; }
    }
}
