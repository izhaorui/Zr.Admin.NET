namespace ZR.Model.System.Dto
{
    public class SysLogininfoQueryDto : PagerInfo
    {
        public string Status { get; set; }
        public long? UserId { get; set; }
        public string Ipaddr { get; set; } = string.Empty; 
        public string UserName { get; set; }
        public DateTime? BeginTime { get; set; }
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// 第一步凭据类型（<see cref="LoginAuthType"/>）：password / sms_code / oauth。
        /// 传 oauth 时按前缀匹配，可一次查出所有 oauth:渠道 的记录
        /// </summary>
        public string AuthType { get; set; }

        /// <summary>
        /// 第二因子结果（<see cref="LoginTwoFactorType"/>）：none / totp / recovery_code / trusted_device。
        /// 审计时最常用的是 recovery_code —— 排查"恢复码到底被谁用了"
        /// </summary>
        public string TwoFactorType { get; set; }
    }

    public class SysLogininfoDto : SysBase
    {
        public int PageNum { get; set; }
        /// <summary>
        /// IP 地址
        /// </summary>
        public string Ipaddr { get; set; }
        /// <summary>
        /// 登录状态 0成功 1失败
        /// </summary>
        public string Status { get; set; }
        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }

    }
}
