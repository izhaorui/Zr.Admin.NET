using Infrastructure;
using JinianNet.JNTemplate;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ZR.CodeGenerator.Model;
using ZR.Model.System.Generate;

namespace ZR.CodeGenerator
{
    /// <summary>
    /// 代码生成器。
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
        /// <param name="dbTableInfo"></param>
        /// <param name="dto"></param>
        public static void Generate(GenTable dbTableInfo, GenerateDto dto)
        {
            _option.BaseNamespace = dbTableInfo.BaseNameSpace;
            _option.DtosNamespace = _option.BaseNamespace + "Model";
            _option.ModelsNamespace = _option.BaseNamespace + "Model";
            _option.RepositoriesNamespace = _option.BaseNamespace + "Repository";
            _option.IRepositoriesNamespace = _option.BaseNamespace + "Repository";
            _option.IServicsNamespace = _option.BaseNamespace + "Service";
            _option.ServicesNamespace = _option.BaseNamespace + "Service";
            _option.ApiControllerNamespace = _option.BaseNamespace + "Admin.WebApi";

            GenerateSingle(dbTableInfo?.Columns, dbTableInfo, dto);
        }

        /// <summary>
        /// 单表生成代码
        /// </summary>
        /// <param name="listField">表字段集合</param>
        /// <param name="tableInfo">表信息</param>
        /// <param name="dto"></param>
        public static void GenerateSingle(List<GenTableColumn> listField, GenTable tableInfo, GenerateDto dto)
        {
            string PKName = "id";
            string PKType = "int";
            ReplaceDto replaceDto = new();
            replaceDto.ModelTypeName = tableInfo.ClassName;//表名对应C# 实体类名
            replaceDto.TableName = tableInfo.TableName;//表名
            replaceDto.Permission = $"{tableInfo.ModuleName}:{tableInfo.ClassName.ToLower()}";//权限
            replaceDto.ViewsFileName = FirstLowerCase(replaceDto.ModelTypeName);
            replaceDto.Author = tableInfo.FunctionAuthor;

            StringBuilder sb1 = new StringBuilder();
            StringBuilder sb2 = new StringBuilder();

            //循环表字段信息
            foreach (GenTableColumn dbFieldInfo in listField)
            {
                string columnName = dbFieldInfo.ColumnName;

                if (dbFieldInfo.IsInsert || dbFieldInfo.IsEdit)
                {
                    replaceDto.VueViewFormResetHtml += $"        {columnName}: undefined,\r\n";
                }
                if (dbFieldInfo.IsPk || dbFieldInfo.IsIncrement)
                {
                    PKName = dbFieldInfo.CsharpField;
                    PKType = dbFieldInfo.CsharpType;
                }
                //编辑字段
                if (dbFieldInfo.IsEdit)
                {
                    replaceDto.UpdateColumn += $"                {dbFieldInfo.CsharpField} = model.{dbFieldInfo.CsharpField}, \n";
                }
                //新增字段
                if (dbFieldInfo.IsInsert)
                {
                    replaceDto.InsertColumn += $"                it.{dbFieldInfo.CsharpField}, \n";
                }
                if ((dbFieldInfo.HtmlType == GenConstants.HTML_SELECT || dbFieldInfo.HtmlType == GenConstants.HTML_RADIO) && !string.IsNullOrEmpty(dbFieldInfo.DictType))
                {
                    sb1.AppendLine($"      // {dbFieldInfo.ColumnComment}选项列表");
                    sb1.AppendLine($"      {FirstLowerCase(dbFieldInfo.CsharpField)}Options: [],");

                    sb2.AppendLine($"    this.getDicts(\"{dbFieldInfo.DictType}\").then((response) => {{");
                    sb2.AppendLine($"      this.{FirstLowerCase(dbFieldInfo.CsharpField)}Options = response.data;");
                    sb2.AppendLine("    })");
                }
                //引用组件 已弃用、改用前端全局注册
                //if (dbFieldInfo.HtmlType == GenConstants.HTML_EDITOR)
                //{
                //    replaceDto.VueComponent += "Editor,";
                //    replaceDto.VueComponentImport += "import Editor from '@/components/Editor';\n";
                //}

                CodeGenerateTemplate.GetQueryDtoProperty(dbFieldInfo, replaceDto);
                replaceDto.ModelProperty += CodeGenerateTemplate.GetModelTemplate(dbFieldInfo);
                replaceDto.VueViewFormHtml += CodeGenerateTemplate.TplVueFormContent(dbFieldInfo);
                CodeGenerateTemplate.TplVueJsMethod(dbFieldInfo, replaceDto);
                replaceDto.VueViewListHtml += CodeGenerateTemplate.TplTableColumn(dbFieldInfo);
                replaceDto.VueViewEditFormRuleContent += CodeGenerateTemplate.TplFormRules(dbFieldInfo);
                replaceDto.InputDtoProperty += CodeGenerateTemplate.GetDtoProperty(dbFieldInfo);
                replaceDto.VueQueryFormHtml += CodeGenerateTemplate.TplQueryFormHtml(dbFieldInfo);
            }
            replaceDto.VueDataContent = sb1.ToString();
            replaceDto.MountedMethod = sb2.ToString();
            replaceDto.VueJsMethod += replaceDto.VueBeforeUpload;
            replaceDto.VueDataContent += replaceDto.VueUploadUrl;

            replaceDto.PKName = PKName;
            replaceDto.PKType = PKType;

            if (dto.GenCodeFiles.Contains(1))
            {
                GenerateModels(replaceDto, dto);
            }
            if (dto.GenCodeFiles.Contains(2))
            {
                GenerateInputDto(replaceDto, dto);
            }
            if (dto.GenCodeFiles.Contains(3))
            {
                GenerateRepository(replaceDto, dto);
            }
            if (dto.GenCodeFiles.Contains(4))
            {
                GenerateIService(replaceDto, dto);
                GenerateService(replaceDto, dto);
            }
            if (dto.GenCodeFiles.Contains(5))
            {
                GenerateControllers(replaceDto, dto);
            }
            if (dto.GenCodeFiles.Contains(6))
            {
                GenerateVueViews(replaceDto, dto);
            }
            if (dto.GenCodeFiles.Contains(7))
            {
                GenerateVueJs(replaceDto, dto);
            }
            if (dto.GenCodeFiles.Contains(8))
            {
                GenerateSql(replaceDto, dto);
            }
            if (dto.IsPreview == 1)
            {
                return;
            }
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
            // ../ZR.Model/Models/User.cs
            var fullPath = Path.Combine(generateDto.GenCodePath, _option.ModelsNamespace, "Models", replaceDto.ModelTypeName + ".cs");

            if (File.Exists(fullPath) && !generateDto.Coverd)
                return;

            var content = FileHelper.ReadTemplate("ModelTemplate.txt")
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{FunctionName}", generateDto.GenTable.FunctionName)
                .Replace("{KeyTypeName}", replaceDto.PKName)
                .Replace("{PropertyName}", replaceDto.ModelProperty)
                .Replace("{TableName}", replaceDto.TableName)
                .Replace("{Author}", replaceDto.Author)
                .Replace("{DateTime}", replaceDto.AddTime);

            generateDto.GenCodes.Add(new GenCode(1, "实体类", fullPath, content));
        }

        /// <summary>
        /// 生成表单提交输入参数Dto
        /// </summary>
        /// <param name="generateDto"></param>
        /// <param name="replaceDto">替换实体</param>
        private static void GenerateInputDto(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var fullPath = Path.Combine(generateDto.GenCodePath, _option.ModelsNamespace, "Dto", $"{replaceDto.ModelTypeName}Dto.cs");

            if (File.Exists(fullPath) && !generateDto.Coverd)
                return;

            var tpl = FileHelper.ReadJtTemplate("InputDtoTemplate.txt");
            tpl.Set("DtosNamespace", _option.DtosNamespace);
            tpl.Set("ModelsNamespace", _option.ModelsNamespace);
            tpl.Set("FunctionName", generateDto.GenTable.FunctionName);
            tpl.Set("PropertyName", replaceDto.InputDtoProperty);
            tpl.Set("QueryProperty", replaceDto.QueryProperty);
            tpl.Set("ModelTypeName", replaceDto.ModelTypeName);
            tpl.Set("Author", replaceDto.Author);
            tpl.Set("DateTime", replaceDto.AddTime);

            var result = tpl.Render();
            generateDto.GenCodes.Add(new GenCode(2, "数据传输实体类", fullPath, result));
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

            if (File.Exists(fullPath) && !generateDto.Coverd)
                return;

            var content = FileHelper.ReadTemplate("RepositoryTemplate.txt")
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{RepositoriesNamespace}", _option.RepositoriesNamespace)
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{FunctionName}", generateDto.GenTable.FunctionName)
                .Replace("{TableName}", replaceDto.TableName)
                .Replace("{Author}", replaceDto.Author)
                .Replace("{DateTime}", replaceDto.AddTime);

            generateDto.GenCodes.Add(new GenCode(3, "仓储层", fullPath, content));
        }

        #endregion

        #region 生成Service
        /// <summary>
        /// 生成IService文件
        /// </summary>
        /// <param name="generateDto"></param>
        /// <param name="replaceDto">替换实体</param>
        private static void GenerateIService(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var fullPath = Path.Combine(generateDto.GenCodePath, _option.IServicsNamespace, "Business", "IBusService", $"I{replaceDto.ModelTypeName}Service.cs");

            if (File.Exists(fullPath) && !generateDto.Coverd)
                return;
            var content = FileHelper.ReadTemplate("IServiceTemplate.txt")
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{FunctionName}", generateDto.GenTable.FunctionName)
                .Replace("{DtosNamespace}", _option.DtosNamespace)
                .Replace("{IServicsNamespace}", _option.IServicsNamespace)
                .Replace("{RepositoriesNamespace}", _option.RepositoriesNamespace)
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{Author}", replaceDto.Author)
                .Replace("{DateTime}", replaceDto.AddTime);

            generateDto.GenCodes.Add(new GenCode(4, "接口层", fullPath, content));
        }

        /// <summary>
        /// 生成Service文件
        /// </summary>
        private static void GenerateService(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var fullPath = Path.Combine(generateDto.GenCodePath, _option.ServicesNamespace, "Business", $"{replaceDto.ModelTypeName}Service.cs");

            if (File.Exists(fullPath) && !generateDto.Coverd)
                return;

            var content = FileHelper.ReadTemplate("ServiceTemplate.txt")
                .Replace("{IRepositoriesNamespace}", _option.IRepositoriesNamespace)
                .Replace("{DtosNamespace}", _option.DtosNamespace)
                .Replace("{IServicsNamespace}", _option.IServicsNamespace)
                .Replace("{FunctionName}", generateDto.GenTable.FunctionName)
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{ServicesNamespace}", _option.ServicesNamespace)
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{Author}", replaceDto.Author)
                .Replace("{DateTime}", replaceDto.AddTime);

            generateDto.GenCodes.Add(new GenCode(4, "服务层", fullPath, content));
        }

        #endregion

        #region 生成Controller
        /// <summary>
        /// 生成控制器ApiControllers文件
        /// </summary>
        private static void GenerateControllers(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var fullPath = Path.Combine(generateDto.GenCodePath, _option.ApiControllerNamespace, "Controllers", generateDto.GenTable.ModuleName, $"{replaceDto.ModelTypeName}Controller.cs");
            if (File.Exists(fullPath) && !generateDto.Coverd)
                return;

            var content = FileHelper.ReadTemplate("ControllersTemplate.txt")
                .Replace("{ApiControllerNamespace}", _option.ApiControllerNamespace)
                .Replace("{ServicesNamespace}", _option.ServicesNamespace)
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{FunctionName}", generateDto.GenTable.FunctionName)
                .Replace("{ModelName}", replaceDto.ModelTypeName)
                .Replace("{Permission}", replaceDto.Permission)
                .Replace("{PrimaryKey}", replaceDto.PKName)
                .Replace("{ModuleName}", generateDto.GenTable.ModuleName)
                .Replace("{PKCsharpType}", replaceDto.PKType)
                .Replace("{Author}", replaceDto.Author)
                .Replace("{DateTime}", replaceDto.AddTime);

            if (replaceDto.UpdateColumn != null)
            {
                content = content.Replace("{UpdateColumn}", replaceDto.UpdateColumn.TrimEnd('\n'));
            }
            if (replaceDto.InsertColumn != null)
            {
                content = content.Replace("{InsertColumn}", replaceDto.InsertColumn.TrimEnd('\n'));
            }
            if (replaceDto.QueryCondition != null)
            {
                content = content.Replace("{QueryCondition}", replaceDto.QueryCondition);
            }
            generateDto.GenCodes.Add(new GenCode(5, "控制器", fullPath, content));
        }
        #endregion

        #region 生成Vue页面 & api
        /// <summary>
        /// 6、生成Vue页面
        private static void GenerateVueViews(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var fullPath = Path.Combine(generateDto.GenCodePath, "ZR.Vue", "src", "views", generateDto.GenTable.ModuleName, replaceDto.ViewsFileName, "index.vue");

            if (File.Exists(fullPath) && !generateDto.Coverd)
                return;

            var content = FileHelper.ReadTemplate("VueTemplate.txt")
                .Replace("{fileClassName}", replaceDto.ViewsFileName)
                .Replace("{VueViewListContent}", replaceDto.VueViewListHtml)//查询 table列
                .Replace("{VueViewFormContent}", replaceDto.VueViewFormHtml)//添加、修改表单
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{Permission}", replaceDto.Permission)
                .Replace("{VueViewFormResetHtml}", replaceDto.VueViewFormResetHtml)
                .Replace("{vueJsMethod}", replaceDto.VueJsMethod)
                .Replace("{vueQueryFormHtml}", replaceDto.VueQueryFormHtml)
                .Replace("{VueDataContent}", replaceDto.VueDataContent)
                .Replace("{PrimaryKey}", FirstLowerCase(replaceDto.PKName))
                .Replace("{MountedMethod}", replaceDto.MountedMethod)
                .Replace("{VueComponent}", replaceDto.VueComponent.TrimEnd(','))
                .Replace("{VueComponentImport}", replaceDto.VueComponentImport)
                .Replace("{VueViewEditFormRuleContent}", replaceDto.VueViewEditFormRuleContent);//添加、修改表单验证规则

            generateDto.GenCodes.Add(new GenCode(6, "index.vue", fullPath, content));

            //var tpl = FileHelper.ReadJtTemplate("VueTemplate.txt");

            //tpl.Set("fileClassName", replaceDto.ViewsFileName);
            //tpl.Set("VueViewListContent", replaceDto.VueViewListHtml);//查询 table列
            //tpl.Set("VueViewFormContent", replaceDto.VueViewFormHtml);//添加、修改表单
            //tpl.Set("ModelTypeName", replaceDto.ModelTypeName);
            //tpl.Set("Permission", replaceDto.Permission);
            //tpl.Set("VueViewFormResetHtml", replaceDto.VueViewFormResetHtml);
            ////tpl.Set("vueJsMethod}", replaceDto.VueJsMethod);
            ////tpl.Set("vueQueryFormHtml", replaceDto.VueQueryFormHtml);
            ////tpl.Set("VueDataContent", replaceDto.VueDataContent);
            ////tpl.Set("PrimaryKey", FirstLowerCase(replaceDto.PKName));
            ////tpl.Set("MountedMethod", replaceDto.MountedMethod);
            ////tpl.Set("VueComponent", replaceDto.VueComponent.TrimEnd(','));
            ////tpl.Set("VueComponentImport", replaceDto.VueComponentImport);
            ////tpl.Set("VueViewEditFormRuleContent", replaceDto.VueViewEditFormRuleContent);//添加、修改表单验证规则
            //tpl.Set("uploadImage", generateDto.GenTable.Columns.Any(x => x.HtmlType == GenConstants.HTML_IMAGE_UPLOAD));

            //var result = tpl.Render();
            //generateDto.GenCodes.Add(new GenCode(6, "index.vue", fullPath, result));
        }
        /// <summary>
        /// 7、生成vue页面api
        /// </summary>
        /// <param name="replaceDto"></param>
        /// <param name="generateDto"></param>
        /// <returns></returns>
        public static void GenerateVueJs(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            string fullPath = Path.Combine(generateDto.GenCodePath, "ZR.Vue", "src", "api", replaceDto.ViewsFileName + ".js");

            if (File.Exists(fullPath) && !generateDto.Coverd)
                return;

            var content = FileHelper.ReadTemplate("VueJsTemplate.txt")
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{FunctionName}", generateDto.GenTable.FunctionName)
                .Replace("{ModuleName}", generateDto.GenTable.ModuleName);

            generateDto.GenCodes.Add(new GenCode(7, "api.js", fullPath, content));
        }

        #endregion

        #region 8、生成SQL

        public static void GenerateSql(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            string fullPath = Path.Combine(generateDto.GenCodePath, replaceDto.ViewsFileName + ".sql");

            if (File.Exists(fullPath) && !generateDto.Coverd)
                return;
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
            var content = FileHelper.ReadTemplate($"{tempName}.txt")
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{Permission}", replaceDto.Permission)
                .Replace("{ModuleName}", generateDto.GenTable.ModuleName)
                .Replace("{ViewsFileName}", replaceDto.ViewsFileName)
                .Replace("{ParentId}", generateDto.GenTable.ParentMenuId ?? "0")
                .Replace("{FunctionName}", generateDto.GenTable.FunctionName);

            generateDto.GenCodes.Add(new GenCode(8, "sql", fullPath, content));
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
                        tableName = tableName.Replace(searcList[i], "");
                    }
                }
            }
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
        /// 判断是否给属性添加?
        /// </summary>
        /// <param name="dbFieldInfo"></param>
        /// <returns></returns>
        public static string GetModelRequired(GenTableColumn dbFieldInfo)
        {
            return (!dbFieldInfo.IsRequired && (IsNumber(dbFieldInfo.ColumnType) || dbFieldInfo.CsharpType == "DateTime")) ? "?" : "";
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

                genTableColumns.Add(genTableColumn);
            }
            return genTableColumns;
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
                "decimal" or "numer(8,2)" => "decimal",
                "bit" => "bool",
                "date" or "datetime" or "datetime2" or "smalldatetime" => "DateTime",
                "money" or "smallmoney" => "double",
                _ => "string",
            };
            return sTempDatatype;
        }

        public static bool IsNumber(string tableDataType)
        {
            string[] arr = new string[] { "int", "long" };
            return arr.Any(f => f.Contains(GetCSharpDatatype(tableDataType)));
        }
    }
}
