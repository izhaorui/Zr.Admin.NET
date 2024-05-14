using System.ComponentModel;

namespace ZR.Model.System.Enums
{
    /// <summary>
    /// M目录 C菜单 F按钮 L链接
    /// </summary>
    public enum MenuType
    {
        [Description("目录")]
        M,
        [Description("菜单")]
        C,
        [Description("按钮")]
        F,
        [Description("链接")]
        L
    }
}
