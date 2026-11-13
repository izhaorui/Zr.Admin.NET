using Infrastructure;
using System.Linq.Expressions;
using ZR.Model;
using ZR.Model.AI;
using ZR.Model.System;
using ZR.Model.System.Model;

namespace ZR.ServiceCore.SqlSugar
{
    /// <summary>
    /// 租户数据隔离 —— 主库共享实体按 TenantId 过滤。
    /// 表达式在 SqlsugarSetup 启动时经 <see cref="Apply"/> 注册，每次查询动态读取 App.GetCurrentTenantId()。
    /// 主租户兼容 TenantId==null 或 TenantId==MainDbConfigId 的历史数据。
    /// 新增过滤：补方法 + 写入 FilteredEntityTypes + 在 Apply 里 AddTableFilter，三处保持一致。
    /// </summary>
    public static class TenantFilter
    {
        /// <summary>
        /// 实际会经 <see cref="Apply"/> 挂到主库 QueryFilter 的实体。隔离自检以这份清单为准。
        /// </summary>
        public static readonly Type[] FilteredEntityTypes =
        {
            typeof(SysUserMsg),
            typeof(SysFile),
            typeof(SysFileGroup),
            typeof(SysTasks),
            typeof(SysTasksLog),
            typeof(AiCallLog),
            typeof(AiAccessPolicy),
        };

        /// <summary>
        /// 把全部租户查询过滤注册到指定连接（仅主库、SaaS 模式由调用方判断）。
        /// </summary>
        public static void Apply(ISqlSugarClient conn)
        {
            conn.QueryFilter.AddTableFilter(SysUserMsgTenantFilter());
            conn.QueryFilter.AddTableFilter(SysFileTenantFilter());
            conn.QueryFilter.AddTableFilter(SysFileGroupTenantFilter());
            conn.QueryFilter.AddTableFilter(SysTasksTenantFilter());
            conn.QueryFilter.AddTableFilter(SysTasksLogTenantFilter());
            conn.QueryFilter.AddTableFilter(AiCallLogTenantFilter());
            conn.QueryFilter.AddTableFilter(AiAccessPolicyTenantFilter());
        }
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

        /// <summary>
        /// AI 调用流水 — 主库共享。主租户看全部（含历史空 TenantId），普通租户只看本租户。
        /// 平台级额度汇总须 ClearFilter，见 AiCallGovernanceService.GetUsagePairAsync。
        /// </summary>
        public static Expression<Func<AiCallLog, bool>> AiCallLogTenantFilter() => it =>
            it.TenantId == App.GetCurrentTenantId()
            || (App.GetCurrentTenantId() == App.MainDbConfigId
                && (it.TenantId == null || it.TenantId == App.MainDbConfigId));

        /// <summary>
        /// AI 访问策略 — 主库共享。主租户看全部；普通租户看全局策略 + 本租户策略（含 role/user）。
        /// 不可写成 TenantId 精确匹配，否则租户上下文读不到 ScopeType=global 的平台规则。
        /// </summary>
        public static Expression<Func<AiAccessPolicy, bool>> AiAccessPolicyTenantFilter() => it =>
            App.GetCurrentTenantId() == App.MainDbConfigId
            || it.ScopeType == "global"
            || it.TenantId == App.GetCurrentTenantId();
    }
}
