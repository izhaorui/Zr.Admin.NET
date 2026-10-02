namespace ZR.Mall.Enum
{
    /// <summary>
    /// 运费计费方式（两种互斥，一单只用一种）
    /// </summary>
    /// <remarks>
    /// 未选中的那组参数不参与计算（按件时重量不计、按重时件数不计），
    /// 但允许保留填写，便于运营切换计费方式时不必重填。
    /// </remarks>
    public enum FreightChargeTypeEnum
    {
        /// <summary>按件数：首件内一个价，超出部分按「续件单位」进位加价</summary>
        /// <remarks>适合小件商品（服饰、配件等），与主流商城的 express 型模板一致</remarks>
        ByPiece = 1,

        /// <summary>按重量：首重（kg）内一个价，超出部分按「续重单位」进位加价</summary>
        /// <remarks>重量取 Skus.Weight（千克）；SKU 重量为 0 时该行不贡献重量</remarks>
        ByWeight = 2
    }
}
