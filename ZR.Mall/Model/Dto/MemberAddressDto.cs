namespace ZR.Mall.Model.Dto
{
    /// <summary>
    /// 会员收货地址 新增/修改入参
    /// </summary>
    public class MemberAddressDto
    {
        /// <summary>
        /// 地址Id（修改时必填；新增忽略）
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 收件人姓名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 收件人手机号
        /// </summary>
        public string Phone { get; set; }

        /// <summary>
        /// 手机区号（可选）
        /// </summary>
        public string PhoneCode { get; set; }

        /// <summary>
        /// 省
        /// </summary>
        public string Province { get; set; }

        /// <summary>
        /// 省级行政编码（2 位，如 33）
        /// </summary>
        /// <remarks>
        /// 供运费模板按地区匹配使用。**不强制必填**：历史地址没有编码，
        /// 服务端会在计算运费时按名称兜底反查（<c>AreaService.ResolveCodes</c>），
        /// 所以老客户端不传也不会下单失败。
        /// </remarks>
        public string ProvinceCode { get; set; }

        /// <summary>
        /// 市
        /// </summary>
        public string City { get; set; }

        /// <summary>
        /// 市级行政编码（4 位，如 3301）
        /// </summary>
        public string CityCode { get; set; }

        /// <summary>
        /// 区/县
        /// </summary>
        public string District { get; set; }

        /// <summary>
        /// 区县级行政编码（6 位；东莞市等不设区地级市为 9 位街道/镇码）
        /// </summary>
        public string DistrictCode { get; set; }

        /// <summary>
        /// 详细地址
        /// </summary>
        public string DetailAddress { get; set; }

        /// <summary>
        /// 是否设为默认地址 1.是 0.否（该会员首条地址自动为默认）
        /// </summary>
        public int IsDefault { get; set; }
    }

    /// <summary>
    /// 会员收货地址 输出对象（不暴露归属字段）
    /// </summary>
    public class MemberAddressInfoDto
    {
        /// <summary>
        /// 地址Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 收件人姓名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 收件人手机号
        /// </summary>
        public string Phone { get; set; }

        /// <summary>
        /// 手机区号
        /// </summary>
        public string PhoneCode { get; set; }

        /// <summary>
        /// 省
        /// </summary>
        public string Province { get; set; }

        /// <summary>
        /// 省级行政编码；历史地址可能为空
        /// </summary>
        public string ProvinceCode { get; set; }

        /// <summary>
        /// 市
        /// </summary>
        public string City { get; set; }

        /// <summary>
        /// 市级行政编码；历史地址可能为空
        /// </summary>
        public string CityCode { get; set; }

        /// <summary>
        /// 区/县
        /// </summary>
        public string District { get; set; }

        /// <summary>
        /// 区县级行政编码；历史地址可能为空
        /// </summary>
        public string DistrictCode { get; set; }

        /// <summary>
        /// 详细地址
        /// </summary>
        public string DetailAddress { get; set; }

        /// <summary>
        /// 是否默认 1.是 0.否
        /// </summary>
        public int IsDefault { get; set; }
    }
}
