namespace ZR.OAuth
{
    /// <summary>
    /// 支持的第三方渠道标识。
    ///
    /// 取值沿用上游库的 authSource 命名（大写枚举名），前端与配置都用同一套字符串，
    /// 避免「前端叫 wx、后端叫 WECHAT_OPEN」这类映射错误。
    /// </summary>
    public static class OAuthSources
    {
        public const string WeChatOpen = "WECHAT_OPEN";
        public const string WeChatMp = "WECHAT_MP";
        public const string WeChatEnterprise = "WECHAT_ENTERPRISE";
        public const string WeChatEnterpriseScan = "WECHAT_ENTERPRISE_SCAN";
        public const string WeChatMini = "WECHAT_MINI";

        public const string DingTalk = "DINGTALK";
        public const string DingTalkScan = "DINGTALK_SCAN";

        public const string Github = "GITHUB";
        public const string Gitee = "GITEE";
        public const string Gitlab = "GITLAB";
        public const string Qq = "QQ";
        public const string Weibo = "WEIBO";
        public const string Douyin = "DOUYIN";
        public const string Microsoft = "MICROSOFT";
        public const string Google = "GOOGLE";
        public const string Linkedin = "LINKEDIN";
        public const string Baidu = "BAIDU";
        public const string Huawei = "HUAWEI";
        public const string AlipayMp = "ALIPAY_MP";

        /// <summary>判断是否为小程序登录（不走浏览器跳转，直接 code 换取）</summary>
        public static bool IsMiniProgram(string authSource) => WeChatMini.Equals(authSource, StringComparison.OrdinalIgnoreCase);
    }
}