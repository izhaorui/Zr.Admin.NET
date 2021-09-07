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
            _option.DtosNamespace = baseNamespace + "ZR.Model.Dto";
            _option.ModelsNamespace = baseNamespace + "ZR.Model";
            //_option.IRepositoriesNamespace = baseNamespace + ".IRepositorie";
            _option.RepositoriesNamespace = baseNamespace + "ZR.Repository";
            //_option.IServicsNamespace = baseNamespace + ".IService";
            _option.ServicesNamespace = baseNamespace + "ZR.Service";
            _option.ApiControllerNamespace = baseNamespace + "Api";
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
            var modelTypeName = tableInfo.Name;//表名
            var modelTypeDesc = tableInfo.Description;//表描述
            if (!string.IsNullOrEmpty(_option.ReplaceTableNameStr))
            {
                modelTypeName = modelTypeName.Replace(_option.ReplaceTableNameStr.ToString(), "");
            }
            modelTypeName = modelTypeName.Replace("_", "");
            modelTypeName = modelTypeName.Substring(0, 1).ToUpper() + modelTypeName.Substring(1);

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
                    modelcontent += $"        [SqlSugar.SugarColumn(IsPrimaryKey = {dbFieldInfo.IsPrimarykey.ToString().ToLower()}, IsIdentity = {dbFieldInfo.IsIdentity.ToString().ToLower()})]\n";
                }
                modelcontent += $"        public {TableMappingHelper.GetPropertyDatatype(dbFieldInfo.DataType)} {columnName} {{ get; set; }}\n\r";

                //主键
                //if (dbFieldInfo.IsIdentity)
                //{
                    //keyTypeName = dbFieldInfo.DataType;
                    //outputDtocontent += "        /// <summary>\n";
                    //outputDtocontent += string.Format("        /// 设置或获取{0}\n", dbFieldInfo.ColumnDescription);
                    //outputDtocontent += "        /// </summary>\n";

                    //outputDtocontent += string.Format("        [SqlSugar.SugarColumn(IsIdentity = true, IsPrimaryKey = true)]\n");
                    //outputDtocontent += string.Format("        public {0} {1}", TableMappingHelper.GetPropertyDatatype(dbFieldInfo.DataType), columnName);
                    //outputDtocontent += " { get; set; }\n\r";
                //}
               // else //非主键
                //{
                    //modelcontent += "        /// <summary>\n";
                    //modelcontent += string.Format("        /// 设置或获取{0}\n", dbFieldInfo.ColumnDescription);
                    //modelcontent += "        /// </summary>\n";
                    ////if (dbFieldInfo.DataType == "string")
                    ////{
                    ////    modelcontent += string.Format("        [MaxLength({0})]\n", dbFieldInfo.FieldMaxLength);
                    ////}
                    //modelcontent += string.Format("        public {0} {1}", TableMappingHelper.GetPropertyDatatype(dbFieldInfo.DataType), columnName);
                    //modelcontent += " { get; set; }\n\r";


                    //outputDtocontent += "        /// <summary>\n";
                    //outputDtocontent += string.Format("        /// 设置或获取{0}\n", dbFieldInfo.ColumnDescription);
                    //outputDtocontent += "        /// </summary>\n";
                    //if (dbFieldInfo.DataType == "string")
                    //{
                    //    outputDtocontent += string.Format("        [MaxLength({0})]\n", dbFieldInfo.FieldMaxLength);
                    //}
                    //outputDtocontent += string.Format("        public {0} {1}", dbFieldInfo.DataType, columnName);
                    //outputDtocontent += " { get; set; }\n\r";
                    //if (dbFieldInfo.DataType == "bool" || dbFieldInfo.DataType == "tinyint")
                    //{

                    //    vueViewListContent += string.Format("        <el-table-column prop=\"{0}\" label=\"{1}\" sortable=\"custom\" width=\"120\" >\n", columnName, dbFieldInfo.ColumnDescription);
                    //    vueViewListContent += "          <template slot-scope=\"scope\">\n";
                    //    vueViewListContent += string.Format("            <el-tag :type=\"scope.row.{0} === true ? 'success' : 'info'\"  disable-transitions >", columnName);
                    //    vueViewListContent += "{{ ";
                    //    vueViewListContent += string.Format("scope.row.{0}===true?'启用':'禁用' ", columnName);
                    //    vueViewListContent += "}}</el-tag>\n";
                    //    vueViewListContent += "          </template>\n";
                    //    vueViewListContent += "        </el-table-column>\n";

                    //    vueViewFromContent += string.Format("        <el-form-item label=\"{0}\" :label-width=\"formLabelWidth\" prop=\"{1}\">", dbFieldInfo.ColumnDescription, columnName);
                    //    vueViewFromContent += string.Format("          <el-radio-group v-model=\"editFrom.{0}\">\n", columnName);
                    //    vueViewFromContent += "           <el-radio label=\"true\">是</el-radio>\n";
                    //    vueViewFromContent += "           <el-radio label=\"false\">否</el-radio>\n";
                    //    vueViewFromContent += "          </el-radio-group>\n";
                    //    vueViewFromContent += "        </el-form-item>\n";

                    //    vueViewEditFromContent += string.Format("        {0}: 'true',\n", columnName);
                    //    vueViewEditFromBindContent += string.Format("        this.editFrom.{0} = res.ResData.{0}+''\n", columnName);
                    //}
                    //else
                    //{
                    //    vueViewListContent += string.Format("        <el-table-column prop=\"{0}\" label=\"{1}\" sortable=\"custom\" width=\"120\" />\n", columnName, dbFieldInfo.ColumnDescription);

                    //    vueViewFromContent += string.Format("        <el-form-item label=\"{0}\" :label-width=\"formLabelWidth\" prop=\"{1}\">\n", dbFieldInfo.ColumnDescription, columnName);
                    //    vueViewFromContent += string.Format("          <el-input v-model=\"editFrom.{0}\" placeholder=\"请输入{1}\" autocomplete=\"off\" clearable />\n", columnName, dbFieldInfo.ColumnDescription);
                    //    vueViewFromContent += "        </el-form-item>\n";
                    //    vueViewEditFromContent += string.Format("        {0}: '',\n", columnName);
                    //    vueViewEditFromBindContent += string.Format("        this.editFrom.{0} = res.ResData.{0}\n", columnName);
                    //}
                    //vueViewSaveBindContent += string.Format("        '{0}':this.editFrom.{0},\n", columnName);
                    //if (!dbFieldInfo.IsNullable)
                    //{
                    //    vueViewEditFromRuleContent += string.Format("        {0}: [\n", columnName);
                    //    vueViewEditFromRuleContent += "        {";
                    //    vueViewEditFromRuleContent += string.Format("required: true, message:\"请输入{0}\", trigger: \"blur\"", dbFieldInfo.ColumnDescription);
                    //    vueViewEditFromRuleContent += "},\n          { min: 2, max: 50, message: \"长度在 2 到 50 个字符\", trigger:\"blur\" }\n";
                    //    vueViewEditFromRuleContent += "        ],\n";
                    //}
                }

                //if (!inputDtoNoField.Contains(columnName) || columnName == "Id")
                //{
                //    InputDtocontent += "        /// <summary>\n";
                //    InputDtocontent += string.Format("        /// 设置或获取{0}\n", dbFieldInfo.ColumnDescription);
                //    InputDtocontent += "        /// </summary>\n";
                //    //if (dbFieldInfo.FieldType == "string")
                //    //{
                //    //    InputDtocontent += string.Format("        [MaxLength({0})]\n", dbFieldInfo.FieldMaxLength);
                //    //}
                //    InputDtocontent += string.Format("        public {0} {1}", dbFieldInfo.DataType, columnName);
                //    InputDtocontent += " { get; set; }\n\r";
                //}
                //
            //}
            GenerateModels(modelsNamespace, modelTypeName, tableInfo.Name, modelcontent, modelTypeDesc, keyTypeName, ifExsitedCovered);
            //GenerateIRepository(modelTypeName, modelTypeDesc, keyTypeName, ifExsitedCovered);
            //GenerateRepository(modelTypeName, modelTypeDesc, tableInfo.TableName, keyTypeName, ifExsitedCovered);
            //GenerateIService(modelsNamespace, modelTypeName, modelTypeDesc, keyTypeName, ifExsitedCovered);
            //GenerateService(modelsNamespace, modelTypeName, modelTypeDesc, keyTypeName, ifExsitedCovered);
            //GenerateOutputDto(modelTypeName, modelTypeDesc, outputDtocontent, ifExsitedCovered);
            //GenerateInputDto(modelsNamespace, modelTypeName, modelTypeDesc, InputDtocontent, keyTypeName, ifExsitedCovered);
            //GenerateControllers(modelTypeName, modelTypeDesc, keyTypeName, ifExsitedCovered);
            //GenerateVueViews(modelTypeName, modelTypeDesc, vueViewListContent, vueViewFromContent, vueViewEditFromContent, vueViewEditFromBindContent, vueViewSaveBindContent, vueViewEditFromRuleContent, ifExsitedCovered);
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
            var parentPath = "..\\";
            var servicesPath = parentPath + _option.BaseNamespace + "\\" + modelsNamespace;
            if (!Directory.Exists(servicesPath))
            {
                servicesPath = parentPath + _option.ModelsNamespace;
                Directory.CreateDirectory(servicesPath);
            }
            var fullPath = servicesPath + "\\Models\\" + modelTypeName + ".cs";
            if (File.Exists(fullPath) && !ifExsitedCovered)
                return;
            var content = ReadTemplate("ModelTemplate.txt");
            content = content
                .Replace("{ModelsNamespace}", modelsNamespace)
                .Replace("{ModelTypeName}", modelTypeName)
                .Replace("{TableNameDesc}", modelTypeDesc)
                .Replace("{KeyTypeName}", keyTypeName)
                .Replace("{ModelContent}", modelContent)
                .Replace("{TableName}", tableName);
            WriteAndSave(fullPath, content);
        }
        #endregion

        #region 帮助方法

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

        #endregion
    }
}
