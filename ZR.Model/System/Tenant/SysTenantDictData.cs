namespace ZR.Model.System.Tenant
{
    /// <summary>
    /// 租户字典数据扩展表（存储在租户库）
    /// 用于租户对主库公共字典的覆盖、禁用和新增。
    /// 以 DictType + DictValue 作为关联主库字典的 Key。
    /// </summary>
    [SugarTable("sys_tenant_dict_data", "租户字典数据扩展表")]
    public class SysTenantDictData : SysBase
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        /// <summary>
        /// 字典类型，关联主库 SysDictType.DictType
        /// </summary>
        [SugarColumn(Length = 100, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string DictType { get; set; }

        /// <summary>
        /// 字典键值，关联主库 SysDictData.DictValue
        /// 新增自定义项时，需确保不与主库已有值冲突。
        /// </summary>
        [SugarColumn(Length = 100, ExtendedAttribute = ProteryConstant.NOTNULL)]
        public string DictValue { get; set; }

        /// <summary>
        /// 字典标签（覆盖/新增的显示文本）
        /// </summary>
        [SugarColumn(Length = 100)]
        public string DictLabel { get; set; }

        /// <summary>
        /// 字典排序
        /// </summary>
        public int DictSort { get; set; }

        /// <summary>
        /// 状态（0正常 1停用）
        /// 停用表示该租户不启用此字典项（覆盖主库项时生效）。
        /// </summary>
        [SugarColumn(Length = 1, DefaultValue = "0")]
        public string Status { get; set; } = "0";

        /// <summary>
        /// 是否默认（Y是 N否）
        /// </summary>
        [SugarColumn(Length = 1, DefaultValue = "N")]
        public string IsDefault { get; set; } = "N";

        /// <summary>
        /// 样式属性（其他样式扩展）
        /// </summary>
        [SugarColumn(Length = 100)]
        public string CssClass { get; set; }

        /// <summary>
        /// 表格回显样式
        /// </summary>
        [SugarColumn(Length = 100)]
        public string ListClass { get; set; }

        /// <summary>
        /// 被覆盖的主库字典数据 DictCode。
        /// 如果是对主库已有项的覆盖，记录其 DictCode；
        /// 如果是租户新增的自定义项，则为 null。
        /// </summary>
        public long? OriginalDictCode { get; set; }
    }
}
