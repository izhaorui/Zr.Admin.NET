using Infrastructure;
using Microsoft.Extensions.Configuration;
using ZR.Model.System;
using ZR.Model.System.Vo;

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

        /// <summary>
        /// 过滤平台专属菜单（移动端工作台菜单 RouterVo 版）。
        /// Meta.Permi 命中 PlatformMenuPermPrefixes 的子项会被移除；分组内子项被清空时该分组也不再下发。
        /// </summary>
        public static List<RouterVo> FilterPlatformMenusForNonMainTenant(List<RouterVo> menus, bool isMainTenant)
        {
            if (menus == null || menus.Count == 0)
            {
                return menus ?? new List<RouterVo>();
            }

            if (isMainTenant)
            {
                return menus;
            }

            var result = new List<RouterVo>();
            foreach (var group in menus)
            {
                if (group == null)
                {
                    continue;
                }

                if (group.Children != null && group.Children.Count > 0)
                {
                    group.Children = group.Children
                        .Where(c => c != null && !IsPlatformMenuPermission(c.Meta?.Permi))
                        .ToList();
                }

                // 分组下已无可见菜单则整个分组不下发
                if (group.Children == null || group.Children.Count == 0)
                {
                    continue;
                }

                result.Add(group);
            }

            return result;
        }
    }
}
