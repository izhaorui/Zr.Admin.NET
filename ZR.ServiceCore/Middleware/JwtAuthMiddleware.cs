using Infrastructure;
using Infrastructure.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using ZR.Common;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.Middleware
{
    /// <summary>
    /// jwt认证中间件
    /// </summary>
    public class JwtAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JwtAuthMiddleware> _logger;
        private readonly OptionsSetting _options;
        private static readonly string[] _whitelistPaths = Array.Empty<string>();

        // Token 刷新阈值（分钟）
        private int TOKEN_REFRESH_THRESHOLD_MINUTES = 5;

        public JwtAuthMiddleware(RequestDelegate next, ILogger<JwtAuthMiddleware> logger, IOptions<OptionsSetting> options)
        {
            _next = next;
            _logger = logger;
            _options = options.Value;
            TOKEN_REFRESH_THRESHOLD_MINUTES = _options.JwtSettings.RefreshTokenTime;
        }

        public async Task InvokeAsync(HttpContext context, ISysUserService sysUserService, ISysTenantService sysTenantService)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            // 注意：不得以"路径含点即静态资源"作为放行依据。
            // 静态文件由 Program.cs 中位于 UseRouting 之前的 UseStaticFiles 处理，不会进入本中间件；
            // 而路由参数完全可能含点（如 system/CommonLang/key/a.b），一旦放行，
            // 下游 ActionPermissionFilter 在未登录（info == null）时不会做任何权限校验，
            // 等于任何带 string 路由参数的接口都可在未登录状态下访问。
            //_logger.LogInformation($"处理请求: {path}");
            // 白名单路径检查
            if (_whitelistPaths.Any(p => !string.IsNullOrEmpty(p) && path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            // 未匹配到任何端点（如 404、非 API 请求），无需鉴权，直接放行
            var endpoint = context.GetEndpoint();
            if (endpoint == null)
            {
                Console.WriteLine($"路由访问404 path={path}");
                await _next(context);
                return;
            }

            // 标注了 [AllowAnonymous] 的公开端点（如登录、验证码），跳过鉴权
            var allowAnonymous = endpoint.Metadata.GetMetadata<AllowAnonymousAttribute>() != null;
            if (allowAnonymous)
            {
                Console.WriteLine($"匿名端点被放行path={path}");
                await _next(context);
                return;
            }

            string ip = HttpContextExtension.GetClientUserIp(context);
            string url = context.Request.Path;
            string osType = context.Request.Headers["os"];

            var loginUser = JwtUtil.GetLoginUser(context);
            if (loginUser == null)
            {
                string msg = $"请求访问[{url}]失败，Token无效或未登录";
                _logger.LogWarning("{Message}, ip={Ip}", msg, ip);
                await context.Response.WriteAsJsonAsync(ApiResult.Error(ResultCode.DENY, msg));
                return;
            }

            // 单设备登录：校验当前 token 携带的会话ID是否仍为服务端有效会话，否则视为被新设备挤下线
            if (_options.SingleLogin)
            {
                var cachedSession = CacheService.GetUserSession(loginUser.UserId);
                if (!string.IsNullOrEmpty(cachedSession) && cachedSession != loginUser.SessionId)
                {
                    string msg = $"账号[{loginUser.UserName}]已在其他设备登录，当前会话已失效";
                    _logger.LogWarning("{Message}, ip={Ip}", msg, ip);
                    await context.Response.WriteAsJsonAsync(ApiResult.Error(ResultCode.FORCE_LOGOUT, msg));
                    return;
                }
            }

            // 状态复核：JWT 无状态，禁用用户 / 停服（过期）租户后，存量 token 在有效期内仍能通过验签，
            // 且下方会自动续期，等于永久有效。必须放在续期之前判定 —— 先续期再拒绝毫无意义。
            // 状态走 60 秒缓存（禁用/停服时主动清除、未命中回查库）；查询异常时放行，避免 DB 抖动踢掉全体在线用户。
            if (!sysUserService.IsUserActive(loginUser.UserId))
            {
                string msg = $"账号[{loginUser.UserName}]已被禁用或删除，请联系管理员";
                _logger.LogWarning($"请求访问[{url}]失败：{msg}, ip={ip}");
                CacheService.RemoveUserSession(loginUser.UserId);
                await context.Response.WriteAsJsonAsync(ApiResult.Error(ResultCode.DENY, msg));
                return;
            }

            // 租户状态复核：停服/到期后存量 token 立即失效。主租户与未启用多租户时 IsTenantActive 直接返回 true。
            if (!string.IsNullOrWhiteSpace(loginUser.TenantId) && !sysTenantService.IsTenantActive(loginUser.TenantId))
            {
                string msg = "当前租户已停用或已到期，请联系平台管理员";
                _logger.LogWarning($"请求访问[{url}]失败：{msg}, tenant={loginUser.TenantId}, ip={ip}");
                await context.Response.WriteAsJsonAsync(ApiResult.Error(ResultCode.DENY, msg));
                return;
            }

            var now = DateTime.UtcNow;

            // 绝对最长存活时间：下面的"临期自动续期"没有次数上限，活跃会话每次都能再续一整个 Expire，
            // 等于 token 可永久有效（被盗 token 只要持续使用就不会失效，改密码也无法吊销）。
            // 这里给会话一个硬上限：超过后不再续期，直接要求重新登录。
            var maxLifetime = _options.JwtSettings.MaxLifetime;
            if (maxLifetime > 0)
            {
                // 升级前签发的旧 token 没有 FirstLoginTime：按"过期时间 - 有效期"反推本次签发时间。
                // 回填后本次续期出的新 token 会带上该值，后续按真实起点计时，不会永久豁免。
                var firstLoginTime = loginUser.FirstLoginTime
                    ?? loginUser.ExpireTime.AddMinutes(-_options.JwtSettings.Expire);
                loginUser.FirstLoginTime = firstLoginTime;

                if (now - firstLoginTime >= TimeSpan.FromMinutes(maxLifetime))
                {
                    var lifetimeText = maxLifetime >= 1440 && maxLifetime % 1440 == 0
                        ? $"{maxLifetime / 1440} 天"
                        : $"{maxLifetime} 分钟";
                    var msg = $"登录状态已超过最长有效期（{lifetimeText}），请重新登录";
                    _logger.LogWarning($"{msg}，UserId={loginUser.UserId}, ip={ip}");
                    CacheService.RemoveUserSession(loginUser.UserId);
                    // 用 401 而非 480：前端仅对 401 触发"重新登录"流程（480 只弹一条提示，用户会卡在页面上）
                    await context.Response.WriteAsJsonAsync(ApiResult.Error(ResultCode.DENY, msg));
                    return;
                }
            }

            var ts = loginUser.ExpireTime - now;
            if (ts.TotalMinutes <= TOKEN_REFRESH_THRESHOLD_MINUTES)
            {
                var cacheKey = $"token_{loginUser.UserId}";

                // 使用缓存防止并发刷新
                if (!CacheHelper.Exists(cacheKey))
                {
                    try
                    {
                        // 设置缓存锁，防止并发刷新（锁定时间略长于阈值）
                        CacheHelper.SetCache(cacheKey, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), TOKEN_REFRESH_THRESHOLD_MINUTES + 1);

                        var newToken = JwtUtil.GenerateJwtToken(JwtUtil.AddClaims(loginUser));

                        if (!string.IsNullOrEmpty(osType))
                        {
                            context.Response.Headers.Append("Access-Control-Expose-Headers", "X-Refresh-Token");
                        }

                        context.Response.Headers.Append("X-Refresh-Token", newToken);
                        _logger.LogInformation($"刷新Token: {loginUser.UserName}");

                        // 刷新 token 时同步续期会话缓存，避免会话先于 token 失效导致误踢
                        if (_options.SingleLogin)
                        {
                            CacheService.SetUserSession(loginUser.UserId, loginUser.SessionId);
                        }

                        // 同步续期"本会话已通过双因子"标记。
                        // 它与单设备登录无关（扫码登录的 2FA 闸门用它判定移动端会话），但 TTL 与 token 同长：
                        // 不在这里续期，就会出现"token 已续期仍有效、扫码却被告知请重新登录"的错判。
                        CacheService.SetTwoFactorSessionOk(loginUser.UserId, loginUser.SessionId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "刷新Token失败: {UserName}", loginUser.UserName);
                    }
                }
            }

            // 存入 HttpContext.Items，避免后续 DataPermi/DataScopeExtensions/GetCurrentUser 重复解析 JWT
            context.Items[HttpContextExtension.CurrentUserCacheKey] = loginUser;

            // 挂载 context.User，确保后续业务层可读取 claims。
            var identity = new ClaimsIdentity(JwtUtil.AddClaims(loginUser), "jwt");
            context.User = new ClaimsPrincipal(identity);

            await _next(context);
        }
    }
}
