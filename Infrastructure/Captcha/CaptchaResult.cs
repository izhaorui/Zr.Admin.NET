namespace Infrastructure.Captcha
{
    /// <summary>
    /// 验证码生成结果
    /// </summary>
    public class CaptchaResult
    {
        /// <summary>
        /// 验证码明文（调试/日志用，前端无需使用）
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 可直接用于 &lt;img src&gt; 的 data URL，例如 data:image/svg+xml;base64,...
        /// </summary>
        public string DataUrl { get; set; }

        /// <summary>
        /// 内容类型，例如 image/svg+xml
        /// </summary>
        public string ContentType { get; set; }
    }
}
