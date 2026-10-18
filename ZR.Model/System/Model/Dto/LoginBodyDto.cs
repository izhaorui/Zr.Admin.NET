namespace ZR.Model.System.Dto
{
    public class LoginBodyDto
    {
        /// <summary>
        /// 用户名
        /// </summary>
        [Required(ErrorMessage = "用户名不能为空")]
        public string Username { get; set; }

        /// <summary>
        /// 用户密码
        /// </summary>
        [Required(ErrorMessage = "密码不能为空")]
        public string Password { get; set; }

        /// <summary>
        /// 验证码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 唯一标识
        /// </summary>
        public string Uuid { get; set; } = "";
        public string LoginIP { get; set; }

        /// <summary>
        /// 设备标识（前端 fingerprintjs 的 visitorId）。仅用于设备列表展示与审计，不参与"记住此设备"信任判定
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// 设备凭据（勾选"记住此设备"后服务端签发、客户端保存并在下次登录带回）。
        /// 命中信任设备时可跳过双因子第二步；为空表示新设备/未记住的设备
        /// </summary>
        public string DeviceToken { get; set; }
        /// <summary>
        /// 租户ID
        /// </summary>
        public string TenantId { get; set; }
    }
}
