namespace ZR.Model.System
{
    /// <summary>
    /// Sys_menu表
    /// </summary>
    [SugarTable("sys_menu", "系统菜单表")]
    [Tenant("0")]
    public class SysMenu : SysBase
    {
        /// <summary>
        /// 菜单ID
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long MenuId { get; set; }
        /// <summary>
        /// 菜单名称
        /// </summary>
        [SugarColumn(Length = 50, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string MenuName { get; set; }

        /// <summary>
        /// 父菜单ID
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public long ParentId { get; set; }

        /// <summary>
        /// 显示顺序
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int OrderNum { get; set; }

        /// <summary>
        /// 路由地址
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        /// 组件路径
        /// </summary>
        public string Component { get; set; }

        /// <summary>
        /// 是否缓存（1不缓存 0缓存）
        /// </summary>
        [SugarColumn(DefaultValue = "0", ColumnDataType = "int")]
        public string IsCache { get; set; } = "0";
        /// <summary>
        /// 是否外链 1、是 0、否
        /// </summary>
        [SugarColumn(DefaultValue = "0", ColumnDataType = "int")]
        public string IsFrame { get; set; } = "0";

        /// <summary>
        /// 类型（M目录 C菜单 F按钮 L链接）
        /// </summary>
        [SugarColumn(Length = 1)]
        public string MenuType { get; set; } = string.Empty;

        /// <summary>
        /// 显示状态（0显示 1隐藏）
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public string Visible { get; set; }

        /// <summary>
        /// 菜单状态（0正常 1停用）
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public string Status { get; set; }

        /// <summary>
        /// 权限字符串
        /// </summary>
        [SugarColumn(Length = 100)]
        public string Perms { get; set; }

        /// <summary>
        /// 菜单图标
        /// </summary>
        [SugarColumn(DefaultValue = "#")]
        public string Icon { get; set; } = string.Empty;
        /// <summary>
        /// 菜单名key
        /// </summary>
        [SugarColumn(ColumnName = "menuName_key")]
        public string MenuNameKey { get; set; }
        /// <summary>
        /// 子菜单
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public List<SysMenu> Children { get; set; } = new List<SysMenu>();
        /// <summary>
        /// 子菜单个数
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public int SubNum { get; set; }
        /// <summary>
        /// 是否包含子节点，前端用
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public bool HasChildren
        {
            get
            {
                return SubNum > 0 || Children.Count > 0;
            }
        }
    }
}
