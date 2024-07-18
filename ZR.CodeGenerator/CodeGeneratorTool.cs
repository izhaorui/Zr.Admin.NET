using Infrastructure;
using Infrastructure.Extensions;
using Infrastructure.Helper;
using Infrastructure.Model;
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
        private static readonly string CodeTemplateDir = "CodeGenTemplate";

        /// <summary>
        /// 代码生成器入口方法
        /// </summary>
        /// <param name="dto"></param>
        public static void Generate(GenerateDto dto)
        {
            var genOptions = AppSettings.Get<CodeGen>("codeGen");
            dto.VueParentPath = "Frontend";
            if (!genOptions.VuePath.IsEmpty())
            {
                dto.VueParentPath = genOptions.VuePath;
            }
            if (genOptions.UniappPath.IsNotEmpty())
            {
                dto.AppVuePath = genOptions.UniappPath;
            }
            dto.GenOptions = GenerateOption(dto.GenTable);
            if (dto.GenTable.SubTable != null)
            {
                dto.SubTableOptions = GenerateOption(dto.GenTable.SubTable);
            }
            if (dto.GenTable.SubTableName.IsNotEmpty() && dto.GenTable.SubTable == null)
            {
                throw new CustomException($"{dto.GenTable.SubTableName}子表不存在");
            }

            ReplaceDto replaceDto = new()
            {
                ModelTypeName = dto.GenTable.ClassName,//表名对应C# 实体类名
                PermissionPrefix = dto.GenTable?.Options?.PermissionPrefix,
                Author = dto.GenTable.FunctionAuthor,
                ShowBtnAdd = dto.GenTable.Options.CheckedBtn.Any(f => f == 1),
                ShowBtnEdit = dto.GenTable.Options.CheckedBtn.Any(f => f == 2),
                ShowBtnDelete = dto.GenTable.Options.CheckedBtn.Any(f => f == 3),
                ShowBtnExport = dto.GenTable.Options.CheckedBtn.Any(f => f == 4),
                ShowBtnView = dto.GenTable.Options.CheckedBtn.Any(f => f == 5),
                ShowBtnTruncate = dto.GenTable.Options.CheckedBtn.Any(f => f == 6),
                ShowBtnMultiDel = dto.GenTable.Options.CheckedBtn.Any(f => f == 7),
                ShowBtnImport = dto.GenTable.Options.CheckedBtn.Any(f => f == 8),
                ViewFileName = dto.GenTable.BusinessName.FirstUpperCase(),
                OperBtnStyle = dto.GenTable.Options.OperBtnStyle,
                UseSnowflakeId = dto.GenTable.Options.UseSnowflakeId,
                EnableLog = dto.GenTable.Options.EnableLog
            };
            var columns = dto.GenTable.Columns;

            replaceDto.PKName = columns.Find(f => f.IsPk || f.IsIncrement)?.CsharpField ?? "Id";
            replaceDto.PKType = columns.Find(f => f.IsPk || f.IsIncrement)?.CsharpType ?? "int";

            replaceDto.UploadFile = columns.Any(f => f.HtmlType.Equals(GenConstants.HTML_IMAGE_UPLOAD) || f.HtmlType.Equals(GenConstants.HTML_FILE_UPLOAD)) ? 1 : 0;
            replaceDto.SelectMulti = columns.Any(f => f.HtmlType.Equals(GenConstants.HTML_SELECT_MULTI)) ? 1 : 0;
            replaceDto.ShowEditor = columns.Any(f => f.HtmlType.Equals(GenConstants.HTML_EDITOR)) ? 1 : 0;
            replaceDto.FistLowerPk = replaceDto.PKName.FirstLowerCase();
            InitJntTemplate(dto, replaceDto);

            GenerateModels(replaceDto, dto);
            GenerateService(replaceDto, dto);
            GenerateControllers(replaceDto, dto);
            if (dto.GenTable.Options.FrontTpl == 2)
            {
                GenerateVue3Views(replaceDto, dto);
            }
            else if (dto.GenTable.Options.FrontTpl == 1)
            {
                GenerateVueViews(replaceDto, dto);
            }
            else
            {
                GenerateAntDesignViews(replaceDto, dto);
            }
            if (dto.GenTable.Options.GenerateRepo == 1)
            {
                GenerateRepository(replaceDto, dto);
            }
            GenerateVueJs(dto);
            GenerateSql(dto);
            if (genOptions.ShowApp)
            {
                GenerateAppVueViews(replaceDto, dto, genOptions.UniappVersion);
                GenerateAppVueFormViews(replaceDto, dto, genOptions.UniappVersion);
                GenerateAppJs(dto);
            }
            dto.ReplaceDto = replaceDto;
        }

        private static CodeGenerateOption GenerateOption(GenTable genTable)
        {
            CodeGenerateOption _option = new()
            {
                BaseNamespace = genTable.BaseNameSpace,
                SubNamespace = genTable.ModuleName.FirstUpperCase()
            };
            _option.DtosNamespace = _option.BaseNamespace + "Model";
            _option.ModelsNamespace = _option.BaseNamespace + "Model";
            _option.RepositoriesNamespace = _option.BaseNamespace + "Repository";
            //_option.IRepositoriesNamespace = _option.BaseNamespace + "Repository";
            _option.IServicsNamespace = _option.BaseNamespace + "Service";
            _option.ServicesNamespace = _option.BaseNamespace + "Service";
            _option.ApiControllerNamespace = _option.BaseNamespace + "Admin.WebApi";
            return _option;
        }

        #region 读取模板

        /// <summary>
        /// 生成实体类Model
        /// </summary>
        /// <param name="generateDto"></param>
        /// <param name="replaceDto">替换实体</param>
        private static void GenerateModels(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var path = Path.Combine(CodeTemplateDir, "csharp");
            var tpl = JnHelper.ReadTemplate(path, "TplModel.txt");
            var tplDto = JnHelper.ReadTemplate(path, "TplDto.txt");

            string fullPath = Path.Combine(generateDto.GenOptions.ModelsNamespace, generateDto.GenOptions.SubNamespace, replaceDto.ModelTypeName + ".cs");
            string fullPathDto = Path.Combine(generateDto.GenOptions.ModelsNamespace, generateDto.GenOptions.SubNamespace, "Dto", replaceDto.ModelTypeName + "Dto.cs");

            generateDto.GenCodes.Add(new GenCode(1, "Model.cs", fullPath, tpl.Render()));
            generateDto.GenCodes.Add(new GenCode(2, "Dto.cs", fullPathDto, tplDto.Render()));
        }

        /// <summary>
        /// 生成Repository层代码文件
        /// </summary>
        /// <param name="generateDto"></param>
        /// <param name="replaceDto">替换实体</param>
        private static void GenerateRepository(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var tpl = JnHelper.ReadTemplate(Path.Combine(CodeTemplateDir, "csharp"), "TplRepository.txt");
            var result = tpl.Render();
            var fullPath = Path.Combine(generateDto.GenOptions.RepositoriesNamespace, generateDto.GenOptions.SubNamespace, $"{replaceDto.ModelTypeName}Repository.cs");

            generateDto.GenCodes.Add(new GenCode(3, "Repository.cs", fullPath, result));
        }

        /// <summary>
        /// 生成Service文件
        /// </summary>
        private static void GenerateService(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var path = Path.Combine(CodeTemplateDir, "csharp");
            var tpl = JnHelper.ReadTemplate(path, "TplService.txt");
            var tpl2 = JnHelper.ReadTemplate(path, "TplIService.txt");

            var fullPath = Path.Combine(generateDto.GenOptions.ServicesNamespace, generateDto.GenOptions.SubNamespace, $"{replaceDto.ModelTypeName}Service.cs");
            var fullPath2 = Path.Combine(generateDto.GenOptions.IServicsNamespace, generateDto.GenOptions.SubNamespace, $"I{generateDto.GenOptions.SubNamespace}Service", $"I{replaceDto.ModelTypeName}Service.cs");

            generateDto.GenCodes.Add(new GenCode(4, "Service.cs", fullPath, tpl.Render()));
            generateDto.GenCodes.Add(new GenCode(4, "IService.cs", fullPath2, tpl2.Render()));
        }

        /// <summary>
        /// 生成控制器ApiControllers文件
        /// </summary>
        private static void GenerateControllers(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var tpl = JnHelper.ReadTemplate(Path.Combine(CodeTemplateDir, "csharp"), "TplControllers.txt");
            tpl.Set("QueryCondition", replaceDto.QueryCondition);
            var result = tpl.Render();

            var fullPath = Path.Combine(generateDto.GenOptions.ApiControllerNamespace, "Controllers", generateDto.GenOptions.SubNamespace, $"{replaceDto.ModelTypeName}Controller.cs");
            generateDto.GenCodes.Add(new GenCode(5, "Controller.cs", fullPath, result));
        }

        /// <summary>
        /// 生成Vue页面
        private static void GenerateVueViews(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            string fileName = generateDto.GenTable.TplCategory switch
            {
                "tree" => "TplTreeVue.txt",
                "select" => "TplVueSelect.txt",
                _ => "TplVue.txt",
            };
            var path = Path.Combine(CodeTemplateDir, "vue2");
            fileName = Path.Combine("vue2", fileName);
            replaceDto.VueViewListHtml = JnHelper.ReadTemplate(path, "TableList.txt").Render();
            replaceDto.VueQueryFormHtml = JnHelper.ReadTemplate(path, "QueryForm.txt").Render();
            replaceDto.VueViewFormHtml = JnHelper.ReadTemplate(path, "CurdForm.txt").Render();

            var tpl = JnHelper.ReadTemplate(CodeTemplateDir, fileName);
            var fullPath = Path.Combine(generateDto.VueParentPath, "src", "views", generateDto.GenTable.ModuleName.FirstLowerCase(), $"{replaceDto.ViewFileName}.vue");

            generateDto.GenCodes.Add(new GenCode(6, "index.vue", fullPath, tpl.Render()));
        }

        /// <summary>
        /// vue3
        /// </summary>
        /// <param name="generateDto"></param>
        private static void GenerateVue3Views(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            string fileName = generateDto.GenTable.TplCategory switch
            {
                "tree" => "TreeVue.txt",
                _ => "Vue.txt",
            };
            fileName = Path.Combine("v3", fileName);
            var tpl = JnHelper.ReadTemplate(CodeTemplateDir, fileName);
            tpl.Set("treeCode", generateDto.GenTable?.Options?.TreeCode?.FirstLowerCase());
            tpl.Set("treeName", generateDto.GenTable?.Options?.TreeName?.FirstLowerCase());
            tpl.Set("treeParentCode", generateDto.GenTable?.Options?.TreeParentCode?.FirstLowerCase());
            tpl.Set("options", generateDto.GenTable?.Options);

            var result = tpl.Render();
            var fullPath = Path.Combine(generateDto.VueParentPath, "src", "views", generateDto.GenTable.ModuleName.FirstLowerCase(), $"{replaceDto.ViewFileName}.vue");
            generateDto.GenCodes.Add(new GenCode(16, "index.vue", fullPath, result));
        }

        /// <summary>
        /// AntDesign
        /// </summary>
        /// <param name="generateDto"></param>
        private static void GenerateAntDesignViews(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            string fileName = generateDto.GenTable.TplCategory switch
            {
                "tree" => "TreeVue.txt",
                _ => "Vue2.txt",
            };
            fileName = Path.Combine("antDesign", fileName);
            var tpl = JnHelper.ReadTemplate(CodeTemplateDir, fileName);
            tpl.Set("treeCode", generateDto.GenTable?.Options?.TreeCode?.FirstLowerCase());
            tpl.Set("treeName", generateDto.GenTable?.Options?.TreeName?.FirstLowerCase());
            tpl.Set("treeParentCode", generateDto.GenTable?.Options?.TreeParentCode?.FirstLowerCase());
            tpl.Set("options", generateDto.GenTable?.Options);

            var result = tpl.Render();
            var fullPath = Path.Combine(generateDto.VueParentPath, "src", "views", generateDto.GenTable.ModuleName.FirstLowerCase(), $"{replaceDto.ViewFileName}.vue");
            generateDto.GenCodes.Add(new GenCode(16, "index.vue", fullPath, result));
        }

        /// <summary>
        /// 生成vue页面api
        /// </summary>
        /// <param name="generateDto"></param>
        /// <returns></returns>
        public static void GenerateVueJs(GenerateDto generateDto)
        {
            var tpl = JnHelper.ReadTemplate(CodeTemplateDir, "TplVueApi.txt");
            var result = tpl.Render();

            string fileName;
            if (generateDto.VueVersion == 3)
            {
                fileName = generateDto.GenTable.BusinessName.ToLower() + ".js";//vue3 api引用目前只能小写
            }
            else
            {
                fileName = generateDto.GenTable.BusinessName.FirstLowerCase() + ".js";
            }
            string fullPath = Path.Combine(generateDto.VueParentPath, "src", "api", generateDto.GenTable.ModuleName.FirstLowerCase(), fileName);

            generateDto.GenCodes.Add(new GenCode(7, "api.js", fullPath, result));
        }

        /// <summary>
        /// 生成SQL
        /// </summary>
        /// <param name="generateDto"></param>
        public static void GenerateSql(GenerateDto generateDto)
        {
            string tempName = generateDto.DbType switch
            {
                0 => "MySqlTemplate",
                1 => "SqlTemplate",
                4 => "PgSql",
                _ => "Other",
            };
            var tpl = JnHelper.ReadTemplate(CodeTemplateDir, Path.Combine("sql", $"{tempName}.txt"));
            tpl.Set("parentId", generateDto.GenTable?.Options?.ParentMenuId ?? 0);
            var result = tpl.Render();
            string fullPath = Path.Combine(generateDto.GenCodePath, "sql", generateDto.GenTable.BusinessName + ".sql");

            generateDto.GenCodes.Add(new GenCode(8, "sql菜单", fullPath, result));
        }

        #endregion

        #region app页面

        /// <summary>
        /// 列表页面
        /// </summary>
        /// <param name="generateDto"></param>
        /// <param name="replaceDto"></param>
        /// <param name="uniappVersion"></param>
        private static void GenerateAppVueViews(ReplaceDto replaceDto, GenerateDto generateDto, int uniappVersion)
        {
            var fileName = Path.Combine("app", $"vue{uniappVersion}.txt");
            var tpl = JnHelper.ReadTemplate(CodeTemplateDir, fileName);

            tpl.Set("options", generateDto.GenTable?.Options);

            var result = tpl.Render();
            var fullPath = Path.Combine(generateDto.AppVuePath, "pages", generateDto.GenTable.ModuleName.FirstLowerCase(), $"{replaceDto.ViewFileName.FirstLowerCase()}", "index.vue");
            generateDto.GenCodes.Add(new GenCode(20, "uniapp页面", fullPath, result));
        }

        private static void GenerateAppVueFormViews(ReplaceDto replaceDto, GenerateDto generateDto, int uniappVersion)
        {
            var fileName = Path.Combine("app", uniappVersion == 3 ? "form3.txt" : "form.txt");
            var tpl = JnHelper.ReadTemplate(CodeTemplateDir, fileName);

            tpl.Set("options", generateDto.GenTable?.Options);

            var result = tpl.Render();
            var fullPath = Path.Combine(generateDto.AppVuePath, "pages", generateDto.GenTable.ModuleName.FirstLowerCase(), $"{replaceDto.ViewFileName.FirstLowerCase()}", "edit.vue");
            generateDto.GenCodes.Add(new GenCode(20, "uniapp表单", fullPath, result));
        }
        /// <summary>
        /// 生成vue页面api
        /// </summary>
        /// <param name="generateDto"></param>
        /// <returns></returns>
        public static void GenerateAppJs(GenerateDto generateDto)
        {
            var filePath = Path.Combine("app", "api.txt");
            var tpl = JnHelper.ReadTemplate(CodeTemplateDir, filePath);
            var result = tpl.Render();

            string fileName = generateDto.GenTable.BusinessName.ToLower() + ".js";
            string fullPath = Path.Combine(generateDto.AppVuePath, "api", generateDto.GenTable.ModuleName.FirstLowerCase(), fileName);

            generateDto.GenCodes.Add(new GenCode(21, "uniapp Api", fullPath, result));
        }
        #endregion

        #region 帮助方法

        /// <summary>
        /// 如果有前缀替换将前缀替换成空，替换下划线"_"为空再将首字母大写
        /// 表名转换成C#类名
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public static string GetClassName(string tableName, bool autoRemovePre, string tablePrefix)
        {
            if (!string.IsNullOrEmpty(tablePrefix) && autoRemovePre)
            {
                string[] prefixList = tablePrefix.Split(",", StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < prefixList.Length; i++)
                {
                    if (!string.IsNullOrEmpty(prefixList[i].ToString()))
                    {
                        tableName = tableName.Replace(prefixList[i], "", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            //return tableName.UnderScoreToCamelCase();
            return tableName.ConvertToPascal("_");
        }

        /// <summary>
        /// 首字母转小写,模板使用(勿删)
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string FirstLowerCase(string str)
        {
            try
            {
                return string.IsNullOrEmpty(str) ? str : str[..1].ToLower() + str[1..];
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return str;
            }
        }

        /// <summary>
        /// 获取C# 类型
        /// </summary>
        /// <param name="sDatatype"></param>
        /// <param name="csharpType"></param>
        /// <returns></returns>
        public static CSharpDataType GetCSharpDatatype(string sDatatype, CsharpTypeArr csharpType)
        {
            sDatatype = sDatatype.ToLower();
            if (csharpType.Int.Contains(sDatatype))
            {
                return CSharpDataType.@int;
            }
            else if (csharpType.Long.Contains(sDatatype))
            {
                return CSharpDataType.@long;
            }
            else if (csharpType.Float.Contains(sDatatype))
            {
                return CSharpDataType.@float;
            }
            else if (csharpType.Decimal.Contains(sDatatype))
            {
                return CSharpDataType.@decimal;
            }
            else if (csharpType.DateTime.Contains(sDatatype))
            {
                return CSharpDataType.DateTime;
            }
            else if (csharpType.Bool.Contains(sDatatype))
            {
                return CSharpDataType.@bool;
            }
            return CSharpDataType.@string;
        }

        #endregion

        #region 初始化信息

        /// <summary>
        /// 初始化表信息
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public static GenTable InitTable(InitTableDto dto)
        {
            var className = GetClassName(dto.TableName, dto.CodeGen.AutoPre, dto.CodeGen.TablePrefix);

            GenTable genTable = new()
            {
                DbName = dto.DbName,
                BaseNameSpace = "ZR.",//导入默认命名空间前缀
                ModuleName = dto.CodeGen.ModuleName,//导入默认模块名
                ClassName = className,
                BusinessName = className,
                FunctionAuthor = dto.CodeGen.Author,
                TableName = dto.TableName,
                TableComment = dto.Desc,
                FunctionName = dto.Desc,
                Create_by = dto.UserName,
                Options = new CodeOptions()
                {
                    SortType = "asc",
                    CheckedBtn = new int[] { 1, 2, 3 },
                    PermissionPrefix = className.ToLower(),
                    FrontTpl = dto.FrontTpl
                }
            };

            return genTable;
        }

        /// <summary>
        /// 初始化列属性字段数据
        /// </summary>
        /// <param name="genTable"></param>
        /// <param name="dbColumnInfos"></param>
        /// <param name="seqs"></param>
        /// <param name="codeGen"></param>
        public static List<GenTableColumn> InitGenTableColumn(GenTable genTable, List<DbColumnInfo> dbColumnInfos, List<OracleSeq> seqs = null, CodeGen codeGen = null)
        {
            OptionsSetting optionsSetting = new();

            var dbConfig = AppSettings.Get<DbConfigs>(nameof(GenConstants.CodeGenDbConfig));

            optionsSetting.CodeGenDbConfig = dbConfig ?? throw new CustomException("代码生成节点数据配置异常"); ;
            optionsSetting.CodeGen = codeGen ?? throw new CustomException("代码生成节点配置异常");

            List<GenTableColumn> genTableColumns = new();
            foreach (var column in dbColumnInfos)
            {
                genTableColumns.Add(InitColumnField(genTable, column, seqs, optionsSetting));
            }
            return genTableColumns;
        }

        /// <summary>
        /// 初始化表字段数据
        /// </summary>
        /// <param name="genTable"></param>
        /// <param name="column"></param>
        /// <param name="optionsSetting"></param>
        /// <param name="seqs">oracle 序列</param>
        /// <returns></returns>
        private static GenTableColumn InitColumnField(GenTable genTable, DbColumnInfo column, List<OracleSeq> seqs, OptionsSetting optionsSetting)
        {
            var dataType = column.DataType;
            if (optionsSetting.CodeGenDbConfig.DbType == 3)
            {
                dataType = column.OracleDataType;
                var seqName = $"SEQ_{genTable.TableName}_{column.DbColumnName}";
                var isIdentity = seqs.Any(f => seqName.Equals(f.SEQUENCE_NAME, StringComparison.CurrentCultureIgnoreCase));
                column.IsIdentity = isIdentity;
            }
            GenTableColumn genTableColumn = new()
            {
                ColumnName = column.DbColumnName.FirstLowerCase(),
                ColumnComment = column.ColumnDescription,
                IsPk = column.IsPrimarykey,
                ColumnType = dataType,
                TableId = genTable.TableId,
                TableName = genTable.TableName,
                CsharpType = GetCSharpDatatype(dataType, optionsSetting.CodeGen.CsharpTypeArr).ToString(),
                CsharpField = column.DbColumnName.ConvertToPascal("_"),
                IsRequired = !column.IsNullable,
                IsIncrement = column.IsIdentity,
                Create_by = genTable.Create_by,
                Create_time = DateTime.Now,
                IsInsert = !column.IsIdentity || GenConstants.inputDtoNoField.Any(f => f.Contains(column.DbColumnName, StringComparison.OrdinalIgnoreCase)),//非自增字段都需要插入
                IsEdit = true,
                IsQuery = false,
                IsExport = true,
                HtmlType = GenConstants.HTML_INPUT,
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

            return genTableColumn;
        }

        #endregion

        /// <summary>
        /// 初始化Jnt模板
        /// </summary>
        /// <param name="dto"></param>
        /// <param name="replaceDto"></param>
        private static void InitJntTemplate(GenerateDto dto, ReplaceDto replaceDto)
        {
#if DEBUG
            Engine.Current.Clean();
#endif
            dto.GenTable.Columns = dto.GenTable.Columns.OrderBy(x => x.Sort).ToList();
            bool showCustomInput = dto.GenTable.Columns.Any(f => f.HtmlType.Equals(GenConstants.HTML_CUSTOM_INPUT, StringComparison.OrdinalIgnoreCase));

            #region 查询所有字典
            var dictHtml = new string[] { GenConstants.HTML_CHECKBOX, GenConstants.HTML_RADIO, GenConstants.HTML_SELECT, GenConstants.HTML_SELECT_MULTI };
            var dicts = new List<GenTableColumn>();
            dicts.AddRange(dto.GenTable.Columns.FindAll(f => dictHtml.Contains(f.HtmlType)));
            if (dto.GenTable.SubTable != null && dto.GenTable.SubTableName.IsNotEmpty())
            {
                dicts.AddRange(dto.GenTable?.SubTable?.Columns?.FindAll(f => dictHtml.Contains(f.HtmlType)));
            }
            #endregion

            //jnt模板引擎全局变量
            Engine.Configure((options) =>
            {
                options.TagPrefix = "${";
                options.TagSuffix = "}";
                options.TagFlag = '$';
                options.OutMode = OutMode.Auto;
                //options.DisableeLogogram = true;//禁用简写
                options.Data.Set("refs", "$");//特殊标签替换
                options.Data.Set("t", "$");//特殊标签替换
                options.Data.Set("modal", "$");//特殊标签替换
                options.Data.Set("alert", "$");//特殊标签替换
                options.Data.Set("index", "$");//特殊标签替换
                options.Data.Set("confirm", "$");//特殊标签替换
                options.Data.Set("nextTick", "$");
                options.Data.Set("tab", "$");
                options.Data.Set("replaceDto", replaceDto);
                options.Data.Set("options", dto.GenOptions);
                options.Data.Set("subTableOptions", dto.SubTableOptions);
                options.Data.Set("genTable", dto.GenTable);
                options.Data.Set("genSubTable", dto.GenTable?.SubTable);
                options.Data.Set("showCustomInput", showCustomInput);
                options.Data.Set("tool", new CodeGeneratorTool());
                options.Data.Set("dicts", dicts.DistinctBy(x => x.DictType));
                options.Data.Set("sub", dto.GenTable.SubTable != null && dto.GenTable.SubTableName.IsNotEmpty());
                options.EnableCache = true;
                //...其它数据
            });
        }

        #region 模板用
        /// <summary>
        /// 模板用
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool CheckInputDtoNoField(string str)
        {
            return GenConstants.inputDtoNoField.Any(f => f.Contains(str, StringComparison.OrdinalIgnoreCase));
        }
        public static bool CheckTree(GenTable genTable, string csharpField)
        {
            return (genTable.TplCategory.Equals("tree", StringComparison.OrdinalIgnoreCase) && genTable?.Options?.TreeParentCode != null && csharpField.Equals(genTable?.Options?.TreeParentCode));
        }
        public static string QueryExp(string propertyName, string queryType)
        {
            if (queryType.Equals("EQ"))
            {
                return $"it => it.{propertyName} == parm.{propertyName})";
            }
            if (queryType.Equals("GTE"))
            {
                return $"it => it.{propertyName} >= parm.{propertyName})";
            }
            if (queryType.Equals("GT"))
            {
                return $"it => it.{propertyName} > parm.{propertyName})";
            }
            if (queryType.Equals("LT"))
            {
                return $"it => it.{propertyName} < parm.{propertyName})";
            }
            if (queryType.Equals("LTE"))
            {
                return $"it => it.{propertyName} <= parm.{propertyName})";
            }
            if (queryType.Equals("NE"))
            {
                return $"it => it.{propertyName} != parm.{propertyName})";
            }
            if (queryType.Equals("LIKE"))
            {
                return $"it => it.{propertyName}.Contains(parm.{propertyName}))";
            }
            return $"it => it.{propertyName} == parm.{propertyName})";
        }
        #endregion
    }
}
