namespace ZR.Model.System
{
    /// <summary>
    /// 用户表
    /// </summary>
    [SugarTable("sys_user", "用户表")]
    [Tenant("0")]
    public class SysUser : SysBase
    {
        /// <summary>
        /// 用户id
        /// </summary>
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        public long UserId { get; set; }
        /// <summary>
        /// 登录用户名
        /// </summary>
        [SugarColumn(Length = 30, ColumnDescription = "用户账号", ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string UserName { get; set; }
        /// <summary>
        /// 用户昵称
        /// </summary>
        [SugarColumn(Length = 30, ColumnDescription = "用户昵称", ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string NickName { get; set; }
        /// <summary>
        /// 用户类型（00系统用户）
        /// </summary>
        [SugarColumn(Length = 2, ColumnDescription = "用户类型（00系统用户）", DefaultValue = "00")]
        public string UserType { get; set; } = "00";
        //[SugarColumn(IsOnlyIgnoreInsert = true)]
        public string Avatar { get; set; }
        [SugarColumn(Length = 50, ColumnDescription = "用户邮箱")]
        public string Email { get; set; }

        [JsonIgnore]
        [ExcelIgnore]
        [SugarColumn(Length = 100, ColumnDescription = "密码", ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string Password { get; set; }
        /// <summary>
        /// 手机号
        /// </summary>
        public string Phonenumber { get; set; }
        /// <summary>
        /// 用户性别（0男 1女 2未知）
        /// </summary>
        public int Sex { get; set; }

        /// <summary>
        /// 帐号状态（0正常 1停用）
        /// </summary>
        [ExcelIgnore]
        [SugarColumn(DefaultValue = "0")]
        public int Status { get; set; }

        /// <summary>
        /// 删除标志
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public DelFlagEnum DelFlag { get; set; }

        /// <summary>
        /// 最后登录IP
        /// </summary>
        [SugarColumn(IsOnlyIgnoreInsert = true)]
        public string LoginIP { get; set; }

        /// <summary>
        /// 最后登录时间
        /// </summary>
        [SugarColumn(IsOnlyIgnoreInsert = true)]
        [ExcelColumn(Name = "登录日期", Format = "yyyy-MM-dd HH:mm:ss")]
        public DateTime? LoginDate { get; set; }

        /// <summary>
        /// 部门Id
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public long DeptId { get; set; }

        /// <summary>
        /// 上级主管用户Id（仅存 Id，昵称展示时实时回查，避免改名后数据不一致）
        /// </summary>
        [SugarColumn(ColumnDescription = "上级主管用户Id", IsNullable = true)]
        public long? LeaderId { get; set; }

        public string Province { get; set; }
        public string City { get; set; }
        /// <summary>
        /// 最后密码更新时间
        /// </summary>
        public DateTime? PwdUpdateTime { get; set; }

        /// <summary>
        /// 是否启用双因子认证（TOTP）0未启用 1已启用
        /// </summary>
        [SugarColumn(DefaultValue = "0", ColumnDescription = "是否启用双因子认证")]
        public int TwoFactorEnabled { get; set; }

        /// <summary>
        /// TOTP 密钥（AES-GCM 加密存储，禁止明文落库/返回前端）
        /// </summary>
        [JsonIgnore]
        [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "TOTP密钥(加密)")]
        public string TwoFactorSecret { get; set; }

        /// <summary>
        /// 双因子认证绑定时间
        /// </summary>
        [SugarColumn(IsNullable = true, ColumnDescription = "双因子绑定时间")]
        public DateTime? TwoFactorBoundAt { get; set; }

        /// <summary>
        /// 恢复码（JSON 数组，元素为 SHA256 哈希，一次性使用）
        /// </summary>
        [JsonIgnore]
        [SugarColumn(Length = 2000, IsNullable = true, ColumnDescription = "恢复码哈希(JSON)")]
        public string TwoFactorRecoveryCodes { get; set; }

        /// <summary>
        /// 第二因子首选方法（取值见 <see cref="TwoFactorMethod"/>）。
        ///
        /// 只决定登录第二步**默认**展示哪种方式，不表达"是否可用"——可用方法由服务端按当前凭据计算。
        /// 预留字段：WebAuthn 落地前恒为 totp；存量行为空，按 totp 处理（列可空是为了避免给存量表加非空列）。
        /// </summary>
        [SugarColumn(Length = 16, IsNullable = true, ColumnDescription = "第二因子首选方法")]
        public string TwoFactorPreferredMethod { get; set; }

        [SugarColumn(IsIgnore = true)]
        public bool IsAdmin
        {
            get
            {
                return UserId == 1;
            }
        }
        /// <summary>
        /// 角色id集合
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public long[] RoleIds { get; set; }
        /// <summary>
        /// 岗位集合
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public int[] PostIds { get; set; }
    }

    /// <summary>
    /// 第二因子方法（存稳定英文码，前端映射中文）。
    ///
    /// 与登录日志的 <see cref="LoginTwoFactorType"/> 取值保持一致，
    /// 这样"可用方法列表"与"实际用了哪种方法"可以直接对照，排查时不需要再做一层翻译。
    /// </summary>
    public static class TwoFactorMethod
    {
        /// <summary>认证器动态码（TOTP）</summary>
        public const string Totp = "totp";

        /// <summary>
        /// WebAuthn / Passkey（安全密钥、指纹、Face ID）。
        /// **尚未实现**：常量与"按方法列表渲染入口"的前端结构已预留，落地时服务端把它加进可用列表即可。
        /// </summary>
        public const string WebAuthn = "webauthn";

        /// <summary>默认方法（库中为空或值不可用时的回退）</summary>
        public const string Default = Totp;
    }
}
