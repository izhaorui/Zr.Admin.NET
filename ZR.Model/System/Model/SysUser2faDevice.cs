namespace ZR.Model.System
{
    /// <summary>
    /// 双因子认证可信设备（"记住此设备"）。
    ///
    /// 设备标识取自登录请求携带的 ClientId（前端 fingerprintjs 的 visitorId），
    /// 在 TrustUntil 之前该设备登录可跳过 TOTP 验证；吊销即删除记录。
    /// 独立成表而非塞进 sys_user 的 JSON 列：需要按设备单独吊销、按用户/过期时间查询，JSON 列无法满足。
    /// </summary>
    [SugarTable("sys_user_2fa_device", "双因子可信设备")]
    [Tenant("0")]
    public class SysUser2faDevice
    {
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long Id { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        public long UserId { get; set; }

        /// <summary>
        /// 设备标识（前端 ClientId）
        /// </summary>
        [SugarColumn(Length = 64, ColumnDescription = "设备标识")]
        public string DeviceId { get; set; }

        /// <summary>
        /// 设备名称（浏览器/系统，仅用于展示）
        /// </summary>
        [SugarColumn(Length = 200, IsNullable = true, ColumnDescription = "设备名称")]
        public string DeviceName { get; set; }

        /// <summary>
        /// 信任截止时间，超过即需重新验证 TOTP
        /// </summary>
        [SugarColumn(ColumnDescription = "信任截止时间")]
        public DateTime TrustUntil { get; set; }

        /// <summary>
        /// 可信时登录 IP（仅记录，不校验）
        /// </summary>
        [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "登录IP")]
        public string LoginIp { get; set; }

        /// <summary>
        /// 最后使用时间（每次免验证登录刷新，便于用户识别僵尸设备）
        /// </summary>
        [SugarColumn(ColumnDescription = "最后使用时间")]
        public DateTime LastUsedAt { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [SugarColumn(ColumnDescription = "创建时间")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
