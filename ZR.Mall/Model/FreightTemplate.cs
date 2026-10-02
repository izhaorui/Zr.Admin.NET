namespace ZR.Mall.Model
{
    /// <summary>
    /// 运费模板
    /// </summary>
    /// <remarks>
    /// 本实体只存参数，计费规则在 FreightCalculator —— 见 document/商城运费模板设计.md 第四节。
    /// 不配送地区见 FreightTemplateExcludeRegion；未绑定模板的商品走 IsDefault = 1 的模板。
    /// </remarks>
    [SugarTable("mms_freight_template", "运费模板")]
    [Tenant("MallDb")]
    public class FreightTemplate
    {
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long Id { get; set; }

        /// <summary>模板名称</summary>
        [SugarColumn(Length = 50)]
        public string TemplateName { get; set; }

        /// <summary>计费方式：1 按件 / 2 按重，见 FreightChargeTypeEnum</summary>
        public int ChargeType { get; set; }

        /// <summary>首件：总件数不超过它时只收一份运费。仅按件使用；须 >= 0</summary>
        [SugarColumn(DecimalDigits = 2)]
        public decimal FirstPiece { get; set; }

        /// <summary>续件：超出首件后每满 AddPiece 件加收一份运费。仅按件使用；须 >= 1</summary>
        [SugarColumn(DecimalDigits = 2)]
        public decimal AddPiece { get; set; }

        /// <summary>首重（千克）：总重量不超过它时只收一份运费。仅按重使用；须 >= 0</summary>
        [SugarColumn(DecimalDigits = 3)]
        public decimal FirstWeight { get; set; }

        /// <summary>续重（千克）：超出首重后每满 AddWeight 千克加收一份运费。仅按重使用；须 &gt; 0</summary>
        [SugarColumn(DecimalDigits = 3)]
        public decimal AddWeight { get; set; }

        /// <summary>运费（元）：首件/首重范围内那一份的金额，续件/续重每单位也加收这么多</summary>
        [SugarColumn(DecimalDigits = 2)]
        public decimal Freight { get; set; }

        /// <summary>包邮门槛（元）：商品总额达到该值时本模板运费为 0；0 表示不包邮</summary>
        [SugarColumn(DecimalDigits = 2)]
        public decimal FreeShippingThreshold { get; set; }

        /// <summary>是否默认模板：1 是 / 0 否。未绑定模板的商品按它计费</summary>
        public int IsDefault { get; set; }
    }
}
