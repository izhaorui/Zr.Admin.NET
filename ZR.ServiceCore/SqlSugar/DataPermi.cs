using Infrastructure;
using Infrastructure.Model;
using System.Linq.Expressions;
using ZR.Model.Models;
using ZR.Model.System;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.SqlSugar
{
    /// <summary>
    /// 角色数据权限 —— 全局动态 QueryFilter 表达式构造。
    /// 表达式在 SqlsugarSetup 启动时注册一次，每次查询动态读取当前登录用户，并发请求天然隔离。
    /// 非 HTTP 场景(种子/后台任务) ScopeType=None，过滤器短路为不过滤。
    /// 
    /// <para>租户隔离过滤器已拆分到 <see cref="TenantFilter"/>。</para>
    /// 
    /// <para><b>优化要点</b>：
    /// ScopeType + DataScopeDeptIds 在登录时预计算并缓存到 JWT TokenModel，
    /// QueryFilter 直接做恒等比较或 Contains，不再走 SQL EXISTS 子查询。</para>
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

        #region 角色数据权限过滤器（租户库表）

        // ===== SysUser ===== SELF | DataScopeDeptIds（含 DEPT + DEPT_CHILD + CUSTOM）
        public static Expression<Func<SysUser, bool>> SysUserFilter() => it =>
            it.DelFlag == 0 && (
                GetCurrentUserId() <= 0
                || IsAllScope()
                || it.UserId == GetCurrentUserId()
                || GetDataScopeDeptIds().Contains(it.DeptId)
            );

        // ===== SysDept ===== DataScopeDeptIds（含 DEPT + DEPT_CHILD + CUSTOM）
        public static Expression<Func<SysDept, bool>> SysDeptFilter() => it =>
            GetCurrentUserId() <= 0
            || IsAllScope()
            || GetDataScopeDeptIds().Contains(it.DeptId);

        // ===== SysRole ===== 用户拥有的角色（从 JWT Token 读取，SQL 翻译为 role_id IN (...)）
        public static Expression<Func<SysRole, bool>> SysRoleFilter() => it =>
            GetCurrentUserId() <= 0
            || IsAllScope()
            || GetCurrentUserRoleIds().Contains(it.RoleId);

        // ===== SysLogininfor ===== 仅看自己的登录日志
        public static Expression<Func<SysLogininfor, bool>> SysLogininforFilter() => it =>
            GetCurrentUserId() <= 0
            || IsAllScope()
            || it.UserName == GetCurrentUserName();

        // ===== UserOnlineLog ===== 仅看自己的在线日志
        public static Expression<Func<UserOnlineLog, bool>> UserOnlineLogFilter() => it =>
            GetCurrentUserId() <= 0
            || IsAllScope()
            || it.UserId == GetCurrentUserId();

        #endregion
    }
}
