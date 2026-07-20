using Infrastructure;
using Infrastructure.Model;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.SqlSugar
{
    /// <summary>
    /// 角色数据权限 —— 辅助方法集。
    /// 全局 QueryFilter 逻辑已迁移至 <see cref="ZR.Repository.DataScopeExtensions"/>。
    /// 此文件保留基础方法供自定义扩展使用（如其他开发者在此添加新的 Expression 过滤器）。
    ///
    /// <para>租户隔离过滤器已拆分到 <see cref="TenantFilter"/>。</para>
    /// </summary>
    public static class DataPermi
    {
        #region 当前登录用户（HttpContext.Items 缓存，同请求内避免重复 JWT 解析）

        private const string LoginUserCacheKey = "__DataPermi_LoginUser";

        private static TokenModel GetLoginUser()
        {
            var ctx = App.HttpContext;
            if (ctx == null) return null;

            if (ctx.Items.TryGetValue(LoginUserCacheKey, out var cached) && cached is TokenModel user)
                return user;

            user = JwtUtil.GetLoginUser(ctx);
            if (user != null) ctx.Items[LoginUserCacheKey] = user;
            return user;
        }

        public static long GetCurrentUserId() => GetLoginUser()?.UserId ?? 0;
        public static long GetCurrentUserDeptId() => GetLoginUser()?.DeptId ?? 0;
        public static string GetCurrentUserName() => GetLoginUser()?.UserName ?? string.Empty;

        #endregion

        #region 登录时预计算的权限缓存（ScopeType 存 JWT，DataScopeDeptIds 存服务端）

        /// <summary>合并后的数据权限等级（登录时预计算，取所有角色中最宽松的权限）</summary>
        internal static int GetScopeType() => GetLoginUser()?.ScopeType ?? (int)MergedScopeType.None;

        /// <summary>是否为 All 权限（管理员 或 DataScope=全部）</summary>
        internal static bool IsAllScope() => GetScopeType() == (int)MergedScopeType.All;

        /// <summary>DEPT_CHILD + CUSTOM 部门 ID 并集（CacheService 缓存，按租户+用户，不走 JWT）</summary>
        internal static IReadOnlyList<long> GetDataScopeDeptIds()
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return [];
            return CacheService.GetDataScopeDeptIds(userId);
        }

        /// <summary>用户拥有的角色 ID 列表（从 JWT Token 中读取，通常 1-3 个）</summary>
        internal static List<long> GetCurrentUserRoleIds() => GetLoginUser()?.Roles?.Select(r => r.RoleId).ToList() ?? [];

        #endregion
    }
}
