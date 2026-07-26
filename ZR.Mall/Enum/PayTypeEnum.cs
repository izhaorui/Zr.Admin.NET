namespace ZR.Mall.Enum
{
    /// <summary>
    /// 支付类型（支付方式）
    /// </summary>
    public enum PayTypeEnum
    {
        /// <summary>
        /// 模拟支付（开发/联调，不产生真实扣款）
        /// </summary>
        Mock = 0,
        /// <summary>
        /// 微信 H5 支付（外浏览器调起微信）
        /// </summary>
        Wechat = 1,
        /// <summary>
        /// 支付宝（预留，暂未开通）
        /// </summary>
        Alipay = 2
    }
}
