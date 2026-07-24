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

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            // 如果请求是带扩展名的（即静态资源）
            if (path.Contains('.'))
            {
                await _next(context);
                return;
            }
            //_logger.LogInformation($"处理请求: {path}");
            // 白名单路径检查
            if (_whitelistPaths.Any(p => !string.IsNullOrEmpty(p) && path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            // 允许匿名访问的端点
            var endpoint = context.GetEndpoint();
            var allowAnonymous = endpoint?.Metadata?.GetMetadata<AllowAnonymousAttribute>() != null;

            if (allowAnonymous || endpoint == null)
            {
                Console.WriteLine($"断点访问被过滤path={path}");
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

            var now = DateTime.UtcNow;
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
