namespace ZR.Model.System
{
    /// <summary>
    /// 第三方账号绑定表。
    ///
    /// 设计要点：
    /// 1) 唯一索引建在 <see cref="AuthSource"/> + <see cref="ThirdKey"/> 上，而不是只按 openid。
    ///    不同渠道的 openid 可能撞车（都是 28 位随机串），只按第三方 ID 查会绑到别人的账号上。
    /// 2) <see cref="ThirdKey"/> 存"UnionId 优先、回落 OpenId"的结果。为什么不直接查 UnionId：
    ///    有 unionid 时它是稳定键，没有时要用 openid，若查询时写 (UnionId = x OR OpenId = x)，
    ///    索引失效且语义混乱。落库时归一化成一个键，查询与唯一约束都简单。
    /// 3) 解绑不物理删除，置 <see cref="Status"/>=1。绑定记录是"谁在什么时间用哪个身份登录过"的
    ///    审计线索，删掉就查不到历史；重新绑定时把旧记录复活即可，不会与唯一索引冲突。
    /// </summary>
    [SugarTable("sys_third_account", "第三方账号绑定")]
    // 唯一约束落在 (AuthSource, ThirdKey)：同一渠道的同一第三方身份只能有一条记录。
    // 解绑后再绑定靠"复活"旧行避免撞索引（见 SysThirdAccountService.SaveBind），
    // 并发下两个请求同时判空插入时，由这个索引兜住重复数据。
    [SugarIndex("uk_sys_third_account_key", nameof(AuthSource), OrderByType.Asc, true)]
    [SugarIndex("uk_sys_third_account_key", nameof(ThirdKey), OrderByType.Asc, true)]
    [Tenant("0")]
    public class SysThirdAccount : SysBase
    {
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long Id { get; set; }

        /// <summary>
        /// 关联的本系统用户ID
        /// </summary>
        [SugarColumn(ColumnDescription = "用户ID")]
        public long UserId { get; set; }

        /// <summary>
        /// 渠道标识，取值见 ZR.ServiceCore 的 OAuthSources（如 WECHAT_ENTERPRISE）
        /// </summary>
        [SugarColumn(Length = 32, ColumnDescription = "第三方渠道")]
        public string AuthSource { get; set; }

        /// <summary>
        /// 渠道内唯一标识：UnionId 优先，无 UnionId 时用 OpenId
        /// </summary>
        [SugarColumn(Length = 128, ColumnDescription = "渠道内唯一标识")]
        public string ThirdKey { get; set; }

        /// <summary>
        /// 第三方 openid（换应用即变，仅存档排查用，不参与绑定判定）
        /// </summary>
        [SugarColumn(Length = 128, IsNullable = true, ColumnDescription = "openid")]
        public string OpenId { get; set; }

        /// <summary>
        /// 第三方 unionid（同主体跨应用稳定）
        /// </summary>
        [SugarColumn(Length = 128, IsNullable = true, ColumnDescription = "unionid")]
        public string UnionId { get; set; }

        /// <summary>
        /// 最近一次同步的第三方昵称（仅展示，不覆盖本系统昵称）
        /// </summary>
        [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "第三方昵称")]
        public string NickName { get; set; }

        [SugarColumn(Length = 512, IsNullable = true, ColumnDescription = "第三方头像")]
        public string Avatar { get; set; }

        /// <summary>
        /// 第三方邮箱（部分平台不返回）
        /// </summary>
        [SugarColumn(Length = 128, IsNullable = true, ColumnDescription = "第三方邮箱")]
        public string Email { get; set; }

        [SugarColumn(ColumnDescription = "绑定时间")]
        public DateTime BindTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 最近一次用该身份登录的时间（由此可识别长期不用的僵尸绑定）
        /// </summary>
        [SugarColumn(IsNullable = true, ColumnDescription = "最后登录时间")]
        public DateTime? LastLoginTime { get; set; }

        /// <summary>
        /// 绑定状态（0正常 1已解绑）。解绑不删记录，保留审计线索
        /// </summary>
        [SugarColumn(DefaultValue = "0", ColumnDescription = "绑定状态（0正常 1已解绑）")]
        public int Status { get; set; }
    }

    /// <summary>
    /// 第三方账号绑定状态
    /// </summary>
    public static class ThirdAccountStatus
    {
        /// <summary>正常绑定</summary>
        public const int Normal = 0;

        /// <summary>已解绑（记录保留，可被重新绑定复活）</summary>
        public const int Unbound = 1;
    }

    /// <summary>
    /// 第三方登录可用的建号策略。
    ///
    /// 默认 <see cref="BindOnly"/>：首次第三方登录**不建号**，要求用户先用账号密码登录再绑定。
    /// 自动建号会绕过管理员对账号的控制（谁能进系统、有什么角色），
    /// 且第三方昵称不可信、可能被冒名，因此只在明确需要 C 端场景时才开启。
    /// </summary>
    public enum ThirdAutoCreateMode
    {
        /// <summary>仅允许已绑定用户登录，未绑定返回"需先绑定"</summary>
        BindOnly = 0,

        /// <summary>首次登录自动建号并绑定</summary>
        AutoCreate = 1
    }
}