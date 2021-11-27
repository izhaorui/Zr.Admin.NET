using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System.Generate
{
    /// <summary>
    /// 代码生成表字段
    /// </summary>
    [SqlSugar.SugarTable("gen_table_column")]
    [SqlSugar.Tenant("0")]
    public class GenTableColumn: SysBase
    {
        [SqlSugar.SugarColumn(IsIdentity = true, IsPrimaryKey =  true)]
        public int ColumnId { get; set; }
        public string ColumnName { get; set; }
        [SqlSugar.SugarColumn(IsOnlyIgnoreUpdate = true)]
        public int TableId { get; set; }

        [SqlSugar.SugarColumn(IsOnlyIgnoreUpdate = true)]
        public string TableName { get; set; }
        public string ColumnComment { get; set; }

        [SqlSugar.SugarColumn(IsOnlyIgnoreUpdate = true)]
        public string ColumnType { get; set; }
        public string CsharpType { get; set; }
        public string CsharpField { get; set; }
        /// <summary>
        /// 是否主键（1是）
        /// </summary>
        [SqlSugar.SugarColumn(IsOnlyIgnoreUpdate = true)]
        public bool IsPk { get; set; }
        /// <summary>
        /// 是否必填（1是）
        /// </summary>
        public bool IsRequired { get; set; }
        [SqlSugar.SugarColumn(IsOnlyIgnoreUpdate = true)]
        public bool IsIncrement { get; set; }
        /// <summary>
        /// 是否插入
        /// </summary>
        public bool IsInsert { get; set; }
        /// <summary>
        /// 是否需要编辑
        /// </summary>
        public bool IsEdit { get; set; }
        /// <summary>
        /// isList
        /// </summary>
        public bool IsList { get; set; }
        public bool IsQuery { get; set; }
        /// <summary>
        /// 显示类型（文本框、文本域、下拉框、复选框、单选框、日期控件）
        /// </summary>
        public string HtmlType { get; set; }
        /// <summary>
        /// 查询类型（等于、不等于、大于、小于、范围）
        /// </summary>
        public string QueryType { get; set; } = "EQ";
        public int Sort { get; set; }
        /// <summary>
        /// 字典类型
        /// </summary>
        public string DictType { get; set; }
        /// <summary>
        /// 字典集合
        /// </summary>
        [SqlSugar.SugarColumn(IsIgnore = true)]
        public List<SysDictData> DictDatas { get; set; }
    }
}
