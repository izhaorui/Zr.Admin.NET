using Infrastructure;
using Infrastructure.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.Middleware
{
    /// <summary>
    /// 按租户套餐菜单权限拦截接口访问。
    /// 说明：权限核心过滤由 ActionPermissionFilter 完成；本中间件作为兜底，
    /// 防止未经过滤器的新增接口直接暴露套餐外能力。
    /// </summary>
    public class TenantFeatureMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantFeatureMiddleware> _logger;

        public TenantFeatureMiddleware(RequestDelegate next, ILogger<TenantFeatureMiddleware> logger)
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
            if (endpoint == null)
            {
                await _next(context);
                return;
            }

            var allowAnonymous = endpoint.Metadata.GetMetadata<AllowAnonymousAttribute>() != null;
            if (allowAnonymous)
            {
                await _next(context);
                return;
            }

            if (context.IsAdmin())
            {
                await _next(context);
                return;
            }

            var permissionFilter = endpoint.Metadata.GetMetadata<ActionPermissionFilter>();
            var permission = permissionFilter?.Permission;
            if (string.IsNullOrWhiteSpace(permission))
            {
                await _next(context);
                return;
            }

            // “common” 是“登录用户即可访问”的语义标记（见 ActionPermissionFilter），不参与租户套餐权限校验。
            // 否则会被当成真实权限去套餐列表里匹配，而套餐权限中只有具体功能权限、不含 “common”，导致所有 common 接口被误拒。
            if (string.Equals(permission, "common", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var tenantId = App.GetCurrentTenantId();
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                await _next(context);
                return;
            }

            var planMenuService = App.GetService<ISysTenantPlanMenuService>();
            var planPerms = planMenuService.GetPermsByTenantId(tenantId);

            // 权限不在当前套餐菜单权限中，拒绝访问
            if (!planPerms.Contains(permission, StringComparer.OrdinalIgnoreCase))
            {
                var path = context.Request.Path.Value ?? string.Empty;
                _logger.LogWarning("租户套餐未包含该权限，请求被拒绝: tenant={TenantId}, path={Path}, permission={Permission}", tenantId, path, permission);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(ApiResult.Error(ResultCode.FORBIDDEN, "当前租户套餐未包含该权限"));
                return;
            }

            await _next(context);
        }
    }
}
