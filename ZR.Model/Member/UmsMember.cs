namespace ZR.Model.Member
{
    /// <summary>
    /// 通用会员表（C 端统一身份，与后台账号 sys_user 完全隔离）。
    /// 库归属：使用 [Tenant("MallDb")]。非 SaaS 解析为 App.Configuration["MallDb"]（商城库）；
    /// SaaS 由 BaseRepository 直接路由到当前租户库，天然满足"与商城同库"，便于同库模块复用。
    /// </summary>
    [SugarTable("ums_member", "通用会员表")]
    [SugarIndex("uk_ums_member_phone", nameof(Phone), OrderByType.Asc, true)]
    [Tenant("MallDb")]
    public class UmsMember
    {
        /// <summary>
        /// 会员Id
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// 登录手机号（唯一）
        /// </summary>
        [SugarColumn(Length = 20, ColumnDescription = "登录手机号")]
        public string Phone { get; set; }

        /// <summary>
        /// 昵称
        /// </summary>
        [SugarColumn(Length = 50, IsNullable = true, ColumnDescription = "昵称")]
        public string NickName { get; set; }

        /// <summary>
        /// 头像地址
        /// </summary>
        [SugarColumn(Length = 255, IsNullable = true, ColumnDescription = "头像地址")]
        public string Avatar { get; set; }

        /// <summary>
        /// 性别 0未知 1男 2女
        /// </summary>
        [SugarColumn(DefaultValue = "0", ColumnDescription = "性别")]
        public int Gender { get; set; }

        /// <summary>
        /// 账号状态 0正常 1禁用
        /// </summary>
        [SugarColumn(DefaultValue = "0", ColumnDescription = "账号状态")]
        public int Status { get; set; }

        /// <summary>
        /// 微信 OpenId（通用化预留，暂未启用）
        /// </summary>
        [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "微信OpenId")]
        public string OpenId { get; set; }

        /// <summary>
        /// 微信 UnionId（通用化预留，暂未启用）
        /// </summary>
        [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "微信UnionId")]
        public string UnionId { get; set; }

        /// <summary>
        /// 会员等级（通用化预留，暂未启用）
        /// </summary>
        [SugarColumn(DefaultValue = "0", ColumnDescription = "会员等级")]
        public int MemberLevel { get; set; }

        /// <summary>
        /// 注册时间
        /// </summary>
        [SugarColumn(ColumnDescription = "注册时间")]
        public DateTime RegisterTime { get; set; }

        /// <summary>
        /// 最近登录时间
        /// </summary>
        [SugarColumn(IsNullable = true, ColumnDescription = "最近登录时间")]
        public DateTime? LastLoginTime { get; set; }

        /// <summary>
        /// 最近登录IP
        /// </summary>
        [SugarColumn(Length = 64, IsNullable = true, ColumnDescription = "最近登录IP")]
        public string LastLoginIp { get; set; }

        /// <summary>
        /// 是否删除 0未删除 1已删除
        /// </summary>
        [SugarColumn(DefaultValue = "0", ColumnDescription = "是否删除")]
        public int IsDelete { get; set; }
    }
}
