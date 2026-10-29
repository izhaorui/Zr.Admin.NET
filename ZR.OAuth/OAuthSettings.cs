using Infrastructure;
using ZR.Model.System;

namespace ZR.OAuth
{
    /// <summary>
    /// 第三方登录配置，对应 appsettings 的 OAuth 节点。
    ///
    /// 多租户：Clients 的 Key 支持两种写法
    /// - "WECHAT_ENTERPRISE"           所有租户共用一个应用
    /// - "WECHAT_ENTERPRISE@1001"      租户 1001 专用应用（优先命中）
    /// 客户各自申请应用是刚需，没有这层后缀就只能改代码。
    /// </summary>
    public sealed class OAuthSettings
    {
        /// <summary>总开关，灰度与回滚用：关闭后所有入口拒绝，绑定数据保留</summary>
        public bool Enabled { get; set; }

        /// <summary>state 有效期（分钟），给用户留出扫码/授权的时间，不宜超过 15</summary>
        public int StateMinutes { get; set; } = 10;

        /// <summary>
        /// 回调页默认路径。前端路由是 history 模式（createWebHistory，路由 /sociallogin），
        /// 不能写成 /#/sociallogin —— hash 会被浏览器当锚点，路由不会跳转，回调页白屏。
        /// </summary>
        public const string DefaultFrontendCallbackPath = "/sociallogin";

        /// <summary>
        /// 前端回调页地址，如 https://admin.example.com/sociallogin。
        ///
        /// 第三方只认回调到后端的地址，而后端不能把 token 拼在 302 的 URL 上（会进浏览器历史、
        /// Referer 和网关日志），所以后端只做一次 302 到前端页面，token 由前端 POST 换取。
        /// 该值必须来自配置，绝不能取自请求参数，否则就是开放重定向。
        ///
        /// 单租户/统一入口场景配这一项即可；SaaS 下各租户前端域名不同，这里只能当默认值，
        /// 实际地址优先按 <see cref="FrontendCallbackUrls"/> 覆盖，再按租户绑定域名 +
        /// <see cref="FrontendCallbackPath"/> 推导（见 SysThirdAccountService.ResolveFrontendCallbackUrl）。
        /// </summary>
        public string FrontendCallbackUrl { get; set; }

        /// <summary>
        /// 回调页路径（默认 /sociallogin）。与租户绑定域名（sys_tenant.Domain）拼成实际跳转地址，
        /// 使"在哪个租户域名登录、就回哪个租户域名"，不必给每个租户重复配一遍完整地址。
        /// </summary>
        public string FrontendCallbackPath { get; set; } = DefaultFrontendCallbackPath;

        /// <summary>
        /// 按租户覆盖回调页完整地址，键为 TenantId。
        /// 用于域名与 sys_tenant.Domain 不一致的场景（独立端口、子路径部署、一租户多域名）。
        /// 优先级最高，值只来自配置，不受请求参数影响。
        /// </summary>
        public Dictionary<string, string> FrontendCallbackUrls { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, OAuthClientOptions> Clients { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 未绑定用户的处置策略。默认 <see cref="ThirdAutoCreateMode.BindOnly"/>：
        /// 第三方登录只做"已绑定用户的快捷入口"，新用户仍须由管理员在系统内建立，
        /// 否则任何人都能凭一个微信/钉钉身份进入系统，账号准入形同虚设。
        /// </summary>
        public ThirdAutoCreateMode AutoCreate { get; set; } = ThirdAutoCreateMode.BindOnly;

        /// <summary>
        /// 自动建号时授予的角色ID。为空视为"不开启自动建号"（即使 <see cref="AutoCreate"/>
        /// 配成 AutoCreate）：没有角色的账号登录后是空白后台，建出来只会累加无人认领的僵尸用户。
        /// </summary>
        public long[] AutoCreateRoleIds { get; set; } = [];

        /// <summary>自动建号时归属部门，0 表示未分配</summary>
        public long AutoCreateDeptId { get; set; }

        /// <summary>
        /// 取某租户在某渠道下的应用配置：先找「渠道@租户」，再回落共用配置。
        /// </summary>
        public OAuthClientOptions Resolve(string authSource, string tenantId)
        {
            if (string.IsNullOrWhiteSpace(authSource))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(tenantId)
                && Clients.TryGetValue($"{authSource}@{tenantId}", out OAuthClientOptions scoped))
            {
                return scoped;
            }

            return Clients.TryGetValue(authSource, out OAuthClientOptions shared) ? shared : null;
        }

        /// <summary>每次读取都重新绑定，避免改了 appsettings 必须重启</summary>
        public static OAuthSettings Current => AppSettings.Bind("OAuth", new OAuthSettings());
    }

    /// <summary>单个渠道的应用参数</summary>
    public sealed class OAuthClientOptions
    {
        public string ClientId { get; set; }

        /// <summary>应用密钥：只在服务端使用，禁止下发前端、禁止写日志</summary>
        public string ClientSecret { get; set; }

        /// <summary>回调地址，必须与开放平台后台登记的完全一致（含协议与端口）</summary>
        public string RedirectUri { get; set; }

        public string Scope { get; set; }

        /// <summary>企业微信专用：网页应用 AgentId</summary>
        public string AgentId { get; set; }
    }
}