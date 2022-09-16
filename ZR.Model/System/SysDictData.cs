using SqlSugar;

namespace ZR.Model.System
{
    /// <summary>
    /// 字典数据表
    /// </summary>
    [Tenant("0")]
    [SugarTable("sys_dict_data")]
    public class SysDictData: SysBase
    {
        /// <summary>
        /// 字典主键
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long DictCode{ get; set; }
        public int DictSort { get; set; }
        public string DictLabel { get; set; }
        public string DictValue { get; set; }
        public string DictType { get; set; }
        public string CssClass { get; set; } = string.Empty;
        public string ListClass { get; set; } = string.Empty;
        public string IsDefault { get; set; }
        public string Status { get; set; }
    }
}
