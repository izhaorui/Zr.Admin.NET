namespace ZR.Model.System.Generate
{
    /// <summary>
    /// 代码生成表
    /// </summary>
    [SugarTable("gen_table", "代码生成表")]
    [Tenant("0")]
    public class GenTable : SysBase
    {
        /// <summary>
        /// 表id
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long TableId { get; set; }
        /// <summary>
        /// 数据库名
        /// </summary>
        [SugarColumn(Length = 50)]
        public string DbName { get; set; }
        /// <summary>
        /// 表名
        /// </summary>
        [SugarColumn(Length = 150)]
        public string TableName { get; set; }
        /// <summary>
        /// 表描述
        /// </summary>
        [SugarColumn(Length = 150)]
        public string TableComment { get; set; }
        /// <summary>
        /// 关联父表的表名
        /// </summary>
        [SugarColumn(Length = 150)]
        public string SubTableName { get; set; }
        /// <summary>
        /// 本表关联父表的外键名
        /// </summary>
        [SugarColumn(Length = 150)]
        public string SubTableFkName { get; set; }
        /// <summary>
        /// csharp类名
        /// </summary>
        public string ClassName { get; set; }
        /// <summary>
        /// 使用的模板（crud单表操作 tree树表操作 sub主子表操作）
        /// </summary>
        [SugarColumn(Length = 50, DefaultValue = "crud")]
        public string TplCategory { get; set; }
        /// <summary>
        /// 基本命名空间前缀
        /// </summary>
        [SugarColumn(Length = 100)]
        public string BaseNameSpace { get; set; }
        /// <summary>
        /// 生成模块名
        /// </summary>
        [SugarColumn(Length = 50)]
        public string ModuleName { get; set; }
        /// <summary>
        /// 生成业务名
        /// </summary>
        [SugarColumn(Length = 50)]
        public string BusinessName { get; set; }
        /// <summary>
        /// 生成功能名
        /// </summary>
        public string FunctionName { get; set; }
        /// <summary>
        /// 生成作者名
        /// </summary>
        public string FunctionAuthor { get; set; }
        /// <summary>
        /// 生成代码方式（0zip压缩包 1自定义路径）
        /// </summary>
        [SugarColumn(Length = 1, DefaultValue = "0")]
        public string GenType { get; set; }
        /// <summary>
        /// 代码生成保存路径
        /// </summary>
        [SugarColumn(Length = 200, DefaultValue = "/")]
        public string GenPath { get; set; }
        /// <summary>
        /// 其他生成选项
        /// </summary>
        [SugarColumn(IsJson = true)]
        public CodeOptions Options { get; set; }

        #region 表额外字段
        /// <summary>
        /// 表列信息
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public List<GenTableColumn> Columns { get; set; }

        /// <summary>
        /// 字表信息
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public GenTable SubTable { get; set; }
        #endregion
    }

    public class CodeOptions
    {
        public long ParentMenuId { get; set; }
        public string SortType { get; set; } = "asc";
        public string SortField { get; set; } = string.Empty;
        public string TreeCode { get; set; } = string.Empty;
        public string TreeName { get; set; } = string.Empty;
        public string TreeParentCode { get; set; } = string.Empty;
        public string PermissionPrefix { get; set; } = string.Empty;
        /// <summary>
        /// 额外参数字符串
        /// </summary>
        public int[] CheckedBtn { get; set; } = new int[] { 1, 2, 3 };
        /// <summary>
        /// 列大小 12,24
        /// </summary>
        public int ColNum { get; set; } = 12;
        /// <summary>
        /// 是否生成仓储层
        /// </summary>
        public int GenerateRepo { get; set; }
        /// <summary>
        /// 自动生成菜单
        /// </summary>
        public bool GenerateMenu { get; set; }
        /// <summary>
        /// 操作按钮样式
        /// </summary>
        public int OperBtnStyle { get; set; } = 1;
        /// <summary>
        /// 是否使用雪花id
        /// </summary>
        public bool UseSnowflakeId { get; set; } = false;
        /// <summary>
        /// 是否启用日志(编辑、删除)自动记录日志
        /// </summary>
        public bool EnableLog { get; set; }
        /// <summary>
        /// 前端模板 1、element ui 2、element plus
        /// </summary>
        public int FrontTpl { get; set; } = 2;
    }
}
