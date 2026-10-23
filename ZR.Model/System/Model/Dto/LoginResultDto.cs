namespace ZR.Model.System.Dto
{
    /// <summary>
    /// 登录流程结果状态。
    ///
    /// 刻意由 Service 判定、Controller 负责映射成业务码与响应体：
    /// Service 若直接产出 <c>IActionResult</c>/<c>ApiResult</c>，整条流程就离不开 HttpContext，
    /// 无法用进程内测试覆盖 —— 而登录第二步恰恰是最需要用例锁死的流程。
    /// </summary>
    public enum LoginResultStatus
    {
        /// <summary>已签发 token</summary>
        Success = 0,

        /// <summary>需要双因子第二步：已下发票据，**未**签发 token</summary>
        TwoFactorRequired = 1,

        /// <summary>票据不存在 / 已过期 / 已被消费</summary>
        TicketExpired = 2,

        /// <summary>用户不存在或已禁用</summary>
        AccountUnavailable = 3,

        /// <summary>
        /// 第三方身份尚未绑定本系统账号，且未开启自动建号。
        /// 与 AccountUnavailable 区分：这是"需要用户先登录再绑定"，不是失败。
        /// </summary>
        BindRequired = 4
    }

    /// <summary>
    /// 登录流程结果（账号密码登录、手机号登录、双因子第二步共用）。
    /// </summary>
    public class LoginResultDto
    {
        public LoginResultStatus Status { get; set; }

        /// <summary>签发成功的 JWT（仅 <see cref="LoginResultStatus.Success"/>）</summary>
        public string Token { get; set; }

        /// <summary>
        /// 本次服务端新签发的设备凭据明文，仅"记住此设备"时非空，且只返回这一次
        /// （服务端只存哈希）。客户端须保存并在下次登录时带回。
        /// </summary>
        public string DeviceToken { get; set; }

        /// <summary>异地登录提醒文案，无则空</summary>
        public string Notice { get; set; }

        /// <summary>一次性票据（仅 <see cref="LoginResultStatus.TwoFactorRequired"/>）</summary>
        public string Ticket { get; set; }

        /// <summary>票据有效期（分钟）</summary>
        public int ExpireMinutes { get; set; }

        /// <summary>可用第二因子方法（前端按此渲染入口）</summary>
        public List<string> Methods { get; set; } = new();

        /// <summary>首选方法（第二步默认展示哪种方式）</summary>
        public string PreferredMethod { get; set; }
    }
}
