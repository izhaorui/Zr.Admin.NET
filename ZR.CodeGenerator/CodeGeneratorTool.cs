using Infrastructure;
using JinianNet.JNTemplate;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZR.CodeGenerator.Model;
using ZR.Model.System.Generate;

namespace ZR.CodeGenerator
{
    /// <summary>
    /// 代码生成器
    /// </remarks>
    /// </summary>
    public class CodeGeneratorTool
    {
        /// <summary>
        /// 代码生成器配置
        /// </summary>
        private static CodeGenerateOption _option = new CodeGenerateOption();

        /// <summary>
        /// 代码生成器入口方法
        /// </summary>
        /// <param name="dto"></param>
        public static void Generate(GenerateDto dto)
        {
            _option.BaseNamespace = dto.GenTable.BaseNameSpace;
            _option.DtosNamespace = _option.BaseNamespace + "Model";
            _option.ModelsNamespace = _option.BaseNamespace + "Model";
            _option.RepositoriesNamespace = _option.BaseNamespace + "Repository";
            _option.IRepositoriesNamespace = _option.BaseNamespace + "Repository";
            _option.IServicsNamespace = _option.BaseNamespace + "Service";
            _option.ServicesNamespace = _option.BaseNamespace + "Service";
            _option.ApiControllerNamespace = _option.BaseNamespace + "Admin.WebApi";

            dto.GenOptions = _option;
            GenerateSingle(dto);
        }

        /// <summary>
        /// 单表生成代码
        /// </summary>
        /// <param name="dto"></param>
        public static void GenerateSingle(GenerateDto dto)
        {
            string PKName = "Id";
            string PKType = "int";
            ReplaceDto replaceDto = new();
            replaceDto.ModelTypeName = dto.GenTable.ClassName;//表名对应C# 实体类名
            replaceDto.PermissionPrefix = $"{dto.GenTable.ModuleName}:{dto.GenTable.ClassName.ToLower()}";//权限
            replaceDto.Author = dto.GenTable.FunctionAuthor;
            replaceDto.ShowBtnAdd = dto.CheckedBtn.Any(f => f == 1);
            replaceDto.ShowBtnEdit = dto.CheckedBtn.Any(f => f == 2);
            replaceDto.ShowBtnDelete = dto.CheckedBtn.Any(f => f == 3);
            replaceDto.ShowBtnExport = dto.CheckedBtn.Any(f => f == 4);

            //循环表字段信息
            foreach (GenTableColumn dbFieldInfo in dto.GenTable.Columns)
            {
                if (dbFieldInfo.IsPk || dbFieldInfo.IsIncrement)
                {
                    PKName = dbFieldInfo.CsharpField;
                    PKType = dbFieldInfo.CsharpType;
                }
                if (dbFieldInfo.HtmlType.Equals(GenConstants.HTML_IMAGE_UPLOAD) || dbFieldInfo.HtmlType.Equals(GenConstants.HTML_FILE_UPLOAD))
                {
                    replaceDto.UploadFile = 1;
                }
                //CodeGenerateTemplate.GetQueryDtoProperty(dbFieldInfo, replaceDto);

                replaceDto.VueViewFormHtml += CodeGenerateTemplate.TplVueFormContent(dbFieldInfo);
                replaceDto.VueViewListHtml += CodeGenerateTemplate.TplTableColumn(dbFieldInfo, dto.GenTable);
                replaceDto.VueQueryFormHtml += CodeGenerateTemplate.TplQueryFormHtml(dbFieldInfo);
            }

            replaceDto.PKName = PKName;
            replaceDto.PKType = PKType;
            replaceDto.FistLowerPk = FirstLowerCase(PKName);
            InitJntTemplate(dto, replaceDto);

            GenerateModels(replaceDto, dto);
            GenerateInputDto(replaceDto, dto);
            GenerateRepository(replaceDto, dto);
            GenerateService(replaceDto, dto);
            GenerateControllers(replaceDto, dto);
            GenerateVueViews(replaceDto, dto);
            GenerateVueJs(replaceDto, dto);
            GenerateSql(replaceDto, dto);

            if (dto.IsPreview == 1) return;

            foreach (var item in dto.GenCodes)
            {
                FileHelper.WriteAndSave(item.Path, item.Content);
            }
        }

        #region 生成Model

        /// <summary>
        /// 生成实体类Model
        /// </summary>
        /// <param name="generateDto"></param>
        /// <param name="replaceDto">替换实体</param>
        private static void GenerateModels(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var fullPath = Path.Combine(generateDto.GenCodePath, _option.ModelsNamespace, "Models", generateDto.GenTable.ModuleName, replaceDto.ModelTypeName + ".cs");

            var tpl = FileHelper.ReadJtTemplate("TplModel.txt");
            var result = tpl.Render();

            generateDto.GenCodes.Add(new GenCode(1, "Model", fullPath, result));
        }

        /// <summary>
        /// 生成表单提交输入参数Dto
        /// </summary>
        /// <param name="generateDto"></param>
        /// <param name="replaceDto">替换实体</param>
        private static void GenerateInputDto(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var fullPath = Path.Combine(generateDto.GenCodePath, _option.ModelsNamespace, "Dto", generateDto.GenTable.ModuleName, $"{replaceDto.ModelTypeName}Dto.cs");

            var tpl = FileHelper.ReadJtTemplate("TplDto.txt");

            var result = tpl.Render();
            generateDto.GenCodes.Add(new GenCode(2, "Dto", fullPath, result));
        }
        #endregion

        #region 生成Repository

        /// <summary>
        /// 生成Repository层代码文件
        /// </summary>
        /// <param name="generateDto"></param>
        /// <param name="replaceDto">替换实体</param>
        private static void GenerateRepository(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var fullPath = Path.Combine(generateDto.GenCodePath, _option.RepositoriesNamespace, "Repositories", $"{replaceDto.ModelTypeName}Repository.cs");
            var tpl = FileHelper.ReadJtTemplate("TplRepository.txt");

            var result = tpl.Render();
            generateDto.GenCodes.Add(new GenCode(3, "Repository", fullPath, result));
        }

        #endregion

        #region 生成Service

        /// <summary>
        /// 生成Service文件
        /// </summary>
        private static void GenerateService(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var fullPath = Path.Combine(generateDto.GenCodePath, _option.ServicesNamespace, "Business", $"{replaceDto.ModelTypeName}Service.cs");
            var tpl = FileHelper.ReadJtTemplate("TplService.txt");

            var result = tpl.Render();
            generateDto.GenCodes.Add(new GenCode(4, "Service", fullPath, result));

            var fullPath2 = Path.Combine(generateDto.GenCodePath, _option.IServicsNamespace, "Business", "IBusService", $"I{replaceDto.ModelTypeName}Service.cs");
            var tpl2 = FileHelper.ReadJtTemplate("TplIService.txt");

            var result2 = tpl2.Render();
            generateDto.GenCodes.Add(new GenCode(4, "IService", fullPath2, result2));
        }

        #endregion

        #region 生成Controller
        /// <summary>
        /// 生成控制器ApiControllers文件
        /// </summary>
        private static void GenerateControllers(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var fullPath = Path.Combine(generateDto.GenCodePath, _option.ApiControllerNamespace, "Controllers", generateDto.GenTable.ModuleName, $"{replaceDto.ModelTypeName}Controller.cs");
            var tpl = FileHelper.ReadJtTemplate("TplControllers.txt");

            tpl.Set("QueryCondition", replaceDto.QueryCondition);
            var result = tpl.Render();
            generateDto.GenCodes.Add(new GenCode(5, "Controller", fullPath, result));
        }
        #endregion

        #region 生成Vue页面 & api
        /// <summary>
        /// 6、生成Vue页面
        private static void GenerateVueViews(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var fullPath = Path.Combine(generateDto.GenCodePath, "ZR.Vue", "src", "views", generateDto.GenTable.ModuleName, $"{generateDto.GenTable.BusinessName}.vue");

            var tpl = FileHelper.ReadJtTemplate("TplVue.txt");
            tpl.Set("vueQueryFormHtml", replaceDto.VueQueryFormHtml);
            tpl.Set("VueViewEditFormRuleContent", replaceDto.VueViewEditFormRuleContent);//添加、修改表单验证规则
            tpl.Set("VueViewFormContent", replaceDto.VueViewFormHtml);//添加、修改表单
            tpl.Set("VueViewListContent", replaceDto.VueViewListHtml);//查询 table列
            tpl.Set("lowerBusinessName", FirstLowerCase(generateDto.GenTable.BusinessName));

            var result = tpl.Render();
            generateDto.GenCodes.Add(new GenCode(6, "index.vue", fullPath, result));
        }
        /// <summary>
        /// 7、生成vue页面api
        /// </summary>
        /// <param name="replaceDto"></param>
        /// <param name="generateDto"></param>
        /// <returns></returns>
        public static void GenerateVueJs(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            string fullPath = Path.Combine(generateDto.GenCodePath, "ZR.Vue", "src", "api", generateDto.GenTable.ModuleName, FirstLowerCase(generateDto.GenTable.BusinessName) + ".js");
            var tpl = FileHelper.ReadJtTemplate("TplVueApi.txt");

            var result = tpl.Render();
            generateDto.GenCodes.Add(new GenCode(7, "api.js", fullPath, result));
        }

        #endregion

        #region 生成SQL

        public static void GenerateSql(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            string fullPath = Path.Combine(generateDto.GenCodePath, generateDto.GenTable.BusinessName + ".sql");

            var tempName = "";
            switch (generateDto.DbType)
            {
                case 0:
                    tempName = "MySqlTemplate";
                    break;
                case 1:
                    tempName = "SqlTemplate";
                    break;
                default:
                    break;
            }
            var tpl = FileHelper.ReadJtTemplate($"{tempName}.txt");
            tpl.Set("parentId", generateDto.GenTable.ParentMenuId ?? 0);
            var result = tpl.Render();
            generateDto.GenCodes.Add(new GenCode(8, "sql", fullPath, result));
        }

        #endregion

        #region 帮助方法

        /// <summary>
        /// 如果有前缀替换将前缀替换成空，替换下划线"_"为空再将首字母大写
        /// 表名转换成C#类名
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public static string GetClassName(string tableName)
        {
            bool autoRemovePre = ConfigUtils.Instance.GetAppConfig(GenConstants.Gen_autoPre, false);
            string tablePrefix = ConfigUtils.Instance.GetAppConfig<string>(GenConstants.Gen_tablePrefix);

            if (!string.IsNullOrEmpty(tablePrefix) && autoRemovePre)
            {
                string[] searcList = tablePrefix.Split(",", StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < searcList.Length; i++)
                {
                    if (!string.IsNullOrEmpty(searcList[i].ToString()))
                    {
                        tableName = tableName.Replace(searcList[i], "", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            return tableName.Substring(0, 1).ToUpper() + tableName[1..].Replace("_", "");
        }

        /// <summary>
        /// 获取业务名
        /// </summary>
        /// <param name="tableName">tableName 表名</param>
        /// <returns>业务名</returns>
        public static string GetBusinessName(string tableName)
        {
            //int firstIndex = tableName.IndexOf("_");//_前缀长度
            //int nameLength = tableName.Length;
            //int subLength = (nameLength - lastIndex) - 1;
            //string businessName = tableName[(lastIndex + 1)..];
            return tableName.Substring(0, 1).ToUpper() + tableName[1..].Replace("_", "");
        }

        /// <summary>
        /// 首字母转小写，输出前端
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string FirstLowerCase(string str)
        {
            return string.IsNullOrEmpty(str) ? str : str.Substring(0, 1).ToLower() + str[1..];
        }

        /// <summary>
        /// 获取前端标签名
        /// </summary>
        /// <param name="columnDescription"></param>
        /// <param name="columnName"></param>
        /// <returns></returns>
        public static string GetLabelName(string columnDescription, string columnName)
        {
            return string.IsNullOrEmpty(columnDescription) ? columnName : columnDescription;
        }
        /// <summary>
        /// 获取C# 类型
        /// </summary>
        /// <param name="sDatatype"></param>
        /// <returns></returns>
        public static string GetCSharpDatatype(string sDatatype)
        {
            sDatatype = sDatatype.ToLower();
            string sTempDatatype = sDatatype switch
            {
                "int" or "number" or "integer" or "smallint" => "int",
                "bigint" => "long",
                "tinyint" => "byte",
                "numeric" or "real" or "float" => "float",
                "decimal" or "numer(8,2)" or "numeric" => "decimal",
                "bit" => "bool",
                "date" or "datetime" or "datetime2" or "smalldatetime" or "timestamp" => "DateTime",
                "money" or "smallmoney" => "decimal",
                _ => "string",
            };
            return sTempDatatype;
        }

        public static bool IsNumber(string tableDataType)
        {
            string[] arr = new string[] { "int", "long" };
            return arr.Any(f => f.Contains(GetCSharpDatatype(tableDataType)));
        }
        #endregion

        /// <summary>
        /// 初始化列属性字段数据
        /// </summary>
        /// <param name="genTable"></param>
        /// <param name="dbColumnInfos"></param>
        public static List<GenTableColumn> InitGenTableColumn(GenTable genTable, List<DbColumnInfo> dbColumnInfos)
        {
            List<GenTableColumn> genTableColumns = new();
            foreach (var column in dbColumnInfos)
            {
                GenTableColumn genTableColumn = new()
                {
                    ColumnName = FirstLowerCase(column.DbColumnName),
                    ColumnComment = column.ColumnDescription,
                    IsPk = column.IsPrimarykey,
                    ColumnType = column.DataType,
                    TableId = genTable.TableId,
                    TableName = genTable.TableName,
                    CsharpType = GetCSharpDatatype(column.DataType),
                    CsharpField = column.DbColumnName.Substring(0, 1).ToUpper() + column.DbColumnName[1..],
                    IsRequired = !column.IsNullable,
                    IsIncrement = column.IsIdentity,
                    Create_by = genTable.Create_by,
                    Create_time = DateTime.Now,
                    IsInsert = !column.IsIdentity,//非自增字段都需要插入
                    IsEdit = true,
                    IsQuery = false,
                    HtmlType = GenConstants.HTML_INPUT
                };

                if (GenConstants.imageFiled.Any(f => column.DbColumnName.ToLower().Contains(f.ToLower())))
                {
                    genTableColumn.HtmlType = GenConstants.HTML_IMAGE_UPLOAD;
                }
                else if (GenConstants.COLUMNTYPE_TIME.Any(f => genTableColumn.CsharpType.ToLower().Contains(f.ToLower())))
                {
                    genTableColumn.HtmlType = GenConstants.HTML_DATETIME;
                }
                else if (GenConstants.radioFiled.Any(f => column.DbColumnName.EndsWith(f, StringComparison.OrdinalIgnoreCase)) ||
                    GenConstants.radioFiled.Any(f => column.DbColumnName.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
                {
                    genTableColumn.HtmlType = GenConstants.HTML_RADIO;
                }
                else if (GenConstants.selectFiled.Any(f => column.DbColumnName == f) ||
                    GenConstants.selectFiled.Any(f => column.DbColumnName.EndsWith(f, StringComparison.OrdinalIgnoreCase)))
                {
                    genTableColumn.HtmlType = GenConstants.HTML_SELECT;
                }
                else if (column.Length > 500)
                {
                    genTableColumn.HtmlType = GenConstants.HTML_TEXTAREA;
                }
                //编辑字段
                if (column.IsIdentity || column.IsPrimarykey || GenConstants.COLUMNNAME_NOT_EDIT.Any(f => column.DbColumnName.Contains(f)))
                {
                    genTableColumn.IsEdit = false;
                }
                //列表字段
                if (!GenConstants.COLUMNNAME_NOT_LIST.Any(f => column.DbColumnName.Contains(f) && !column.IsPrimarykey))
                {
                    genTableColumn.IsList = true;
                }
                //时间类型初始化between范围查询
                if (genTableColumn.CsharpType == GenConstants.TYPE_DATE)
                {
                    genTableColumn.QueryType = "BETWEEN";
                }
                genTableColumns.Add(genTableColumn);
            }
            return genTableColumns;
        }

        /// <summary>
        /// 初始化Jnt模板
        /// </summary>
        /// <param name="dto"></param>
        /// <param name="replaceDto"></param>
        private static void InitJntTemplate(GenerateDto dto, ReplaceDto replaceDto)
        {
            //Engine.Current.Clean();

            //jnt模板引擎全局变量
            Engine.Configure((options) =>
            {
                options.TagPrefix = "${";
                options.TagSuffix = "}";
                options.TagFlag = '$';
                options.OutMode = OutMode.Auto;
                //options.DisableeLogogram = true;//禁用简写
                options.Data.Set("refs", "$");//特殊标签替换
                options.Data.Set("confirm", "$");//特殊标签替换
                options.Data.Set("replaceDto", replaceDto);
                options.Data.Set("options", dto.GenOptions);
                options.Data.Set("genTable", dto.GenTable);
                options.Data.Set("btns", dto.CheckedBtn);
                options.Data.Set("tool", new CodeGeneratorTool());
                options.Data.Set("codeTool", new CodeGenerateTemplate());
                options.EnableCache = true;
                //...其它数据
            });
        }
    }
}
