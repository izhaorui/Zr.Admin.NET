namespace ZR.Model.System
{
    /// <summary>
    /// 部门表
    /// </summary>
    [SugarTable("sys_dept", "部门配置表")]
    [Tenant("0")]
    public class SysDept : SysBase
    {
        /// <summary>
        /// 部门ID
        /// </summary>
        [SugarColumn(IsIdentity = true, IsPrimaryKey = true)]
        [JsonConverter(typeof(ValueToStringConverter))]
        public long DeptId { get; set; }

        /// <summary>
        /// 父部门ID
        /// </summary>
        public long ParentId { get; set; }

        /// <summary>
        /// 祖级列表
        /// </summary>
        public string Ancestors { get; set; }

        /// <summary>
        /// 部门名称
        /// </summary>
        [SugarColumn(Length = 30, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string DeptName { get; set; }

        /// <summary>
        /// 显示顺序
        /// </summary>
        public int OrderNum { get; set; }

        /// <summary>
        /// 负责人
        /// </summary>
        [SugarColumn(Length = 30)]
        public string Leader { get; set; }

        /// <summary>
        /// 联系电话
        /// </summary>
        [SugarColumn(Length = 11)]
        public string Phone { get; set; }

        /// <summary>
        /// 邮箱
        /// </summary>
        [SugarColumn(Length = 50)]
        public string Email { get; set; }

        /// <summary>
        /// 部门状态:0正常,1停用
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int Status { get; set; }

        /// <summary>
        /// 删除标志（0代表存在 2代表删除）
        /// </summary>
        [SugarColumn(DefaultValue = "0")]
        public int DelFlag { get; set; }

        /// <summary>
        /// 子菜单
        /// </summary>
        public List<SysDept> children = new();
    }
}
