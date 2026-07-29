using Infrastructure;
using Infrastructure.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Security.Claims;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.Middleware
{
    /// <summary>
    /// 多租户请求解析与一致性校验中间件。
    /// 在请求早期按访问域名（子域名/自定义域名）解析租户，使匿名（商城游客）与已登录请求
    /// 都能定位到对应租户库。已登录请求按域名识别，但与 token 租户不一致时不拦截（仅识别）。
    /// </summary>
    public class TenantResolveMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantResolveMiddleware> _logger;

        public TenantResolveMiddleware(RequestDelegate next, ILogger<TenantResolveMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ISysTenantService tenantService)
        {
            if (!App.IsTenantEnabled())
            {
                await _next(context);
                return;
            }

            // 早期域名解析：在匿名跳过判定之前执行，使匿名（商城游客）也能识别租户。
            var host = context.Request.Host.Host; // 已去除端口
            var map = tenantService.GetDomainTenantMap();
            var resolvedTenant = ResolveTenantByHost(host, map, App.GetTenantRootDomains(), App.GetTenantReservedSubDomains());

            var resolvedFromDomain = !string.IsNullOrWhiteSpace(resolvedTenant);
            if (resolvedFromDomain)
            {
                // 写入请求级租户上下文：TenantContext 优先级最高，下游自动按该租户生效。
                TenantContext.CurrentTenantId = resolvedTenant;
                context.Items["TenantId"] = resolvedTenant;
                context.Request.Headers["tenantId"] = resolvedTenant;
            }

            var endpoint = context.GetEndpoint();
            var allowAnonymous = endpoint?.Metadata?.GetMetadata<AllowAnonymousAttribute>() != null;

            // 匿名接口：域名解析已完成（若有），无需 token 一致性校验。
            // 多租户定位依赖前端在请求头统一携带 tenantId（见 src/utils/request.js），
            // BaseRepository 在控制器/Service 构造时通过 GetCurrentTenantId() 读取该 header
            // 即可正确绑定对应租户库；无需后端解析 body。
            if (allowAnonymous)
            {
                await _next(context);
                return;
            }

            var headerTenantId = context.Request.Headers["tenantId"].ToString();
            var tokenTenantId = context.User?.FindFirstValue(ClaimTypes.PrimaryGroupSid);

            if (string.IsNullOrWhiteSpace(tokenTenantId))
            {
                var loginUser = JwtUtil.GetLoginUser(context);
                tokenTenantId = loginUser?.TenantId;
            }

            // 域名已解析出租户：以服务端派生的域名为准，不拦截 token 与域名不一致的情况。
            if (resolvedFromDomain)
            {
                await _next(context);
                return;
            }

            if (!string.IsNullOrWhiteSpace(headerTenantId) && !string.IsNullOrWhiteSpace(tokenTenantId)
                && !string.Equals(headerTenantId, tokenTenantId, StringComparison.OrdinalIgnoreCase))
            {
                var path = context.Request.Path.Value ?? string.Empty;
                _logger.LogWarning("租户不一致，请求被拒绝: path={Path}, headerTenant={HeaderTenant}, tokenTenant={TokenTenant}", path, headerTenantId, tokenTenantId);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(ApiResult.Error(ResultCode.FORBIDDEN, "租户信息不匹配"));
                return;
            }

            if (string.IsNullOrWhiteSpace(headerTenantId))
            {
                var resolvedTenantId = !string.IsNullOrWhiteSpace(tokenTenantId)
                    ? tokenTenantId
                    : App.GetCurrentTenantId();

                if (!string.IsNullOrWhiteSpace(resolvedTenantId))
                {
                    context.Request.Headers["tenantId"] = resolvedTenantId;
                }
            }

            await _next(context);
        }

        /// <summary>
        /// 按访问域名解析租户。
        /// 匹配顺序：保留子域名（不解析）→ 精确完整域名 → 子域名标签（host 以 .根域名 结尾取首段）。
        /// 均未命中返回 null（回退 token/主库）。
        /// </summary>
        private static string ResolveTenantByHost(string host, Dictionary<string, string> map, string[] rootDomains, string[] reservedSubDomains)
        {
            if (string.IsNullOrWhiteSpace(host) || map == null || map.Count == 0)
            {
                return null;
            }

            var lower = host.Trim().ToLowerInvariant();

            // 保留子域名（www/admin/api… 或 www.shop.com）：不解析为租户
            foreach (var r in reservedSubDomains ?? Enumerable.Empty<string>())
            {
                var rv = r.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(rv)) continue;
                if (string.Equals(lower, rv, StringComparison.OrdinalIgnoreCase)) return null;
                if (lower.StartsWith(rv + ".", StringComparison.OrdinalIgnoreCase)) return null;
            }

            // 精确完整域名匹配（自定义独立域名，如 acme.com）
            if (map.TryGetValue(lower, out var exactTenant))
            {
                return exactTenant;
            }

            // 子域名标签匹配（a.shop.com → a）
            foreach (var root in rootDomains ?? Enumerable.Empty<string>())
            {
                var rv = root.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(rv)) continue;
                var suffix = "." + rv;
                if (lower.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    var sub = lower.Substring(0, lower.Length - suffix.Length);
                    if (!string.IsNullOrWhiteSpace(sub) && map.TryGetValue(sub, out var subTenant))
                    {
                        return subTenant;
                    }
                }
            }

            return null;
        }
    }
}
