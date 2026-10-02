namespace ZR.Mall.Model
{
    /// <summary>
    /// 运费模板的不配送地区
    /// </summary>
    /// <remarks>
    /// 按行政编码匹配而非名称（会因撤县设区失准），编码取自 <see cref="ZR.Model.Public.Area"/> 字典。
    /// 存省级码排除全省、市级码排除该市、区县码只排该区县；命中即阻止下单，
    /// 不静默计 0 运费。详见 document/商城运费模板设计.md 第四节。
    /// </remarks>
    [SugarTable("mms_freight_template_region", "运费模板不配送地区")]
    // 运费计算每次都要按模板取排除区；无索引会退化成全表扫描
    [SugarIndex("idx_mms_freight_region_tpl", nameof(TemplateId), OrderByType.Asc, false)]
    [Tenant("MallDb")]
    public class FreightTemplateExcludeRegion
    {
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long Id { get; set; }

        /// <summary>所属模板 Id（FreightTemplate.Id）</summary>
        public long TemplateId { get; set; }

        /// <summary>排除地区的行政编码（Area.AreaCode）</summary>
        [SugarColumn(Length = 16)]
        public string AreaCode { get; set; }

        /// <summary>地区名称（冗余，便于后台回显与订单排查）</summary>
        /// <remarks>地区更名后不会自动同步，仅作"当时选了什么"的留痕，匹配一律以 AreaCode 为准</remarks>
        [SugarColumn(Length = 64)]
        public string AreaName { get; set; }

        /// <summary>层级：1 省 / 2 市 / 3 区县，见 AreaLevelEnum</summary>
        public int Level { get; set; }
    }
}
