using Infrastructure;
using Infrastructure.Model;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using ZR.Common;
using ZR.Model.Models;
using ZR.Model.System;

namespace ZR.Repository
{
    /// <summary>
    /// 数据权限扩展方法（从 DataPermi 迁移至此，供 BaseRepository 和 Service 层统一使用）
    /// </summary>
    public static class DataScopeExtensions
    {
        #region 类型注册表（启动时注册，运行时 BaseRepository 自动查表附加过滤）

        private static readonly Dictionary<Type, Delegate> _scopeFilters = new();

        /// <summary>
        /// 注册实体类型的数据权限过滤器（在 SqlsugarSetup 启动时调用，各业务模块也可调用）
        /// </summary>
        public static void RegisterScopeFilter<T>(Func<ISugarQueryable<T>, ISugarQueryable<T>> filter)
        {
            _scopeFilters[typeof(T)] = filter;
        }

        internal static bool TryApplyScope<T>(ISugarQueryable<T> query, out ISugarQueryable<T> result)
        {
            if (_scopeFilters.TryGetValue(typeof(T), out var filter))
            {
                result = ((Func<ISugarQueryable<T>, ISugarQueryable<T>>)filter)(query);
                return true;
            }
            result = query;
            return false;
        }

        #endregion
        #region 当前登录用户（HttpContext.Items 缓存，同请求内避免重复 JWT 解析）

        private const string LoginUserCacheKey = "__DataPermi_LoginUser";
        private static readonly string DataScopeDeptIdsPrefix = "CACHE-DATASCOPE-DEPTIDS_";

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

        #region 登录时预计算的权限缓存

        internal static int GetScopeType() => GetLoginUser()?.ScopeType ?? (int)MergedScopeType.None;
        internal static bool IsAllScope() => GetScopeType() == (int)MergedScopeType.All;

        private static string BuildTenantKey(string key)
        {
            var tenantId = App.GetCurrentTenantId();
            return string.IsNullOrWhiteSpace(tenantId) ? key : $"{tenantId}:{key}";
        }

        internal static IReadOnlyList<long> GetDataScopeDeptIds()
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return [];
            var cacheKey = BuildTenantKey(DataScopeDeptIdsPrefix + userId);
            return CacheHelper.GetCache<List<long>>(cacheKey) ?? [];
        }

        internal static List<long> GetCurrentUserRoleIds() => GetLoginUser()?.Roles?.Select(r => r.RoleId).ToList() ?? [];

        #endregion

        #region ApplyScope 扩展方法

        public static ISugarQueryable<SysUser> ApplyScope(this ISugarQueryable<SysUser> query)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0 || IsAllScope()) return query;

            var deptIds = GetDataScopeDeptIds();
            // 过滤掉 DeptId=0（未分配部门），避免 DeptId IN (0) 泄露所有未分配部门的用户
            var validDeptIds = deptIds.Where(d => d != 0).ToList();
            if (validDeptIds.Count > 0)
                return query.Where(it => it.UserId == userId || validDeptIds.Contains(it.DeptId));
            else
                return query.Where(it => it.UserId == userId);
        }

        public static ISugarQueryable<SysDept> ApplyScope(this ISugarQueryable<SysDept> query)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0 || IsAllScope()) return query;

            var deptIds = GetDataScopeDeptIds();
            // 过滤掉 DeptId=0（未分配部门）
            var validDeptIds = deptIds.Where(d => d != 0).ToList();
            if (validDeptIds.Count > 0)
                return query.Where(it => validDeptIds.Contains(it.DeptId));
            else
                return query.Where(_ => false);
        }

        public static ISugarQueryable<SysRole> ApplyScope(this ISugarQueryable<SysRole> query)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0 || IsAllScope()) return query;

            var roleIds = GetCurrentUserRoleIds();
            if (roleIds.Count > 0)
                return query.Where(it => roleIds.Contains(it.RoleId));
            else
                return query.Where(_ => false);
        }

        public static ISugarQueryable<SysLogininfor> ApplyScope(this ISugarQueryable<SysLogininfor> query)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0 || IsAllScope()) return query;

            var userName = GetCurrentUserName();
            return query.Where(it => it.UserName == userName);
        }

        public static ISugarQueryable<UserOnlineLog> ApplyScope(this ISugarQueryable<UserOnlineLog> query)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0 || IsAllScope()) return query;

            return query.Where(it => it.UserId == userId);
        }

        #endregion
    }
}
