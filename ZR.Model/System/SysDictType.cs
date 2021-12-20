using OfficeOpenXml.Attributes;
using SqlSugar;

namespace ZR.Model.System
{
    /// <summary>
    /// 字典类型表
    /// </summary>
    [SugarTable("sys_dict_type")]
    [Tenant("0")]
    public class SysDictType : SysBase
    {
        /// <summary>
        /// 字典主键
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]//主键并且自增 （string不能设置自增）
        public long DictId { get; set; }
        /// <summary>
        /// 字典名称
        /// </summary>
        public string DictName { get; set; }
        /// <summary>
        /// 字典类型
        /// </summary>
        public string DictType { get; set; }
        /// <summary>
        /// 状态 0、正常 1、停用
        /// </summary>
        [EpplusIgnore]
        public string Status { get; set; }
        /// <summary>
        /// 系统内置 Y是 N否
        /// </summary>
        public string Type { get; set; }
    }
}
