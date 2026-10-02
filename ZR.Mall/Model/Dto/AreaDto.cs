namespace ZR.Mall.Model.Dto
{
    /// <summary>
    /// 地区解析入参：名称 + 编码（编码可空）
    /// </summary>
    /// <remarks>
    /// 订单快照（AddressSnapshot）里两者都有，但**历史订单/历史地址只有名称**，
    /// 所以解析入口必须同时接受名称与编码，由服务端决定可信度。
    /// </remarks>
    public class AreaQueryDto
    {
        /// <summary>省级名称，如「浙江省」</summary>
        public string Province { get; set; }

        /// <summary>省级编码（2 位）；为空时按 <see cref="Province"/> 反查</summary>
        public string ProvinceCode { get; set; }

        /// <summary>市级名称，如「杭州市」</summary>
        public string City { get; set; }

        /// <summary>市级编码（4 位）；为空时按 <see cref="City"/> 反查</summary>
        public string CityCode { get; set; }

        /// <summary>区县级名称，如「西湖区」</summary>
        public string District { get; set; }

        /// <summary>区县级编码（6 位，东莞等不设区地级市为 9 位）；为空时按 <see cref="District"/> 反查</summary>
        public string DistrictCode { get; set; }
    }

    /// <summary>
    /// 地区解析结果：编码 + Id + 规范名称
    /// </summary>
    /// <remarks>
    /// 名称一并回填是为了**用字典里的规范名称覆盖用户传入的名称**：
    /// 用户可能填「浙江省杭州市西湖区」这类非标准写法，反查命中后统一成字典标准名，
    /// 避免同一地区在订单里出现多种写法，导致后台按地区统计对不上。
    /// </remarks>
    public class AreaCodesDto
    {
        /// <summary>省级编码；未命中为 null</summary>
        public string ProvinceCode { get; set; }

        /// <summary>市级编码；未命中为 null</summary>
        public string CityCode { get; set; }

        /// <summary>区县级编码；未命中为 null</summary>
        public string DistrictCode { get; set; }

        /// <summary>省级 Id；未命中为 0</summary>
        public long ProvinceId { get; set; }

        /// <summary>市级 Id；未命中为 0</summary>
        public long CityId { get; set; }

        /// <summary>区县级 Id；未命中为 0</summary>
        public long DistrictId { get; set; }

        /// <summary>省级规范名称；未命中为 null</summary>
        public string ProvinceName { get; set; }

        /// <summary>市级规范名称；未命中为 null</summary>
        public string CityName { get; set; }

        /// <summary>区县级规范名称；未命中为 null</summary>
        public string DistrictName { get; set; }
    }

    /// <summary>
    /// 地区树节点（后台运费模板配置不配送地区时用的级联下拉）
    /// </summary>
    public class AreaTreeDto
    {
        /// <summary>主键 Id</summary>
        public long Id { get; set; }

        /// <summary>行政编码</summary>
        public string AreaCode { get; set; }

        /// <summary>名称</summary>
        public string Name { get; set; }

        /// <summary>父级 Id；省级为 0</summary>
        public long ParentId { get; set; }

        /// <summary>层级：1 省 / 2 市 / 3 区县</summary>
        public int Level { get; set; }

        /// <summary>是否有下级；有下级时前端才允许展开（省/市为 true，区县为 false）</summary>
        public bool HasChildren { get; set; }
    }
}
