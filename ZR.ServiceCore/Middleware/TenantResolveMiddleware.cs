using Infrastructure;
using Infrastructure.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ZR.ServiceCore.Middleware
{
    /// <summary>
    /// 多租户请求解析与一致性校验中间件。
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

        public async Task InvokeAsync(HttpContext context)
        {
            if (!App.IsTenantEnabled())
            {
                await _next(context);
                return;
            }

            var endpoint = context.GetEndpoint();
            var allowAnonymous = endpoint?.Metadata?.GetMetadata<AllowAnonymousAttribute>() != null;

            // 匿名接口跳过租户一致性校验（无认证用户，tokenTenantId 必然为空，无需校验）
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
    }
}
