namespace Infrastructure.Captcha
{
    /// <summary>
    /// 验证码提供程序抽象接口，定义生成与校验契约
    /// </summary>
    public interface ICaptchaProvider
    {
        /// <summary>
        /// 生成验证码
        /// </summary>
        /// <param name="id">唯一标识（如 uuid）</param>
        /// <param name="expiredSeconds">答案缓存过期秒数</param>
        /// <returns>验证码结果（含可直接渲染的 DataUrl）</returns>
        CaptchaResult Generate(string id, int expiredSeconds = 60);

        /// <summary>
        /// 校验验证码
        /// </summary>
        /// <param name="id">唯一标识（如 uuid）</param>
        /// <param name="code">用户输入的验证码</param>
        /// <param name="removeIfSuccess">校验成功后是否移除缓存（防止重放）</param>
        /// <returns>是否通过</returns>
        bool Validate(string id, string code, bool removeIfSuccess = true);
    }
}
