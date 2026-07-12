using Infrastructure;
using Microsoft.Extensions.Configuration;
using ZR.Model.System;

namespace ZR.ServiceCore.Middleware
{
    /// <summary>
    /// saas 多租户功能开关策略工具类。
    /// 当前仅保留平台专属菜单过滤，套餐功能改为通过套餐菜单配置控制。
    /// </summary>
    public static class TenantFeaturePolicy
    {
        private static readonly Lazy<List<string>> PlatformMenuPermPrefixes = new(() =>
        {
            var prefixes = App.Configuration.GetSection("TenantSettings:PlatformMenuPermPrefixes").Get<List<string>>();
            return prefixes ?? new List<string>();
        });

        public static bool IsPlatformMenuPermission(string permission)
        {
            if (string.IsNullOrWhiteSpace(permission))
            {
                return false;
            }

            return PlatformMenuPermPrefixes.Value.Any(p =>
                permission.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsPlatformMenu(SysMenu menu)
        {
            return menu != null && IsPlatformMenuPermission(menu.Perms);
        }

        public static List<SysMenu> FilterPlatformMenusForNonMainTenant(List<SysMenu> menus, bool isMainTenant)
        {
            if (menus == null || menus.Count == 0)
            {
                return menus ?? new List<SysMenu>();
            }

            if (isMainTenant)
            {
                return menus;
            }

            List<SysMenu> Filter(List<SysMenu> source)
            {
                var result = new List<SysMenu>();
                foreach (var menu in source)
                {
                    if (menu == null || IsPlatformMenu(menu))
                    {
                        continue;
                    }

                    if (menu.Children != null && menu.Children.Count > 0)
                    {
                        menu.Children = Filter(menu.Children);
                    }

                    var hasVisibleChildren = menu.Children != null && menu.Children.Count > 0;
                    if (menu.MenuType == "M" && !hasVisibleChildren)
                    {
                        continue;
                    }

                    result.Add(menu);
                }

                return result;
            }

            return Filter(menus);
        }
    }
}
