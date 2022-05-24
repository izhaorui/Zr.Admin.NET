using SqlSugar;
using System.Collections.Generic;

namespace ZR.Model.System
{
    /// <summary>
    /// Sys_menu表
    /// </summary>
    [SugarTable("sys_menu")]
    [Tenant("0")]
    public class SysMenu : SysBase
    {
        /// <summary>
        /// 菜单ID
        /// </summary>
        //[Key]//非自动增长主键时使用ExplicitKey
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long MenuId { get; set; }
        /// <summary>
        /// 菜单名称
        /// </summary>
        public string MenuName { get; set; }

        /// <summary>
        /// 父菜单ID
        /// </summary>
        public long parentId { get; set; }

        /// <summary>
        /// 显示顺序
        /// </summary>
        public int orderNum { get; set; }

        /// <summary>
        /// 路由地址
        /// </summary>
        public string path { get; set; } = "#";

        /// <summary>
        /// 组件路径
        /// </summary>
        public string component { get; set; }

        /// <summary>
        /// 是否缓存（1缓存 0不缓存）
        /// </summary>
        public string isCache { get; set; }
        /// <summary>
        /// 是否外链 1、是 0、否
        /// </summary>
        public string isFrame { get; set; }

        /// <summary>
        /// 类型（M目录 C菜单 F按钮 L链接）
        /// </summary>
        public string menuType { get; set; }

        /// <summary>
        /// 显示状态（0显示 1隐藏）
        /// </summary>
        public string visible { get; set; }

        /// <summary>
        /// 菜单状态（0正常 1停用）
        /// </summary>
        public string status { get; set; }

        /// <summary>
        /// 权限字符串
        /// </summary>
        public string perms { get; set; }

        /// <summary>
        /// 菜单图标
        /// </summary>
        public string icon { get; set; } = string.Empty;
        /// <summary>
        /// 菜单名key
        /// </summary>
        [SugarColumn(ColumnName = "menuName_key")]
        public string MenuNameKey { get; set; }
        /// <summary>
        /// 子菜单
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public List<SysMenu> children { get; set; } = new List<SysMenu>();
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
                return SubNum > 0 || children.Count > 0;
            }
        }
    }
}
