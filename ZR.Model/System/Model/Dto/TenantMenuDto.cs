namespace ZR.Model.System.Dto
{
    /// <summary>
    /// 租户菜单配置视图（合并主库 SysMenu 信息）
    /// </summary>
    public class TenantMenuDto
    {
        public long Id { get; set; }
        public long MenuId { get; set; }
        public long ParentId { get; set; }

        /// <summary>
        /// 菜单名称（主库原名）
        /// </summary>
        public string MenuName { get; set; }

        /// <summary>
        /// 租户自定义名称（为空则用原名）
        /// </summary>
        public string CustomName { get; set; }

        /// <summary>
        /// 菜单类型 M/C/F/L
        /// </summary>
        public string MenuType { get; set; }

        /// <summary>
        /// 路由路径
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 权限标识
        /// </summary>
        public string Perms { get; set; }

        /// <summary>
        /// 图标
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// 组件路径
        /// </summary>
        public string Component { get; set; }

        /// <summary>
        /// 是否可见（0=可见 1=隐藏）
        /// </summary>
        public int IsVisible { get; set; }

        /// <summary>
        /// 是否启用（1=启用 0=停用）
        /// </summary>
        public int IsEnable { get; set; }

        /// <summary>
        /// 租户自定义排序
        /// </summary>
        public int Sort { get; set; }

        /// <summary>
        /// 主库原始排序
        /// </summary>
        public int OrderNum { get; set; }

        /// <summary>
        /// 菜单状态（0=正常 1=停用）
        /// </summary>
        public string Status { get; set; }

        public List<TenantMenuDto> Children { get; set; } = new List<TenantMenuDto>();
    }

    /// <summary>
    /// 全量菜单同步 - 单租户结果
    /// </summary>
    public class TenantMenuSyncItemDto
    {
        public string TenantId { get; set; }
        public string TenantName { get; set; }
        public int AddedCount { get; set; }
        public bool Success { get; set; } = true;
        public string Error { get; set; }
    }

    /// <summary>
    /// 全量菜单同步结果
    /// </summary>
    public class SyncAllTenantMenusResultDto
    {
        public int TotalTenants { get; set; }
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public int TotalAdded { get; set; }
        public List<TenantMenuSyncItemDto> Items { get; set; } = new List<TenantMenuSyncItemDto>();
    }

    /// <summary>
    /// 租户菜单配置更新请求
    /// </summary>
    public class TenantMenuUpdateDto
    {
        [Required(ErrorMessage = "ID不能为空")]
        public long Id { get; set; }

        /// <summary>
        /// 租户标识（后端从当前上下文填充）
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// 是否可见（0=可见 1=隐藏），不传则不更新
        /// </summary>
        public int? IsVisible { get; set; }

        /// <summary>
        /// 是否启用（1=启用 0=停用），不传则不更新
        /// </summary>
        public int? IsEnable { get; set; }

        /// <summary>
        /// 自定义排序，不传则不更新
        /// </summary>
        public int? Sort { get; set; }

        /// <summary>
        /// 自定义菜单名称，null 则不更新
        /// </summary>
        public string CustomName { get; set; }
    }
}
