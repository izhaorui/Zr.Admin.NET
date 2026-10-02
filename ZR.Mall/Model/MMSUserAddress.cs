namespace ZR.Mall.Model
{
    /// <summary>
    /// 收货地址表
    /// </summary>
    [SugarTable("mms_address")]
    [SugarIndex("idx_mms_address_member", nameof(MemberId), OrderByType.Asc, false)]
    [Tenant("MallDb")]
    public class MMSUserAddress
    {
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long Id { get; set; }
        /// <summary>
        /// 会员ID（C 端会员归属；会员接口一律按此列过滤，禁止跨会员访问）
        /// </summary>
        // 同 OMSOrder.MemberId：映射到库中的历史列 userId
        [SugarColumn(ColumnName = "userId", DefaultValue = "0", ColumnDescription = "会员ID")]
        public long MemberId { get; set; }
        /// <summary>
        /// 收件人姓名
        /// </summary>
        public string UserName { get; set; }
        /// <summary>
        /// 手机人电话
        /// </summary>
        public string Phone { get; set; }
        /// <summary>
        /// 手机区号
        /// </summary>
        public string PhoneCode { get; set; }
        /// <summary>
        /// 省市区地址
        /// </summary>
        /// <remarks>
        /// 名称与编码**并存**：名称用于展示与地址回显，编码用于运费模板的地区匹配。
        /// 编码由 C 端地区选择器回传，历史数据为空 —— 运费计算时会按名称兜底反查
        /// （见 <c>AreaService.ResolveCodes</c>），不因存量数据缺编码而无法下单。
        /// </remarks>
        public string Province { get; set; }

        /// <summary>省级行政编码（2 位，如 33）；历史数据可能为空</summary>
        [SugarColumn(Length = 16)]
        public string ProvinceCode { get; set; }

        public string City { get; set; }

        /// <summary>市级行政编码（4 位，如 3301）；历史数据可能为空</summary>
        [SugarColumn(Length = 16)]
        public string CityCode { get; set; }

        public string District { get; set; }

        /// <summary>区县级行政编码（6 位；东莞市等不设区地级市为 9 位街道/镇码）</summary>
        [SugarColumn(Length = 16)]
        public string DistrictCode { get; set; }
        /// <summary>
        /// 详细地址
        /// </summary>
        public string DetailAddress { get; set; }
        /// <summary>
        /// 是否默认1.是 0.否
        /// </summary>
        public int IsDefault { get; set; }
        /// <summary>
        /// 添加时间
        /// </summary>
        public DateTime AddTime { get; set; }
    }
}
