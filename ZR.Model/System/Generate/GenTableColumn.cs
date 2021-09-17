using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System.Generate
{
    /// <summary>
    /// 代码生成表字段
    /// </summary>
    [SqlSugar.SugarTable("gen_table_column")]
    public class GenTableColumn
    {
        [SqlSugar.SugarColumn(IsIdentity = true, IsPrimaryKey =  true)]
        public int ColumnId { get; set; }
        public string ColumnName { get; set; }
        public int TableId { get; set; }
        public string TableName { get; set; }
        public string ColumnComment { get; set; }
        public string ColumnType { get; set; }
        public string CsharpType { get; set; }
        public string CsharpField { get; set; }
        public bool IsPk { get; set; }
        public bool IsRequired { get; set; }
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
        public int Sort { get; set; }

        public string CreateBy { get; set; }
        public DateTime CreateTime { get; set; }
    }
}
