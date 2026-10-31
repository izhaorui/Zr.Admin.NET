namespace ZR.Model.System.Dto
{
    /// <summary>
    /// SSO 应用查询对象
    /// </summary>
    public class SsoAppQueryDto : PagerInfo
    {
        public string AppId { get; set; }
        public string AppName { get; set; }
        public int? Status { get; set; }
    }

    /// <summary>
    /// SSO 应用新增/编辑入参。
    /// 不接收 AppSecret：密钥只能由服务端生成，编辑不会改动密钥。
    /// </summary>
    public class SsoAppEditDto
    {
        public long Id { get; set; }
        public string AppName { get; set; }
        /// <summary>多个回调地址用换行分隔，精确全等匹配</summary>
        public string RedirectUris { get; set; }
        /// <summary>可选扩展字段：profile（昵称/头像/部门）,email,phone</summary>
        public string AllowFields { get; set; }
        public int Status { get; set; }
        public DateTime? ExpireTime { get; set; }
        public string Remark { get; set; }
    }

    /// <summary>
    /// 创建应用 / 重置密钥后的返回。
    /// AppSecret 为明文，仅此一次返回，服务端只存哈希。
    /// </summary>
    public class SsoAppSecretDto
    {
        public long Id { get; set; }
        public string AppId { get; set; }
        public string AppSecret { get; set; }
    }

    /// <summary>
    /// 授权页预校验结果（登录页展示"XX 请求登录"用）
    /// </summary>
    public class SsoAuthorizeInfoDto
    {
        public string AppId { get; set; }
        public string AppName { get; set; }
        public string RedirectUri { get; set; }
        public string State { get; set; }
    }

    /// <summary>
    /// 已登录用户换取一次性授权码
    /// </summary>
    public class SsoCodeRequestDto
    {
        public string AppId { get; set; }
        public string RedirectUri { get; set; }
        public string State { get; set; }
    }

    public class SsoCodeResultDto
    {
        public string Code { get; set; }
        /// <summary>有效期（秒）</summary>
        public int ExpiresIn { get; set; }
        /// <summary>回跳地址（前端直接 window.location 跳转，避免前端自行拼接出错）</summary>
        public string RedirectUri { get; set; }
        public string State { get; set; }
    }

    /// <summary>
    /// 外部系统服务端用授权码换用户信息
    /// </summary>
    public class SsoTokenRequestDto
    {
        public string AppId { get; set; }
        public string AppSecret { get; set; }
        public string Code { get; set; }
    }

    /// <summary>
    /// 返回给外部系统的用户基本信息。
    /// userId/userName/tenantId 固定返回；profile 开启时下发昵称、头像、部门；
    /// email、phone 各自独立授权。未开启的字段为 null。
    /// 刻意不包含密码、角色、权限等隐私字段。
    /// </summary>
    public class SsoUserInfoDto
    {
        public long UserId { get; set; }
        public string UserName { get; set; }
        public string NickName { get; set; }
        /// <summary>用户所属租户（多租户下外部系统据此区分身份域）</summary>
        public string TenantId { get; set; }
        public string Avatar { get; set; }
        public long? DeptId { get; set; }
        public string DeptName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }

    /// <summary>
    /// 授权码载荷：缓存 60 秒、一次性消费。
    /// 绑定 AppId + RedirectUri + UserId + TenantId，兑换时逐项复核。
    /// </summary>
    public class SsoCodePayload
    {
        public string AppId { get; set; }
        public string RedirectUri { get; set; }
        public long UserId { get; set; }
        public string TenantId { get; set; }
    }
}