using Infrastructure;
using System.Linq.Expressions;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Model;

namespace ZR.ServiceCore.SqlSugar
{
    /// <summary>
    /// 租户数据隔离 —— 主库共享实体按 TenantId 过滤。
    /// 表达式在 SqlsugarSetup 启动时注册，每次查询动态读取 App.GetCurrentTenantId()。
    /// 主租户兼容 TenantId==null 或 TenantId==MainDbConfigId 的历史数据。
    /// </summary>
    public static class TenantFilter
    {
        public static Expression<Func<SysUserMsg, bool>> SysUserMsgTenantFilter() => it =>
            it.IsDelete == 0
            && (it.TenantId == App.GetCurrentTenantId()
                || (App.GetCurrentTenantId() == App.MainDbConfigId
                    && (it.TenantId == null || it.TenantId == App.MainDbConfigId)));

        public static Expression<Func<SysFile, bool>> SysFileTenantFilter() => it =>
            it.TenantId == App.GetCurrentTenantId()
            || (App.GetCurrentTenantId() == App.MainDbConfigId
                && (it.TenantId == null || it.TenantId == App.MainDbConfigId));

        public static Expression<Func<SysFileGroup, bool>> SysFileGroupTenantFilter() => it =>
            it.TenantId == App.GetCurrentTenantId()
            || (App.GetCurrentTenantId() == App.MainDbConfigId
                && (it.TenantId == null || it.TenantId == App.MainDbConfigId));

        /// <summary>
        /// 计划任务 — 主库共享，主租户看全部，普通租户看自己的 + 通配 * 任务。
        /// 逗号列表（t1,t2）的精确成员判定因 SqlSugar 表达式翻译限制，留在 EnsureTaskAccess 中处理。
        /// </summary>
        public static Expression<Func<SysTasks, bool>> SysTasksTenantFilter() => it =>
            App.GetCurrentTenantId() == App.MainDbConfigId
            || it.TenantId == App.GetCurrentTenantId()
            || it.TenantId == "*";

        /// <summary>
        /// 任务日志 — 主库共享，主租户看全部，普通租户只看自己租户的执行记录。
        /// </summary>
        public static Expression<Func<SysTasksLog, bool>> SysTasksLogTenantFilter() => it =>
            App.GetCurrentTenantId() == App.MainDbConfigId
            || it.TenantId == App.GetCurrentTenantId();
    }
}
