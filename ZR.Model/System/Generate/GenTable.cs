using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System.Generate
{
    /// <summary>
    /// 代码生成表
    /// </summary>
    [SqlSugar.SugarTable("gen_table")]
    public class GenTable: SysBase
    {
        [SqlSugar.SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int TableId { get; set; }
        public string TableName { get; set; }
        public string TableComment { get; set; }
        public string SubTableName { get; set; }
        public string SubTableFkName { get; set; }
        public string ClassName { get; set; }
        public string TplCategory { get; set; }
        public string BaseNameSpace { get; set; }
        public string ModuleName { get; set; }
        public string BusinessName { get; set; }
        public string FunctionName { get; set; }
        public string FunctionAuthor { get; set; }
        public string GenType { get; set; }
        public string Options { get; set; }


        /** 表列信息 */
        [SqlSugar.SugarColumn(IsIgnore = true)]
        public List<GenTableColumn> Columns { get; set; }
    }
}
