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
    }
}
