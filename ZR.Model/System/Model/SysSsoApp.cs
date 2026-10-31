namespace ZR.Model.System
{
    /// <summary>
    /// SSO 应用接入（外部系统跳转本系统登录，换取用户基本信息）。
    ///
    /// 平台级配置，放主库（IMainDbEntity）：
    /// /sso/token 是匿名接口且无租户上下文，必须能按 AppId 全局查到应用，
    /// 若放租户库会因"不知道该去哪个库查应用"而无法兑换授权码。
    /// 租户隔离由授权码载荷中的 TenantId 显式保证（见 SsoService.ExchangeCode）。
    /// </summary>
    [SugarTable("sys_sso_app", "SSO应用接入表")]
    [SugarIndex("uk_sys_sso_app_appid", nameof(AppId), OrderByType.Asc, true)]
    [Tenant("0")]
    public class SysSsoApp : SysBase, IMainDbEntity
    {
        /// <summary>
        /// 主键
        /// </summary>
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long Id { get; set; }

        /// <summary>
        /// 应用标识，对外公开（用于拼接授权链接）
        /// </summary>
        [SugarColumn(Length = 64, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string AppId { get; set; }

        /// <summary>
        /// 应用密钥哈希：SHA256(AppId:Secret)。
        /// 明文只在创建/重置时返回一次，不入库、不返回列表、不写日志。
        /// </summary>
        [SugarColumn(Length = 128)]
        public string AppSecretHash { get; set; }

        /// <summary>
        /// 应用名称（展示在登录页"XX 请求登录"）
        /// </summary>
        [SugarColumn(Length = 100)]
        public string AppName { get; set; }

        /// <summary>
        /// 允许的回调地址，换行分隔。
        /// 匹配必须精确全等（含协议、域名、端口、路径），禁止前缀/包含匹配——否则可被改写成攻击者域名。
        /// </summary>
        [SugarColumn(Length = 2000)]
        public string RedirectUris { get; set; }

        /// <summary>
        /// 允许返回的扩展字段，逗号分隔：profile,email,phone。
        /// profile = 昵称/头像/部门（展示类，合并为一个授权项）；
        /// email、phone 属个人信息，各自独立授权。
        /// 为空时只返回 userId/userName/tenantId 三个基础字段。
        /// </summary>
        [SugarColumn(Length = 500)]
        public string AllowFields { get; set; }

        /// <summary>
        /// 状态：0正常 1停用
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int Status { get; set; }

        /// <summary>
        /// 过期时间，为空表示不过期
        /// </summary>
        [SugarColumn(IsNullable = true)]
        public DateTime? ExpireTime { get; set; }
    }
}