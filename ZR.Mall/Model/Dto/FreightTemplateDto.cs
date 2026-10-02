namespace ZR.Mall.Model.Dto
{
    /// <summary>
    /// 运费模板查询对象
    /// </summary>
    public class FreightTemplateQueryDto : PagerInfo
    {
        /// <summary>模板名称（模糊匹配）</summary>
        public string TemplateName { get; set; }

        /// <summary>计费方式：1 按件 / 2 按重；空表示全部</summary>
        public int? ChargeType { get; set; }
    }

    /// <summary>
    /// 运费模板输出对象（列表 / 详情）
    /// </summary>
    public class FreightTemplateDto
    {
        /// <summary>模板 Id</summary>
        public long Id { get; set; }

        /// <summary>模板名称</summary>
        [ExcelColumn(Name = "模板名称")]
        public string TemplateName { get; set; }

        /// <summary>计费方式：1 按件 / 2 按重</summary>
        [ExcelColumn(Name = "计费方式")]
        public int ChargeType { get; set; }

        /// <summary>首件</summary>
        [ExcelColumn(Name = "首件")]
        public decimal FirstPiece { get; set; }

        /// <summary>续件</summary>
        [ExcelColumn(Name = "续件")]
        public decimal AddPiece { get; set; }

        /// <summary>首重（kg）</summary>
        [ExcelColumn(Name = "首重(kg)")]
        public decimal FirstWeight { get; set; }

        /// <summary>续重（kg）</summary>
        [ExcelColumn(Name = "续重(kg)")]
        public decimal AddWeight { get; set; }

        /// <summary>运费（元）</summary>
        [ExcelColumn(Name = "运费(元)")]
        public decimal Freight { get; set; }

        /// <summary>包邮门槛（元），0 表示不包邮</summary>
        [ExcelColumn(Name = "包邮门槛(元)")]
        public decimal FreeShippingThreshold { get; set; }

        /// <summary>是否默认模板：1 是 / 0 否</summary>
        [ExcelColumn(Name = "是否默认")]
        public int IsDefault { get; set; }

        /// <summary>已绑定的商品数（列表展示用，避免前端再查一次）</summary>
        [ExcelColumn(Ignore = true)]
        public int ProductCount { get; set; }

        /// <summary>不配送地区（详情回显；列表不返回，避免 N+1）</summary>
        [ExcelColumn(Ignore = true)]
        public List<FreightTemplateRegionDto> ExcludeRegions { get; set; } = new();
    }

    /// <summary>
    /// 运费模板输入对象（新增 / 修改）
    /// </summary>
    /// <remarks>
    /// 与输出对象分开：不把 <c>ProductCount</c>、<c>ExcludeRegions</c> 之类的派生字段开放给写入，
    /// 避免请求体里的"已绑定商品数"被原样写库。
    /// </remarks>
    public class FreightTemplateInputDto
    {
        /// <summary>模板 Id；修改时必填，新增忽略</summary>
        public long Id { get; set; }

        /// <summary>模板名称</summary>
        [Required(ErrorMessage = "模板名称不能为空")]
        [StringLength(50, ErrorMessage = "模板名称不能超过 50 个字符")]
        public string TemplateName { get; set; }

        /// <summary>计费方式：1 按件 / 2 按重</summary>
        [Required(ErrorMessage = "请选择计费方式")]
        public int ChargeType { get; set; }

        /// <summary>首件（仅按件使用）</summary>
        public decimal FirstPiece { get; set; }

        /// <summary>续件（仅按件使用），须 ≥ 1</summary>
        public decimal AddPiece { get; set; } = 1m;

        /// <summary>首重 kg（仅按重使用）</summary>
        public decimal FirstWeight { get; set; }

        /// <summary>续重 kg（仅按重使用），须 &gt; 0</summary>
        public decimal AddWeight { get; set; } = 1m;

        /// <summary>运费（元）</summary>
        [Range(0, 999999.99, ErrorMessage = "运费不能为负数")]
        public decimal Freight { get; set; }

        /// <summary>包邮门槛（元），0 表示不包邮</summary>
        [Range(0, 9999999.99, ErrorMessage = "包邮门槛不能为负数")]
        public decimal FreeShippingThreshold { get; set; }

        /// <summary>是否默认模板：1 是 / 0 否</summary>
        public int IsDefault { get; set; }

        /// <summary>不配送地区；留空表示全国均可配送</summary>
        public List<FreightTemplateRegionDto> ExcludeRegions { get; set; } = new();
    }

    /// <summary>
    /// 不配送地区（按行政编码排除）
    /// </summary>
    public class FreightTemplateRegionDto
    {
        /// <summary>行政编码（与 Area 字典一致）</summary>
        public string AreaCode { get; set; }

        /// <summary>地区名称（冗余回显，便于运营确认选了什么）</summary>
        public string AreaName { get; set; }

        /// <summary>层级：1 省 / 2 市 / 3 区县</summary>
        public int Level { get; set; }
    }
}
