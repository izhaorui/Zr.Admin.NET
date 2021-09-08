using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ZR.CodeGenerator.CodeGenerator;
using ZR.CodeGenerator.Service;
using ZR.Model;

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
        private static string inputDtoNoField = "DeleteMark,CreatorTime,CreatorUserId,CompanyId,DeptId,LastModifyTime,LastModifyUserId,DeleteTime,DeleteUserId,";

        /// <summary>
        /// 代码生成器入口方法
        /// </summary>
        /// <param name="baseNamespace"></param>
        /// <param name="dbTableInfo"></param>
        /// <param name="replaceTableNameStr">要删除表名称的字符</param>
        /// <param name="ifExsitedCovered">是否替换现有文件，为true时替换</param>
        public static void Generate(string dbName, string baseNamespace, DbTableInfo dbTableInfo, string replaceTableNameStr, bool ifExsitedCovered = false)
        {
            _option.BaseNamespace = baseNamespace;
            _option.DtosNamespace = baseNamespace + "ZR.Model";
            _option.ModelsNamespace = baseNamespace + "ZR.Model";
            //_option.IRepositoriesNamespace = baseNamespace + ".IRepositorie";
            _option.RepositoriesNamespace = baseNamespace + "ZR.Repository";
            _option.IServicsNamespace = baseNamespace + "ZR.Service";
            _option.ServicesNamespace = baseNamespace + "ZR.Service";
            _option.ApiControllerNamespace = baseNamespace + "ZR.Admin.WebApi";
            _option.ReplaceTableNameStr = replaceTableNameStr;
            //_option.TableList = listTable;

            CodeGeneraterService codeGeneraterService = new CodeGeneraterService();
            string profileContent = string.Empty;
            //foreach (DbTableInfo dbTableInfo in listTable)
            //{
            List<DbColumnInfo> listField = codeGeneraterService.GetColumnInfo(dbName, dbTableInfo.Name);
            GenerateSingle(listField, dbTableInfo, ifExsitedCovered);
            //string tableName = dbTableInfo.TableName;
            //if (!string.IsNullOrEmpty(_option.ReplaceTableNameStr))
            //{
            //    string[] rel = _option.ReplaceTableNameStr.Split(';');
            //    for (int i = 0; i < rel.Length; i++)
            //    {
            //        if (!string.IsNullOrEmpty(rel[i].ToString()))
            //        {
            //            tableName = tableName.Replace(rel[i].ToString(), "");
            //        }
            //    }
            //}
            //tableName = tableName.Substring(0, 1).ToUpper() + tableName.Substring(1);
            //profileContent += string.Format("           CreateMap<{0}, {0}OutputDto>();\n", tableName);
            //profileContent += string.Format("           CreateMap<{0}InputDto, {0}>();\n", tableName);
            //}

            //GenerateDtoProfile(_option.ModelsNamespace, profileContent, ifExsitedCovered);
        }



        /// <summary>
        /// 单表生成代码
        /// </summary>
        /// <param name="listField">表字段集合</param>
        /// <param name="tableInfo">表信息</param>
        /// <param name="ifExsitedCovered">如果目标文件存在，是否覆盖。默认为false</param>
        public static void GenerateSingle(List<DbColumnInfo> listField, DbTableInfo tableInfo, bool ifExsitedCovered = false)
        {
            var modelsNamespace = _option.ModelsNamespace;
            var modelTypeName = GetModelName(tableInfo.Name); ;//表名
            var modelTypeDesc = tableInfo.Description;//表描述
            var primaryKey = "id";//主键

            string keyTypeName = "string";//主键数据类型
            string modelcontent = "";//数据库模型字段
            string InputDtocontent = "";//输入模型
            string outputDtocontent = "";//输出模型
            string vueViewListContent = string.Empty;//Vue列表输出内容
            string vueViewFromContent = string.Empty;//Vue表单输出内容 
            string vueViewEditFromContent = string.Empty;//Vue变量输出内容
            string vueViewEditFromBindContent = string.Empty;//Vue显示初始化输出内容
            string vueViewSaveBindContent = string.Empty;//Vue保存时输出内容
            string vueViewEditFromRuleContent = string.Empty;//Vue数据校验

            foreach (DbColumnInfo dbFieldInfo in listField)
            {
                string columnName = dbFieldInfo.DbColumnName.Substring(0, 1).ToUpper() + dbFieldInfo.DbColumnName.Substring(1);

                modelcontent += "        /// <summary>\n";
                modelcontent += ($"        /// 描述 :{dbFieldInfo.ColumnDescription}\n");
                modelcontent += ($"        /// 空值 :{dbFieldInfo.IsNullable}\n");
                modelcontent += ($"        /// 默认 :{dbFieldInfo.DefaultValue}\n");
                modelcontent += "        /// </summary>\n";
                if (dbFieldInfo.IsIdentity || dbFieldInfo.IsPrimarykey)
                {
                    primaryKey = columnName;
                    modelcontent += $"        [SqlSugar.SugarColumn(IsPrimaryKey = {dbFieldInfo.IsPrimarykey.ToString().ToLower()}, IsIdentity = {dbFieldInfo.IsIdentity.ToString().ToLower()})]\n";
                }
                modelcontent += $"        public {TableMappingHelper.GetPropertyDatatype(dbFieldInfo.DataType)} {columnName} {{ get; set; }}\n\r";

                //if (dbFieldInfo.DataType == "string")
                //{
                //    outputDtocontent += string.Format("        [MaxLength({0})]\n", dbFieldInfo.FieldMaxLength);
                //}
                //outputDtocontent += string.Format("        public {0} {1}", dbFieldInfo.DataType, columnName);
                //outputDtocontent += " { get; set; }\n\r";
                if (dbFieldInfo.DataType == "bool" || dbFieldInfo.DataType == "tinyint")
                {

                    vueViewListContent += string.Format("        <el-table-column prop=\"{0}\" label=\"{1}\" sortable=\"custom\" width=\"120\" >\n", columnName, dbFieldInfo.ColumnDescription);
                    vueViewListContent += "          <template slot-scope=\"scope\">\n";
                    vueViewListContent += string.Format("            <el-tag :type=\"scope.row.{0} === true ? 'success' : 'info'\"  disable-transitions >", columnName);
                    vueViewListContent += "{{ ";
                    vueViewListContent += string.Format("scope.row.{0}===true?'启用':'禁用' ", columnName);
                    vueViewListContent += "}}</el-tag>\n";
                    vueViewListContent += "          </template>\n";
                    vueViewListContent += "        </el-table-column>\n";

                    vueViewFromContent += string.Format("        <el-form-item label=\"{0}\" :label-width=\"labelWidth\" prop=\"{1}\">", dbFieldInfo.ColumnDescription, columnName);
                    vueViewFromContent += string.Format("          <el-radio-group v-model=\"editFrom.{0}\">\n", columnName);
                    vueViewFromContent += "           <el-radio label=\"true\">是</el-radio>\n";
                    vueViewFromContent += "           <el-radio label=\"false\">否</el-radio>\n";
                    vueViewFromContent += "          </el-radio-group>\n";
                    vueViewFromContent += "        </el-form-item>\n";

                    vueViewEditFromContent += string.Format("        {0}: 'true',\n", columnName);
                    vueViewEditFromBindContent += string.Format("        this.editFrom.{0} = res.data.{0}+''\n", columnName);
                }
                else
                {
                    //table-column
                    vueViewListContent += $"        <el-table-column prop=\"{FirstLowerCase(columnName)}\" label=\"{GetLabelName(dbFieldInfo.ColumnDescription, columnName)}\" />\n";

                    //form-item
                    vueViewFromContent += $"        <el-form-item label=\"{ GetLabelName(dbFieldInfo.ColumnDescription, columnName)}\" :label-width=\"labelWidth\" prop=\"{FirstLowerCase(columnName)}\">\n";
                    vueViewFromContent += $"          <el-input v-model=\"form.{FirstLowerCase(columnName)}\" placeholder=\"请输入{GetLabelName(dbFieldInfo.ColumnDescription, columnName)}\" clearable />\n";
                    vueViewFromContent += "        </el-form-item>\n";
                    vueViewEditFromContent += string.Format("        {0}: '',\n", columnName);
                    vueViewEditFromBindContent += string.Format("        this.editFrom.{0} = res.ResData.{0}\n", columnName);
                }
                //vueViewSaveBindContent += string.Format("        '{0}':this.editFrom.{0},\n", columnName);
                
                //Rule 规则验证
                //if (!dbFieldInfo.IsNullable)
                //{
                //    vueViewEditFromRuleContent += string.Format("        {0}: [\n", columnName);
                //    vueViewEditFromRuleContent += "        {";
                //    vueViewEditFromRuleContent += string.Format("required: true, message:\"请输入{0}\", trigger: \"blur\"", dbFieldInfo.ColumnDescription);
                //    vueViewEditFromRuleContent += "},\n          { min: 2, max: 50, message: \"长度在 2 到 50 个字符\", trigger:\"blur\" }\n";
                //    vueViewEditFromRuleContent += "        ],\n";
                //}


                //if (!inputDtoNoField.Contains(columnName) || columnName == "Id")
                //{
                InputDtocontent += "        /// <summary>\n";
                InputDtocontent += string.Format("        /// 设置或获取{0}\n", dbFieldInfo.ColumnDescription);
                InputDtocontent += "        /// </summary>\n";
                InputDtocontent += $"        public {TableMappingHelper.GetPropertyDatatype(dbFieldInfo.DataType)} {columnName} {{ get; set; }}\n\r";
                //}
                //
            }
            //GenerateModels(modelsNamespace, modelTypeName, tableInfo.Name, modelcontent, modelTypeDesc, keyTypeName, ifExsitedCovered);
            //GenerateInputDto(modelsNamespace, modelTypeName, modelTypeDesc, InputDtocontent, keyTypeName, ifExsitedCovered);
            //GenerateRepository(modelTypeName, modelTypeDesc, tableInfo.Name, keyTypeName, ifExsitedCovered);
            //GenerateIService(modelsNamespace, modelTypeName, modelTypeDesc, keyTypeName, ifExsitedCovered);
            //GenerateService(modelsNamespace, modelTypeName, modelTypeDesc, keyTypeName, ifExsitedCovered);
            //GenerateControllers(modelTypeName, primaryKey, modelTypeDesc, keyTypeName, ifExsitedCovered);

            //GenerateIRepository(modelTypeName, modelTypeDesc, keyTypeName, ifExsitedCovered);
            //GenerateOutputDto(modelTypeName, modelTypeDesc, outputDtocontent, ifExsitedCovered);
            GenerateVueViews(modelTypeName, primaryKey, modelTypeDesc, vueViewListContent, vueViewFromContent, vueViewEditFromContent, vueViewEditFromBindContent, vueViewSaveBindContent, vueViewEditFromRuleContent, ifExsitedCovered);
        }


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
        private static void GenerateModels(string modelsNamespace, string modelTypeName, string tableName, string modelContent, string modelTypeDesc, string keyTypeName, bool ifExsitedCovered = false)
        {
            var parentPath = "..";
            //../ZR.Model
            var servicesPath = parentPath + "\\" + _option.BaseNamespace + "\\" + modelsNamespace;
            if (!Directory.Exists(servicesPath))
            {
                //servicesPath = parentPath + "\\" + _option.ModelsNamespace;
                Directory.CreateDirectory(servicesPath);
            }
            // ../ZR.Model/Models/User.cs
            var fullPath = servicesPath + "\\Models\\" + modelTypeName + ".cs";
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return;
            var content = ReadTemplate("ModelTemplate.txt");
            content = content
                .Replace("{ModelsNamespace}", modelsNamespace)
                .Replace("{ModelTypeName}", modelTypeName)
                .Replace("{TableNameDesc}", modelTypeDesc)
                .Replace("{KeyTypeName}", keyTypeName)
                .Replace("{PropertyName}", modelContent)
                .Replace("{TableName}", tableName);
            WriteAndSave(fullPath, content);
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
        private static void GenerateInputDto(string modelsNamespace, string modelTypeName, string modelTypeDesc, string modelContent, string keyTypeName, bool ifExsitedCovered = false)
        {
            var parentPath = "..";
            var servicesPath = parentPath + "\\" + _option.BaseNamespace + "\\" + modelsNamespace;
            if (!Directory.Exists(servicesPath))
            {
                //servicesPath = parentPath + "\\" + _option.BaseNamespace + "\\Dtos";
                Directory.CreateDirectory(servicesPath);
            }
            // ../ZR.Model/Dto/User.cs
            var fullPath = servicesPath + "\\Dto\\" + modelTypeName + "Dto.cs";
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return;
            var content = ReadTemplate("InputDtoTemplate.txt");
            content = content
                .Replace("{DtosNamespace}", _option.DtosNamespace)
                .Replace("{ModelsNamespace}", modelsNamespace)
                .Replace("{TableNameDesc}", modelTypeDesc)
                .Replace("{KeyTypeName}", keyTypeName)
                .Replace("{PropertyName}", modelContent)
                .Replace("{ModelTypeName}", modelTypeName);
            WriteAndSave(fullPath, content);
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
        private static void GenerateRepository(string modelTypeName, string modelTypeDesc, string tableName, string keyTypeName, bool ifExsitedCovered = false)
        {
            //var path = AppDomain.CurrentDomain.BaseDirectory;
            var parentPath = "..";// path.Substring(0, path.LastIndexOf("\\"));
            var repositoryPath = parentPath + "\\" + _option.BaseNamespace + "\\" + _option.RepositoriesNamespace;
            if (!Directory.Exists(repositoryPath))
            {
                //repositoryPath = parentPath + "\\" + _option.BaseNamespace + "\\Repositories";
                Directory.CreateDirectory(repositoryPath);
            }
            var fullPath = repositoryPath + "\\" + modelTypeName + "Repository.cs";
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return;
            var content = ReadTemplate("RepositoryTemplate.txt");
            content = content.Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{IRepositoriesNamespace}", _option.IRepositoriesNamespace)
                .Replace("{RepositoriesNamespace}", _option.RepositoriesNamespace)
                .Replace("{ModelTypeName}", modelTypeName)
                .Replace("{TableNameDesc}", modelTypeDesc)
                .Replace("{TableName}", tableName)
                .Replace("{KeyTypeName}", keyTypeName);
            WriteAndSave(fullPath, content);
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
        private static void GenerateIService(string modelsNamespace, string modelTypeName, string modelTypeDesc, string keyTypeName, bool ifExsitedCovered = false)
        {
            //var path = AppDomain.CurrentDomain.BaseDirectory;
            //path = path.Substring(0, path.IndexOf("\\bin"));
            var parentPath = "..";// path.Substring(0, path.LastIndexOf("\\"));
            var iServicesPath = parentPath + "\\" + _option.BaseNamespace + "\\" + _option.IServicsNamespace;
            if (!Directory.Exists(iServicesPath))
            {
                iServicesPath = parentPath + "\\" + _option.BaseNamespace + "\\IBusService";
                Directory.CreateDirectory(iServicesPath);
            }
            var fullPath = $"{iServicesPath}\\Business\\IService\\I{modelTypeName}Service.cs";
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return;
            var content = ReadTemplate("IServiceTemplate.txt");
            content = content.Replace("{ModelsNamespace}", modelsNamespace)
                .Replace("{DtosNamespace}", _option.DtosNamespace)
                .Replace("{TableNameDesc}", modelTypeDesc)
                .Replace("{IServicsNamespace}", _option.IServicsNamespace)
                .Replace("{ModelTypeName}", modelTypeName)
                .Replace("{KeyTypeName}", keyTypeName);
            WriteAndSave(fullPath, content);
        }

        /// <summary>
        /// 生成Service文件
        /// </summary>
        /// <param name="modelsNamespace"></param>
        /// <param name="modelTypeName"></param>
        /// <param name="modelTypeDesc"></param>
        /// <param name="keyTypeName"></param>
        /// <param name="ifExsitedCovered">如果目标文件存在，是否覆盖。默认为false</param>
        private static void GenerateService(string modelsNamespace, string modelTypeName, string modelTypeDesc, string keyTypeName, bool ifExsitedCovered = false)
        {
            //var path = AppDomain.CurrentDomain.BaseDirectory;
            //path = path.Substring(0, path.IndexOf("\\bin"));
            var parentPath = "..";// path.Substring(0, path.LastIndexOf("\\"));
            var servicesPath = parentPath + "\\" + _option.BaseNamespace + "\\" + _option.ServicesNamespace;
            if (!Directory.Exists(servicesPath))
            {
                servicesPath = parentPath + "\\" + _option.BaseNamespace + "\\Business";
                Directory.CreateDirectory(servicesPath);
            }
            var fullPath = servicesPath + "\\Business\\" + modelTypeName + "Service.cs";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return;
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
        private static void GenerateControllers(string modelTypeName, string primaryKey, string modelTypeDesc, string keyTypeName, bool ifExsitedCovered = false)
        {
            //var servicesNamespace = _option.DtosNamespace;
            //var fileClassName = _option.BaseNamespace.Substring(_option.BaseNamespace.IndexOf('.') + 1);
            //var path = AppDomain.CurrentDomain.BaseDirectory;
            //path = path.Substring(0, path.IndexOf("\\bin"));
            var parentPath = "..";//path.Substring(0, path.LastIndexOf("\\"));
            var servicesPath = parentPath + "\\" + _option.BaseNamespace + "\\" + _option.ApiControllerNamespace;
            if (!Directory.Exists(servicesPath))
            {
                servicesPath = parentPath + "\\" + _option.BaseNamespace + "\\Controllers\\";
                Directory.CreateDirectory(servicesPath);
            }
            var fullPath = servicesPath + "\\Controllers\\business\\" + modelTypeName + "Controller.cs";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return;
            var content = ReadTemplate("ControllersTemplate.txt");
            content = content
                //.Replace("{DtosNamespace}", _option.DtosNamespace)
                .Replace("<#=ControllerName#>", modelTypeName)
                //.Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("<#=FileName#>", modelTypeDesc)
                .Replace("<#=ServiceName#>", modelTypeName + "Service")
                .Replace("<#=ModelName#>", modelTypeName)
                .Replace("{primaryKey}", primaryKey)
                .Replace("{KeyTypeName}", keyTypeName);
            WriteAndSave(fullPath, content);
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
        private static void GenerateVueViews(string modelTypeName, string primaryKey, string modelTypeDesc, string vueViewListContent, string vueViewFromContent, string vueViewEditFromContent, string vueViewEditFromBindContent, string vueViewSaveBindContent, string vueViewEditFromRuleContent, bool ifExsitedCovered = false)
        {
            var servicesNamespace = _option.DtosNamespace;
            var fileClassName = _option.BaseNamespace.Substring(_option.BaseNamespace.IndexOf('.') + 1);
            var path = AppDomain.CurrentDomain.BaseDirectory;
            //path = path.Substring(0, path.IndexOf("\\bin"));
            var parentPath = path.Substring(0, path.LastIndexOf("\\"));
            var servicesPath = parentPath + "\\" + _option.BaseNamespace + "\\" + servicesNamespace;
            if (!Directory.Exists(servicesPath))
            {
                servicesPath = parentPath + "\\" + _option.BaseNamespace + "\\vue\\" + modelTypeName.ToLower();
                Directory.CreateDirectory(servicesPath);
            }
            var fullPath = servicesPath + "\\" + "index.vue";
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return;
            var content = ReadTemplate("VueTemplate.txt");
            content = content
                .Replace("{BaseNamespace}", fileClassName.ToLower())
                .Replace("{fileClassName}", modelTypeName.ToLower())
                .Replace("{ModelTypeNameToLower}", modelTypeName.ToLower())
                .Replace("{VueViewListContent}", vueViewListContent)
                .Replace("{VueViewFromContent}", vueViewFromContent)
                .Replace("{ModelTypeName}", modelTypeName)
                .Replace("{VueViewEditFromContent}", vueViewEditFromContent)
                .Replace("{VueViewEditFromBindContent}", vueViewEditFromBindContent)
                .Replace("{VueViewSaveBindContent}", vueViewSaveBindContent)
                .Replace("{primaryKey}", primaryKey)
                .Replace("{VueViewEditFromRuleContent}", vueViewEditFromRuleContent);
            WriteAndSave(fullPath, content);

            fullPath = servicesPath + "\\" + modelTypeName.ToLower() + ".js";
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return;
            content = ReadTemplate("VueJsTemplate.txt");
            content = content
                .Replace("{ModelTypeName}", modelTypeName)
                .Replace("{ModelTypeDesc}", modelTypeDesc)
                .Replace("{fileClassName}", fileClassName);
            WriteAndSave(fullPath, content);
        }

        #endregion

        #region 帮助方法

        private static string GetModelName(string modelTypeName)
        {
            if (!string.IsNullOrEmpty(_option.ReplaceTableNameStr))
            {
                modelTypeName = modelTypeName.Replace(_option.ReplaceTableNameStr.ToString(), "");
            }
            modelTypeName = modelTypeName.Replace("_", "");
            modelTypeName = modelTypeName.Substring(0, 1).ToUpper() + modelTypeName.Substring(1);
            return modelTypeName;
        }
        private static string FirstLowerCase(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }
            str = str.Substring(0, 1).ToLower() + str.Substring(1);
            return str;
        }
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
