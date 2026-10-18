namespace ZR.ServiceCore.Model.Dto
{
    public class PhoneLoginDto
    {
        /// <summary>
        /// 验证码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 唯一标识
        /// </summary>
        public string Uuid { get; set; } = "";
        public string LoginIP { get; set; }
        [Required(ErrorMessage = "手机号不能为空")]
        public string PhoneNum { get; set; }
        /// <summary>
        /// 手机短信验证码
        /// </summary>
        //[Required(ErrorMessage = "短信验证码不能为空")]
        public string PhoneCode { get; set; }
        /// <summary>
        /// 0、登录 1、绑定
        /// </summary>
        public int SendType { get; set; }
        /// <summary>
        /// 租户id
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// 设备标识（前端 fingerprintjs 的 visitorId）。仅用于设备列表展示与审计，不参与"记住此设备"信任判定
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// 设备凭据（勾选"记住此设备"后服务端签发、客户端保存并在下次登录带回）。
        /// 命中信任设备时可跳过双因子第二步
        /// </summary>
        public string DeviceToken { get; set; }
    }
}
