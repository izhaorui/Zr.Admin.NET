namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 菜单种子定义用的轻量记录类型，替代原先嵌套 ValueTuple 的写法。
    /// 用命名属性 + 可选默认值，新增字段无需改动已有调用点。
    /// </summary>

    /// <summary>按钮/权限（F 类型菜单）。</summary>
    public sealed record SeedButton(
        string Name,
        string Perms,
        int OrderNum = 1);

    /// <summary>子页面（C 类型菜单）+ 其下按钮权限。</summary>
    public sealed record SeedPage(
        string Name,
        string Path,
        string Component,
        string Perms,
        int OrderNum,
        IReadOnlyList<SeedButton> Buttons,
        string Icon = "",
        string RouteName = "",
        //1隐藏，0显示
        string Visible = "0",
        int Category = 0);
}
