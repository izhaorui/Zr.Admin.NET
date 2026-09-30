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
        public string Province { get; set; }
        public string City { get; set; }
        public string District { get; set; }
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
