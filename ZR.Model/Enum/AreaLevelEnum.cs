namespace ZR.Model.Enum
{
    /// <summary>
    /// 行政区划层级
    /// </summary>
    /// <remarks>
    /// 与 GB/T 2260 统计用区划代码的位数对应，但**不与位数强绑定**：
    /// 「不设区的地级市」（如东莞市、儋州市）第三级直接是街道/镇，编码为 9 位。
    /// 因此层级只能靠 <see cref="Public.Area.Level"/> 判定，不能靠 <c>AreaCode.Length</c> 反推。
    /// </remarks>
    public enum AreaLevelEnum
    {
        /// <summary>省级（编码 2 位，如 33 浙江省）</summary>
        Province = 1,

        /// <summary>市级（编码 4 位，如 3301 杭州市）</summary>
        City = 2,

        /// <summary>区县级（编码 6 位，如 330106 西湖区；东莞等为 9 位街道/镇码）</summary>
        District = 3
    }
}
