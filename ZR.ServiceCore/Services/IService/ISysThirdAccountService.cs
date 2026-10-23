using System.Threading.Tasks;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 第三方账号绑定与第三方登录。
    ///
    /// 分工边界：本服务负责"第三方身份 → 本系统用户"的解析与绑定，
    /// 签发 token 一律交给 <see cref="ISysLoginService.SignIn"/>（含 2FA、单会话、设备登记、审计）。
    /// </summary>
    public interface ISysThirdAccountService : IBaseService<SysThirdAccount>
    {
        /// <summary>
        /// 生成授权跳转地址（浏览器场景）。state 的上下文由服务端写入缓存，
        /// 租户与设备标识不经过 URL，避免被篡改。
        /// </summary>
        /// <param name="dto">渠道、租户、设备标识</param>
        /// <param name="bindUserId">为空/0 表示登录场景；非 0 表示已登录用户绑定场景</param>
        string BuildAuthorizeUrl(ThirdAuthorizeDto dto, long bindUserId = 0);

        /// <summary>
        /// App/小程序预发 state：端内只有 code，没有授权页跳转，由服务端先发 state。
        /// 返回值直接交给端上，回调时与 code 一并带回。
        /// </summary>
        string IssueAppState(ThirdStateDto dto);

        /// <summary>
        /// 回调登录：校验 state → 用 code 换身份 → 查绑定/建号 → 双因子闸门 → 签发 token。
        /// </summary>
        Task<LoginResultDto> LoginByCode(ThirdLoginDto dto);

        /// <summary>
        /// 已登录用户绑定第三方账号。
        /// </summary>
        Task Bind(ThirdBindDto dto, long userId);

        /// <summary>
        /// 解绑（置为已解绑，保留记录）
        /// </summary>
        int Unbind(string authSource, string tenantId, long userId);

        /// <summary>
        /// 当前用户已绑定的第三方账号（不含第三方原始 ID）
        /// </summary>
        List<ThirdAccountDto> GetUserBinds(long userId);

        /// <summary>
        /// 浏览器回调要 302 到的前端页面地址，按租户解析（SaaS 下各租户域名不同）。
        /// 取值优先级：按租户显式配置 → 租户绑定域名 + 回调路径 → 全局 FrontendCallbackUrl。
        /// </summary>
        /// <param name="tenantId">state 载荷里的租户，不能来自回调请求参数</param>
        string ResolveFrontendCallbackUrl(string tenantId);
    }
}