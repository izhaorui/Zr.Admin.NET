using SqlSugar;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZR.CodeGenerator.CodeGenerator;
using ZR.CodeGenerator.Model;
using ZR.CodeGenerator.Service;

namespace ZR.CodeGenerator
{
    /// <summary>
    /// 代码生成器。
    /// <remarks>
    /// 根据指定的实体域名空间生成Repositories和Services层的基础代码文件。
    /// </remarks>
    /// </summary>
    public class CodeGeneratorTool
    {
        /// <summary>
        /// 代码生成器配置
        /// </summary>
        private static CodeGenerateOption _option = new CodeGenerateOption();
        /// <summary>
        /// InputDto输入实体是不包含字段
        /// </summary>
        private static string[] inputDtoNoField = new string[] { "DeleteMark", "CreateTime", "updateTime", "addtime" };
        private static string[] imageFiled = new string[] { "icon", "img", "image", "url", "pic" };
        private static string[] selectFiled = new string[] { "status", "type", "state" };
        private static string[] radioFiled = new string[] { "status", "state", "is" };

        /// <summary>
        /// 代码生成器入口方法
        /// </summary>
        /// <param name="dbTableInfo"></param>
        /// <param name="dto"></param>
        public static void Generate(DbTableInfo dbTableInfo, GenerateDto dto)
        {
            //_option.BaseNamespace = baseNamespace;
            _option.DtosNamespace = "ZR.Model";
            _option.ModelsNamespace = "ZR.Model";
            _option.RepositoriesNamespace = "ZR.Repository";
            _option.IRepositoriesNamespace = "ZR.Repository";
            _option.IServicsNamespace = "ZR.Service";
            _option.ServicesNamespace = "ZR.Service";
            _option.ApiControllerNamespace = "ZR.Admin.WebApi";
            _option.ReplaceTableNameStr = dto.replaceTableNameStr;
            //_option.TableList = listTable;

            CodeGeneraterService codeGeneraterService = new CodeGeneraterService();

            List<DbColumnInfo> listField = codeGeneraterService.GetColumnInfo(dto.dbName, dbTableInfo.Name);
            GenerateSingle(listField, dbTableInfo, dto);

            //GenerateDtoProfile(_option.ModelsNamespace, profileContent, ifExsitedCovered);
        }

        /// <summary>
        /// 单表生成代码
        /// </summary>
        /// <param name="listField">表字段集合</param>
        /// <param name="tableInfo">表信息</param>
        /// <param name="ifExsitedCovered">如果目标文件存在，是否覆盖。默认为false</param>
        public static void GenerateSingle(List<DbColumnInfo> listField, DbTableInfo tableInfo, GenerateDto dto)
        {
            bool ifExsitedCovered = dto.coverd;
            var modelTypeName = GetModelClassName(tableInfo.Name);//表名对应C# 实体类名
            var modelTypeDesc = tableInfo.Description;//表描述
            var primaryKey = "id";//主键

            string keyTypeName = "int";//主键数据类型
            string modelContent = "";//数据库模型字段
            string InputDtoContent = "";//输入模型
            string outputDtoContent = "";//输出模型
            string updateColumn = "";//修改数据映射字段
            string vueViewListContent = string.Empty;//Vue列表输出内容
            string vueViewFormContent = string.Empty;//Vue表单输出内容 
            string vueViewEditFromContent = string.Empty;//Vue变量输出内容
            string vueViewEditFromBindContent = string.Empty;//Vue显示初始化输出内容
            string vueViewSaveBindContent = string.Empty;//Vue保存时输出内容
            string vueViewEditFromRuleContent = string.Empty;//Vue数据校验

            foreach (DbColumnInfo dbFieldInfo in listField)
            {
                string columnName = FirstLowerCase(dbFieldInfo.DbColumnName);

                if (dbFieldInfo.DataType == "bool" || dbFieldInfo.DataType == "tinyint")
                {
                    vueViewEditFromContent += $"        {columnName}: 'true',\n";
                    //vueViewEditFromBindContent += $"        this.form.{columnName} = res.data.{0}+''\n";
                }
                else
                {
                    vueViewEditFromContent += $"        {columnName}: undefined,\n";
                    //vueViewEditFromBindContent += $"        {columnName}: row.{columnName},\n";
                }
                //vueViewSaveBindContent += string.Format("        '{0}':this.editFrom.{0},\n", columnName);
                //主键
                if (dbFieldInfo.IsIdentity || dbFieldInfo.IsPrimarykey)
                {
                    primaryKey = columnName.Substring(0, 1).ToUpper() + columnName[1..];
                    keyTypeName = dbFieldInfo.DataType;
                }
                else
                {
                    var tempColumnName = columnName.Substring(0, 1).ToUpper() + columnName[1..];
                    updateColumn += $"              {tempColumnName} = parm.{tempColumnName},\n";
                }

                dbFieldInfo.DbColumnName = columnName;
                modelContent += GetModelTemplate(dbFieldInfo);
                vueViewFormContent += GetVueViewFormContent(dbFieldInfo);
                vueViewListContent += GetTableColumn(dbFieldInfo);
                vueViewEditFromRuleContent += GetFormRules(dbFieldInfo);
                InputDtoContent += GetDtoContent(dbFieldInfo);
            }
            if (dto.genFiles.Contains(1))
            {
                GenerateModels(_option.ModelsNamespace, modelTypeName, tableInfo.Name, modelContent, modelTypeDesc, keyTypeName, ifExsitedCovered);
            }
            if (dto.genFiles.Contains(2))
            {
                GenerateInputDto(_option.ModelsNamespace, modelTypeName, modelTypeDesc, InputDtoContent, keyTypeName, ifExsitedCovered);
            }
            if (dto.genFiles.Contains(3))
            {
                GenerateRepository(modelTypeName, modelTypeDesc, tableInfo.Name, keyTypeName, ifExsitedCovered);
            }
            if (dto.genFiles.Contains(4))
            {
                GenerateIService(_option.ModelsNamespace, modelTypeName, modelTypeDesc, keyTypeName, ifExsitedCovered);
                GenerateService(_option.ModelsNamespace, modelTypeName, modelTypeDesc, keyTypeName, ifExsitedCovered);
            }
            if (dto.genFiles.Contains(5))
            {
                GenerateControllers(modelTypeName, primaryKey, modelTypeDesc, keyTypeName, updateColumn, ifExsitedCovered);
            }
            if (dto.genFiles.Contains(6))
            {
                GenerateVueViews(modelTypeName, primaryKey, modelTypeDesc, vueViewListContent, vueViewFormContent, vueViewEditFromContent, vueViewEditFromBindContent, vueViewSaveBindContent, vueViewEditFromRuleContent, ifExsitedCovered);
            }
            //GenerateIRepository(modelTypeName, modelTypeDesc, keyTypeName, ifExsitedCovered);
            //GenerateOutputDto(modelTypeName, modelTypeDesc, outputDtocontent, ifExsitedCovered);
        }

        #region Template

        //rules
        private static string GetFormRules(DbColumnInfo dbFieldInfo)
        {
            string vueViewEditFromRuleContent = "";
            //Rule 规则验证
            if (!dbFieldInfo.IsNullable && !dbFieldInfo.IsIdentity)
            {
                vueViewEditFromRuleContent += $"        {dbFieldInfo.DbColumnName}: [\n";
                vueViewEditFromRuleContent += $"        {{ required: true, message:\"请输入{dbFieldInfo.ColumnDescription}\", trigger: \"blur\"}},\n";
                //vueViewEditFromRuleContent += "        { min: 2, max: 50, message: \"长度在 2 到 50 个字符\", trigger:\"blur\" }\n";
                vueViewEditFromRuleContent += "        ],\n";
            }

            return vueViewEditFromRuleContent;
        }

        //model 属性
        private static string GetModelTemplate(DbColumnInfo dbFieldInfo)
        {
            string columnName = dbFieldInfo.DbColumnName.Substring(0, 1).ToUpper() + dbFieldInfo.DbColumnName[1..];
            var modelcontent = "";
            modelcontent += "        /// <summary>\n";
            modelcontent += $"        /// 描述 :{dbFieldInfo.ColumnDescription}\n";
            modelcontent += $"        /// 空值 :{dbFieldInfo.IsNullable}\n";
            modelcontent += $"        /// 默认 :{dbFieldInfo.DefaultValue}\n";
            modelcontent += "        /// </summary>\n";
            if (dbFieldInfo.IsIdentity || dbFieldInfo.IsPrimarykey)
            {
                modelcontent += $"        [SqlSugar.SugarColumn(IsPrimaryKey = {dbFieldInfo.IsPrimarykey.ToString().ToLower()}, IsIdentity = {dbFieldInfo.IsIdentity.ToString().ToLower()})]\n";
            }
            modelcontent += $"        public {TableMappingHelper.GetPropertyDatatype(dbFieldInfo.DataType)} {columnName} {{ get; set; }}\n\r";
            return modelcontent;
        }
        //DTO model
        private static string GetDtoContent(DbColumnInfo dbFieldInfo)
        {
            string columnName = dbFieldInfo.DbColumnName.Substring(0, 1).ToUpper() + dbFieldInfo.DbColumnName[1..];
            string InputDtoContent = "";
            InputDtoContent += $"        public {TableMappingHelper.GetPropertyDatatype(dbFieldInfo.DataType)} {columnName} {{ get; set; }}\n\r";

            return InputDtoContent;
        }

        //form-item
        private static string GetVueViewFormContent(DbColumnInfo dbFieldInfo)
        {
            string columnName = FirstLowerCase(dbFieldInfo.DbColumnName);
            string labelName = GetLabelName(dbFieldInfo.ColumnDescription, columnName);
            string vueViewFromContent = "";
            string labelDisabled = dbFieldInfo.IsIdentity ? ":disabled=\"true\"" : "";
            if (dbFieldInfo.DataType == "datetime")
            {
                vueViewFromContent += "";
            }
            else if (((IList)imageFiled).Contains(columnName))
            {
                //TODO 图片
            }
            else if (radioFiled.Any(f => columnName.Contains(f)) && (dbFieldInfo.DataType == "bool" || dbFieldInfo.DataType == "tinyint" || dbFieldInfo.DataType == "int"))
            {
                vueViewFromContent += $"     <el-col :span=\"12\">\n";
                vueViewFromContent += $"       <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">";
                vueViewFromContent += $"         <el-radio-group v-model=\"form.{columnName}\">\n";
                vueViewFromContent += "           <el-radio v-for=\"dict in statusOptions\" :key=\"dict.dictValue\" :label=\"dict.dictValue\">{{dict.dictLabel}}</el-radio>\n";
                vueViewFromContent += "          </el-radio-group>\n";
                vueViewFromContent += "        </el-form-item>\n";
                vueViewFromContent += "      </el-col>\n";
            }
            else
            {
                vueViewFromContent += $"     <el-col :span=\"12\">\n";
                vueViewFromContent += $"        <el-form-item label=\"{ GetLabelName(dbFieldInfo.ColumnDescription, columnName)}\" :label-width=\"labelWidth\" prop=\"{FirstLowerCase(columnName)}\">\n";
                vueViewFromContent += $"           <el-input v-model=\"form.{FirstLowerCase(columnName)}\" placeholder=\"请输入{GetLabelName(dbFieldInfo.ColumnDescription, columnName)}\" {labelDisabled}/>\n";
                vueViewFromContent += "         </el-form-item>\n";
                vueViewFromContent += "      </el-col>\n";
            }

            return vueViewFromContent;
        }

        //table-column
        private static string GetTableColumn(DbColumnInfo dbFieldInfo)
        {
            string columnName = FirstLowerCase(dbFieldInfo.DbColumnName);
            string label = GetLabelName(dbFieldInfo.ColumnDescription, columnName);
            string vueViewListContent = "";
            string showToolTip = dbFieldInfo.DataType.Contains("varchar") ? ":show-overflow-tooltip=\"true\"" : "";

            if (imageFiled.Any(f => columnName.ToLower().Contains(f)))
            {
                vueViewListContent += $"      <el-table-column prop=\"{ columnName}\" label=\"图片\">\n";
                vueViewListContent += "         <template slot-scope=\"scope\">\n";
                vueViewListContent += $"            <el-image class=\"table-td-thumb\" :src=\"scope.row.{columnName}\" :preview-src-list=\"[scope.row.{columnName}]\"></el-image>\n";
                vueViewListContent += "         </template>\n";
                vueViewListContent += "       </el-table-column>\n";
            }
            else if (dbFieldInfo.DataType == "bool" || dbFieldInfo.DataType == "tinyint")
            {
                vueViewListContent += $"        <el-table-column prop=\"{columnName}\" label=\"{label}\" width=\"120\" >\n";
                vueViewListContent += "          <template slot-scope=\"scope\">\n";
                vueViewListContent += $"            <el-tag :type=\"scope.row.{columnName} === true ? 'success' : 'info'\"  disable-transitions >";
                vueViewListContent += $"                {{scope.row.{columnName}===true?'启用':'禁用'}} </el-tag>\n";
                vueViewListContent += "          </template>\n";
                vueViewListContent += "        </el-table-column>\n";
            }
            else
            {
                //table-column
                vueViewListContent += $"      <el-table-column prop=\"{FirstLowerCase(columnName)}\" label=\"{GetLabelName(dbFieldInfo.ColumnDescription, columnName)}\"  align=\"center\" width=\"100\" {showToolTip} />\n";
            }
            return vueViewListContent;
        }
        #endregion

        #region 生成Model

        /// <summary>
        /// 生成Models文件
        /// </summary>
        /// <param name="modelsNamespace">命名空间</param>
        /// <param name="modelTypeName">类名</param>
        /// <param name="tableName">表名称</param>
        /// <param name="modelTypeDesc">表描述</param>
        /// <param name="modelContent">数据库表实体内容</param>
        /// <param name="keyTypeName">主键数据类型</param>
        /// <param name="ifExsitedCovered">如果目标文件存在，是否覆盖。默认为false</param>
        private static Tuple<string, string> GenerateModels(string modelsNamespace, string modelTypeName, string tableName, string modelContent, string modelTypeDesc, string keyTypeName, bool ifExsitedCovered = false)
        {
            var parentPath = "..";
            //../ZR.Model
            var servicesPath = parentPath + "\\" + modelsNamespace + "\\Models\\";
            if (!Directory.Exists(servicesPath))
            {
                Directory.CreateDirectory(servicesPath);
            }
            // ../ZR.Model/Models/User.cs
            var fullPath = servicesPath + modelTypeName + ".cs";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return Tuple.Create(fullPath, "");
            var content = ReadTemplate("ModelTemplate.txt");
            content = content
                .Replace("{ModelsNamespace}", modelsNamespace)
                .Replace("{ModelTypeName}", modelTypeName)
                .Replace("{TableNameDesc}", modelTypeDesc)
                .Replace("{KeyTypeName}", keyTypeName)
                .Replace("{PropertyName}", modelContent)
                .Replace("{TableName}", tableName);
            WriteAndSave(fullPath, content);
            return Tuple.Create(fullPath, content);
        }


        /// <summary>
        /// 生成InputDto文件
        /// </summary>
        /// <param name="modelsNamespace"></param>
        /// <param name="modelTypeName"></param>
        /// <param name="modelTypeDesc"></param>
        /// <param name="modelContent"></param>
        /// <param name="keyTypeName"></param>
        /// <param name="ifExsitedCovered">如果目标文件存在，是否覆盖。默认为false</param>
        private static Tuple<string, string> GenerateInputDto(string modelsNamespace, string modelTypeName, string modelTypeDesc, string modelContent, string keyTypeName, bool ifExsitedCovered = false)
        {
            var parentPath = "..";
            var servicesPath = parentPath + "\\" + modelsNamespace + "\\Dto\\";
            if (!Directory.Exists(servicesPath))
            {
                Directory.CreateDirectory(servicesPath);
            }
            // ../ZR.Model/Dto/User.cs
            var fullPath = servicesPath + modelTypeName + "Dto.cs";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return Tuple.Create(fullPath, ""); ; 
            var content = ReadTemplate("InputDtoTemplate.txt");
            content = content
                .Replace("{DtosNamespace}", _option.DtosNamespace)
                .Replace("{ModelsNamespace}", modelsNamespace)
                .Replace("{TableNameDesc}", modelTypeDesc)
                .Replace("{KeyTypeName}", keyTypeName)
                .Replace("{PropertyName}", modelContent)
                .Replace("{ModelTypeName}", modelTypeName);
            WriteAndSave(fullPath, content);
            return Tuple.Create(fullPath, content);
        }
        #endregion

        #region 生成Repository

        /// <summary>
        /// 生成Repository层代码文件
        /// </summary>
        /// <param name="modelTypeName"></param>
        /// <param name="modelTypeDesc"></param>
        /// <param name="tableName">表名</param>
        /// <param name="keyTypeName"></param>
        /// <param name="ifExsitedCovered">如果目标文件存在，是否覆盖。默认为false</param>
        private static Tuple<string, string> GenerateRepository(string modelTypeName, string modelTypeDesc, string tableName, string keyTypeName, bool ifExsitedCovered = false)
        {
            var parentPath = "..";
            var repositoryPath = parentPath + "\\" + _option.RepositoriesNamespace + "\\Repositories\\";
            if (!Directory.Exists(repositoryPath))
            {
                Directory.CreateDirectory(repositoryPath);
            }
            var fullPath = repositoryPath + "\\" + modelTypeName + "Repository.cs";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return Tuple.Create(fullPath, "");
            var content = ReadTemplate("RepositoryTemplate.txt");
            content = content.Replace("{ModelsNamespace}", _option.ModelsNamespace)
                //.Replace("{IRepositoriesNamespace}", _option.IRepositoriesNamespace)
                .Replace("{RepositoriesNamespace}", _option.RepositoriesNamespace)
                .Replace("{ModelTypeName}", modelTypeName)
                .Replace("{TableNameDesc}", modelTypeDesc)
                .Replace("{TableName}", tableName)
                .Replace("{KeyTypeName}", keyTypeName);
            WriteAndSave(fullPath, content);
            return Tuple.Create(fullPath, content);
        }

        #endregion

        #region 生成Service
        /// <summary>
        /// 生成IService文件
        /// </summary>
        /// <param name="modelsNamespace"></param>
        /// <param name="modelTypeName"></param>
        /// <param name="modelTypeDesc"></param>
        /// <param name="keyTypeName"></param>
        /// <param name="ifExsitedCovered">如果目标文件存在，是否覆盖。默认为false</param>
        private static Tuple<string, string> GenerateIService(string modelsNamespace, string modelTypeName, string modelTypeDesc, string keyTypeName, bool ifExsitedCovered = false)
        {
            var parentPath = "..";
            var iServicesPath = parentPath + "\\" + _option.IServicsNamespace + "\\Business\\IBusService\\";
            if (!Directory.Exists(iServicesPath))
            {
                Directory.CreateDirectory(iServicesPath);
            }
            var fullPath = $"{iServicesPath}\\I{modelTypeName}Service.cs";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return Tuple.Create(fullPath, ""); 
            var content = ReadTemplate("IServiceTemplate.txt");
            content = content.Replace("{ModelsNamespace}", modelsNamespace)
                .Replace("{TableNameDesc}", modelTypeDesc)
                .Replace("{DtosNamespace}", _option.DtosNamespace)
                .Replace("{IServicsNamespace}", _option.IServicsNamespace)
                .Replace("{RepositoriesNamespace}", _option.RepositoriesNamespace)
                .Replace("{ModelTypeName}", modelTypeName)
                .Replace("{KeyTypeName}", keyTypeName);
            WriteAndSave(fullPath, content);
            return Tuple.Create(fullPath, content);
        }

        /// <summary>
        /// 生成Service文件
        /// </summary>
        /// <param name="modelsNamespace"></param>
        /// <param name="modelTypeName"></param>
        /// <param name="modelTypeDesc"></param>
        /// <param name="keyTypeName"></param>
        /// <param name="ifExsitedCovered">如果目标文件存在，是否覆盖。默认为false</param>
        private static Tuple<string, string> GenerateService(string modelsNamespace, string modelTypeName, string modelTypeDesc, string keyTypeName, bool ifExsitedCovered = false)
        {
            var parentPath = "..";
            var servicesPath = parentPath + "\\" + _option.ServicesNamespace + "\\Business\\";
            if (!Directory.Exists(servicesPath))
            {
                Directory.CreateDirectory(servicesPath);
            }
            var fullPath = servicesPath + modelTypeName + "Service.cs";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return Tuple.Create(fullPath, "");
            var content = ReadTemplate("ServiceTemplate.txt");
            content = content
                .Replace("{IRepositoriesNamespace}", _option.IRepositoriesNamespace)
                .Replace("{DtosNamespace}", _option.DtosNamespace)
                .Replace("{IServicsNamespace}", _option.IServicsNamespace)
                .Replace("{TableNameDesc}", modelTypeDesc)
                .Replace("{ModelsNamespace}", modelsNamespace)
                .Replace("{ServicesNamespace}", _option.ServicesNamespace)
                .Replace("{ModelTypeName}", modelTypeName)
                .Replace("{KeyTypeName}", keyTypeName);
            WriteAndSave(fullPath, content);
            return Tuple.Create(fullPath, content);
        }

        #endregion

        #region 生成Controller
        /// <summary>
        /// 生成控制器ApiControllers文件
        /// </summary>
        /// <param name="modelTypeName">实体类型名称</param>
        /// <param name="primaryKey">主键</param>
        /// <param name="modelTypeDesc">实体描述</param>
        /// <param name="keyTypeName"></param>
        /// <param name="ifExsitedCovered">如果目标文件存在，是否覆盖。默认为false</param>
        private static Tuple<string, string> GenerateControllers(string modelTypeName, string primaryKey, string modelTypeDesc, string keyTypeName, string updateColumn, bool ifExsitedCovered = false)
        {
            var parentPath = "..";
            var servicesPath = parentPath + "\\" + _option.ApiControllerNamespace + "\\Controllers\\business\\";
            if (!Directory.Exists(servicesPath))
            {
                Directory.CreateDirectory(servicesPath);
            }
            var fullPath = servicesPath + modelTypeName + "Controller.cs";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return Tuple.Create(fullPath, "");
            var content = ReadTemplate("ControllersTemplate.txt");
            content = content
                //.Replace("{DtosNamespace}", _option.DtosNamespace)
                .Replace("{ControllerName}", modelTypeName)
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{FileName}", modelTypeDesc)
                .Replace("{ServiceName}", modelTypeName + "Service")
                .Replace("{ModelName}", modelTypeName)
                .Replace("{Permission}", modelTypeName.ToLower())
                .Replace("{primaryKey}", primaryKey)
                .Replace("{updateColumn}", updateColumn)
                .Replace("{KeyTypeName}", keyTypeName);
            WriteAndSave(fullPath, content);
            return Tuple.Create(fullPath, content);
        }
        #endregion

        #region 生成Vue页面
        /// <summary>
        /// 生成Vue页面
        /// </summary>
        /// <param name="modelTypeName">类名</param>
        /// <param name="modelTypeDesc">表/类描述</param>
        /// <param name="vueViewListContent"></param>
        /// <param name="vueViewFromContent"></param>
        /// <param name="vueViewEditFromContent"></param>
        /// <param name="vueViewEditFromBindContent"></param>
        /// <param name="vueViewSaveBindContent"></param>
        /// <param name="vueViewEditFromRuleContent"></param>
        /// <param name="ifExsitedCovered">如果目标文件存在，是否覆盖。默认为false</param>
        private static Tuple<string, string> GenerateVueViews(string modelTypeName, string primaryKey, string modelTypeDesc, string vueViewListContent, string vueViewFromContent, string vueViewEditFromContent, string vueViewEditFromBindContent, string vueViewSaveBindContent, string vueViewEditFromRuleContent, bool ifExsitedCovered = false)
        {
            //var parentPath = "..\\CodeGenerate";//若要生成到项目中将路径改成 “..\\ZR.Vue\\src”
            var parentPath = "..\\ZR.Vue\\src";
            var servicesPath = parentPath + "\\views\\" + FirstLowerCase(modelTypeName);
            if (!Directory.Exists(servicesPath))
            {
                Directory.CreateDirectory(servicesPath);
            }
            var fullPath = servicesPath + "\\" + "index.vue";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return Tuple.Create(fullPath, ""); ;
            var content = ReadTemplate("VueTemplate.txt");
            content = content
                .Replace("{fileClassName}", FirstLowerCase(modelTypeName))
                .Replace("{VueViewListContent}", vueViewListContent)//查询 table列
                .Replace("{VueViewFormContent}", vueViewFromContent)//添加、修改表单
                .Replace("{ModelTypeName}", modelTypeName)
                .Replace("{Permission}", modelTypeName.ToLower())
                .Replace("{VueViewEditFormContent}", vueViewEditFromContent)//重置表单
                                                                            //.Replace("{VueViewEditFromBindContent}", vueViewEditFromBindContent)
                                                                            //.Replace("{VueViewSaveBindContent}", vueViewSaveBindContent)
                .Replace("{primaryKey}", FirstLowerCase(primaryKey))
                .Replace("{VueViewEditFormRuleContent}", vueViewEditFromRuleContent);//添加、修改表单验证规则
            WriteAndSave(fullPath, content);

            //api js
            servicesPath = parentPath + "\\api\\";
            Directory.CreateDirectory(servicesPath);
            fullPath = servicesPath + "\\" + FirstLowerCase(modelTypeName) + ".js";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return Tuple.Create(fullPath, "");
            content = ReadTemplate("VueJsTemplate.txt");
            content = content
                .Replace("{ModelTypeName}", modelTypeName)
                .Replace("{ModelTypeDesc}", modelTypeDesc);
            //.Replace("{fileClassName}", fileClassName)
            WriteAndSave(fullPath, content);
            return Tuple.Create(fullPath, content);
        }

        #endregion

        #region 帮助方法


        /// <summary>
        /// 如果有前缀替换将前缀替换成空，替换下划线"_"为空再将首字母大写
        /// </summary>
        /// <param name="modelTypeName"></param>
        /// <returns></returns>
        private static string GetModelClassName(string modelTypeName)
        {
            if (!string.IsNullOrEmpty(_option.ReplaceTableNameStr))
            {
                modelTypeName = modelTypeName.Replace(_option.ReplaceTableNameStr.ToString(), "");
            }
            modelTypeName = modelTypeName.Replace("_", "");
            modelTypeName = modelTypeName.Substring(0, 1).ToUpper() + modelTypeName[1..];
            return modelTypeName;
        }
        /// <summary>
        /// 首字母转小写，输出前端
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static string FirstLowerCase(string str)
        {
            return string.IsNullOrEmpty(str) ? str : str.Substring(0, 1).ToLower() + str[1..];
        }

        /// <summary>
        /// 获取前端标签名
        /// </summary>
        /// <param name="columnDescription"></param>
        /// <param name="columnName"></param>
        /// <returns></returns>
        private static string GetLabelName(string columnDescription, string columnName)
        {
            return string.IsNullOrEmpty(columnDescription) ? columnName : columnDescription;
        }
        /// <summary>
        /// 从代码模板中读取内容
        /// </summary>
        /// <param name="templateName">模板名称，应包括文件扩展名称。比如：template.txt</param>
        /// <returns></returns>
        private static string ReadTemplate(string templateName)
        {
            var path = AppDomain.CurrentDomain.BaseDirectory;
            string fullName = $"{path}\\Template\\{templateName}";
            string temp = fullName;
            string str = "";
            if (!File.Exists(temp))
            {
                return str;
            }
            StreamReader sr = null;
            try
            {
                sr = new StreamReader(temp);
                str = sr.ReadToEnd(); // 读取文件
            }
            catch { }
            sr?.Close();
            sr?.Dispose();
            return str;

        }

        /// <summary>
        /// 写文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="content"></param>
        private static void WriteAndSave(string fileName, string content)
        {
            try
            {
                //实例化一个文件流--->与写入文件相关联
                using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                //实例化一个StreamWriter-->与fs相关联
                using var sw = new StreamWriter(fs);
                //开始写入
                sw.Write(content);
                //清空缓冲区
                sw.Flush();
                //关闭流
                sw.Close();
                fs.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("写入文件出错了:" + ex.Message);
            }
        }

        #endregion
    }
}
