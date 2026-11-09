using System.Collections.Generic;
using ZR.Model.System.Generate;

namespace ZR.CodeGenerator.Model
{
    /// <summary>
    /// 代码生成契约：把 gen_table / gen_table_column / code_options 中的人工配置整理成
    /// 结构化描述，供 AI / 编码助手作为上下文使用。只做描述，不参与模板渲染。
    /// </summary>
    public class CodeGenSpec
    {
        /// <summary>
        /// 表基本信息
        /// </summary>
        public SpecTable Table { get; set; }
        /// <summary>
        /// 原始生成选项（与前端配置页一致）
        /// </summary>
        public CodeOptions Options { get; set; }
        /// <summary>
        /// 各层命名空间
        /// </summary>
        public SpecNamespaces Namespaces { get; set; }
        /// <summary>
        /// 生成的目标文件（相对项目根目录）
        /// </summary>
        public List<SpecFile> TargetFiles { get; set; } = new();
        /// <summary>
        /// 主表字段
        /// </summary>
        public List<SpecColumn> Columns { get; set; } = new();
        /// <summary>
        /// 子表（主子表模式）
        /// </summary>
        public List<SpecSubTable> SubTables { get; set; } = new();
        /// <summary>
        /// 权限码清单
        /// </summary>
        public List<SpecPermission> Permissions { get; set; } = new();
        /// <summary>
        /// 接口契约
        /// </summary>
        public List<SpecApi> Apis { get; set; } = new();
        /// <summary>
        /// 行为开关（软删除/雪花ID/日志/上传等）
        /// </summary>
        public SpecFeatures Features { get; set; }
        /// <summary>
        /// 本项目生成代码必须遵守的约定
        /// </summary>
        public List<string> Conventions { get; set; } = new();
    }

    /// <summary>
    /// 表基本信息
    /// </summary>
    public class SpecTable
    {
        public string TableName { get; set; }
        public string TableComment { get; set; }
        public string DbName { get; set; }
        /// <summary>
        /// 数据库类型名称，如 SQL Server
        /// </summary>
        public string DbType { get; set; }
        /// <summary>
        /// 模板类型：crud/tree/sub/subNav/subNavMore
        /// </summary>
        public string TplCategory { get; set; }
        /// <summary>
        /// 模板类型中文说明
        /// </summary>
        public string TplCategoryText { get; set; }
        public string ClassName { get; set; }
        public string ModuleName { get; set; }
        public string BusinessName { get; set; }
        public string FunctionName { get; set; }
        public string BaseNameSpace { get; set; }
        public string FunctionAuthor { get; set; }
    }

    /// <summary>
    /// 各层命名空间
    /// </summary>
    public class SpecNamespaces
    {
        public string Model { get; set; }
        public string Dto { get; set; }
        public string Service { get; set; }
        public string IService { get; set; }
        public string Repository { get; set; }
        public string Controller { get; set; }
    }

    /// <summary>
    /// 生成的目标文件
    /// </summary>
    public class SpecFile
    {
        /// <summary>
        /// 所在层：Model/Dto/IService/Service/Controller/Repository/Vue/Api/Sql
        /// </summary>
        public string Layer { get; set; }
        /// <summary>
        /// 相对项目根目录的路径
        /// </summary>
        public string Path { get; set; }
    }

    /// <summary>
    /// 字段描述
    /// </summary>
    public class SpecColumn
    {
        public string ColumnName { get; set; }
        public string CsharpField { get; set; }
        public string CsharpType { get; set; }
        public string ColumnType { get; set; }
        public string Comment { get; set; }
        public string Remark { get; set; }
        public bool IsPk { get; set; }
        public bool IsIncrement { get; set; }
        public bool IsRequired { get; set; }
        public bool IsInsert { get; set; }
        public bool IsEdit { get; set; }
        public bool IsList { get; set; }
        public bool IsQuery { get; set; }
        public bool IsSort { get; set; }
        public bool IsExport { get; set; }
        /// <summary>
        /// 前端控件类型：input/textarea/select/selectMulti/radio/checkbox/datetime/imageUpload/fileUpload/editor/customInput/colorPicker
        /// </summary>
        public string HtmlType { get; set; }
        /// <summary>
        /// 查询方式：EQ/NE/LIKE/GT/GTE/LT/LTE/BETWEEN
        /// </summary>
        public string QueryType { get; set; }
        /// <summary>
        /// 字典类型（为空表示不用字典）
        /// </summary>
        public string DictType { get; set; }
        /// <summary>
        /// 自动填充：1新增 2编辑 3新增+编辑
        /// </summary>
        public int AutoFillType { get; set; }
        public int Sort { get; set; }
    }

    /// <summary>
    /// 子表描述
    /// </summary>
    public class SpecSubTable
    {
        public string TableName { get; set; }
        public string ClassName { get; set; }
        public string TableComment { get; set; }
        /// <summary>
        /// 关联主表的外键字段
        /// </summary>
        public string ParentFkName { get; set; }
        public List<SpecColumn> Columns { get; set; } = new();
    }

    /// <summary>
    /// 权限码
    /// </summary>
    public class SpecPermission
    {
        public string Code { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// 接口契约
    /// </summary>
    public class SpecApi
    {
        public string Method { get; set; }
        public string Route { get; set; }
        public string Action { get; set; }
        public string Permission { get; set; }
    }

    /// <summary>
    /// 行为开关
    /// </summary>
    public class SpecFeatures
    {
        public string PrimaryKey { get; set; }
        public string PrimaryKeyType { get; set; }
        /// <summary>
        /// 是否使用雪花ID
        /// </summary>
        public bool UseSnowflakeId { get; set; }
        /// <summary>
        /// 是否启用软删除
        /// </summary>
        public bool SoftDelete { get; set; }
        public string SoftDeleteField { get; set; }
        public string SoftDeleteFieldType { get; set; }
        public string SoftDeleteNormalValue { get; set; }
        public string SoftDeleteDeletedValue { get; set; }
        /// <summary>
        /// 是否自动记录操作日志
        /// </summary>
        public bool EnableLog { get; set; }
        /// <summary>
        /// 是否存在图片/文件上传字段
        /// </summary>
        public bool HasUploadFile { get; set; }
        /// <summary>
        /// 是否存在下拉多选字段
        /// </summary>
        public bool HasMultiSelect { get; set; }
        /// <summary>
        /// 是否存在富文本字段
        /// </summary>
        public bool HasEditor { get; set; }
        /// <summary>
        /// 是否存在行内编辑字段（生成 ChangeSort 接口）
        /// </summary>
        public bool HasCustomInput { get; set; }
    }
}
