using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public static readonly string[] inputDtoNoField = new string[] { "DeleteMark", "CreateTime", "updateTime", "addtime" };
        public static readonly string[] imageFiled = new string[] { "icon", "img", "image", "url", "pic", "photo" };
        public static readonly string[] selectFiled = new string[] { "status", "type", "state", "sex", "gender" };
        public static readonly string[] radioFiled = new string[] { "status", "state", "isShow", "isHidden", "ishide" };

        /// <summary>
        /// 代码生成器入口方法
        /// </summary>
        /// <param name="dbTableInfo"></param>
        /// <param name="dto"></param>
        public static void Generate(DbTableInfo dbTableInfo, GenerateDto dto)
        {
            _option.BaseNamespace = "ZR.";
            //_option.TableList = listTable;
            _option.ReplaceTableNameStr = dto.replaceTableNameStr;
            _option.DtosNamespace = _option.BaseNamespace + "Model";
            _option.ModelsNamespace = _option.BaseNamespace + "Model";
            _option.RepositoriesNamespace = _option.BaseNamespace + "Repository";
            _option.IRepositoriesNamespace = _option.BaseNamespace + "Repository";
            _option.IServicsNamespace = _option.BaseNamespace + "Service";
            _option.ServicesNamespace = _option.BaseNamespace + "Service";
            _option.ApiControllerNamespace = _option.BaseNamespace + "Admin.WebApi";

            CodeGeneraterService codeGeneraterService = new();
            List<DbColumnInfo> listField = codeGeneraterService.GetColumnInfo(dto.dbName, dbTableInfo.Name);
            GenerateSingle(listField, dbTableInfo, dto);

            //GenerateDtoProfile(_option.ModelsNamespace, profileContent, ifExsitedCovered);
        }

        /// <summary>
        /// 单表生成代码
        /// </summary>
        /// <param name="listField">表字段集合</param>
        /// <param name="tableInfo">表信息</param>
        /// <param name="dto"></param>
        public static void GenerateSingle(List<DbColumnInfo> listField, DbTableInfo tableInfo, GenerateDto dto)
        {
            var modelTypeName = GetModelClassName(tableInfo.Name);//表名对应C# 实体类名
            var primaryKey = "id";//主键

            string keyTypeName = "int";//主键数据类型
            string modelContent = "";//数据库模型字段
            string InputDtoContent = "";//输入模型
            //string outputDtoContent = "";//输出模型
            string updateColumn = "";//修改数据映射字段
            string vueViewListContent = string.Empty;//Vue列表输出内容
            string vueViewFormContent = string.Empty;//Vue表单输出内容 
            string vueViewEditFromContent = string.Empty;//Vue变量输出内容
            string vueViewEditFromBindContent = string.Empty;//Vue显示初始化输出内容
            string vueViewSaveBindContent = string.Empty;//Vue保存时输出内容
            string vueViewEditFromRuleContent = string.Empty;//Vue数据校验
            string vueJsMethod = string.Empty;//Vue js自定义方法

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
                modelContent += CodeGenerateTemplate.GetModelTemplate(dbFieldInfo);
                vueViewFormContent += CodeGenerateTemplate.GetVueViewFormContent(dbFieldInfo);
                vueJsMethod += CodeGenerateTemplate.GetVueJsMethod(dbFieldInfo);
                vueViewListContent += CodeGenerateTemplate.GetTableColumn(dbFieldInfo);
                vueViewEditFromRuleContent += CodeGenerateTemplate.GetFormRules(dbFieldInfo);
                InputDtoContent += CodeGenerateTemplate.GetDtoContent(dbFieldInfo);
            }
            ReplaceDto replaceDto = new();
            replaceDto.KeyTypeName = keyTypeName;
            replaceDto.PrimaryKey = primaryKey;
            replaceDto.ModelTypeName = modelTypeName;
            replaceDto.ModelProperty = modelContent;
            replaceDto.TableName = tableInfo.Name;
            replaceDto.TableDesc = tableInfo.Description;
            replaceDto.InputDtoProperty = InputDtoContent;
            replaceDto.updateColumn = updateColumn;
            replaceDto.VueJsMethod = vueJsMethod;
            replaceDto.VueViewEditFormContent = vueViewEditFromContent;
            replaceDto.VueViewFormContent = vueViewFormContent;
            replaceDto.VueViewEditFormRuleContent = vueViewEditFromRuleContent;
            replaceDto.VueViewListContent = vueViewListContent;

            if (dto.genFiles.Contains(1))
            {
                GenerateModels(replaceDto, dto);
            }
            if (dto.genFiles.Contains(2))
            {
                GenerateInputDto(replaceDto, dto);
            }
            if (dto.genFiles.Contains(3))
            {
                GenerateRepository(replaceDto, dto);
            }
            if (dto.genFiles.Contains(4))
            {
                GenerateIService(replaceDto, dto);
                GenerateService(replaceDto, dto);
            }
            if (dto.genFiles.Contains(5))
            {
                GenerateControllers(replaceDto, dto);
            }
            if (dto.genFiles.Contains(6))
            {
                GenerateVueViews(replaceDto, dto);
            }
            //GenerateIRepository(modelTypeName, modelTypeDesc, keyTypeName, ifExsitedCovered);
            //GenerateOutputDto(modelTypeName, modelTypeDesc, outputDtocontent, ifExsitedCovered);
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
        private static Tuple<string, string> GenerateModels(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var parentPath = "..";
            //../ZR.Model
            var servicesPath = parentPath + "\\" + _option.ModelsNamespace + "\\Models\\";
            if (!Directory.Exists(servicesPath))
            {
                Directory.CreateDirectory(servicesPath);
            }
            // ../ZR.Model/Models/User.cs
            var fullPath = servicesPath + replaceDto.ModelTypeName + ".cs";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, "");
            var content = ReadTemplate("ModelTemplate.txt");
            content = content
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{TableNameDesc}", replaceDto.TableDesc)
                .Replace("{KeyTypeName}", replaceDto.KeyTypeName)
                .Replace("{PropertyName}", replaceDto.ModelProperty)
                .Replace("{TableName}", replaceDto.TableName);
            WriteAndSave(fullPath, content);
            return Tuple.Create(fullPath, content);
        }


        /// <summary>
        /// 生成InputDto文件
        /// </summary>
        /// <param name="generateDto"></param>
        /// <param name="replaceDto"></param>
        private static Tuple<string, string> GenerateInputDto(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var parentPath = "..";
            var servicesPath = parentPath + "\\" + _option.ModelsNamespace + "\\Dto\\";
            if (!Directory.Exists(servicesPath))
            {
                Directory.CreateDirectory(servicesPath);
            }
            // ../ZR.Model/Dto/User.cs
            var fullPath = servicesPath + replaceDto.ModelTypeName + "Dto.cs";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, ""); ;
            var content = ReadTemplate("InputDtoTemplate.txt");
            content = content
                .Replace("{DtosNamespace}", _option.DtosNamespace)
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{TableNameDesc}", replaceDto.TableDesc)
                .Replace("{KeyTypeName}", replaceDto.KeyTypeName)
                .Replace("{PropertyName}", replaceDto.InputDtoProperty)
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName);
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
        private static Tuple<string, string> GenerateRepository(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var parentPath = "..";
            var repositoryPath = parentPath + "\\" + _option.RepositoriesNamespace + "\\Repositories\\";
            if (!Directory.Exists(repositoryPath))
            {
                Directory.CreateDirectory(repositoryPath);
            }
            var fullPath = repositoryPath + "\\" + replaceDto.ModelTypeName + "Repository.cs";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, "");
            var content = ReadTemplate("RepositoryTemplate.txt");
            content = content.Replace("{ModelsNamespace}", _option.ModelsNamespace)
                //.Replace("{IRepositoriesNamespace}", _option.IRepositoriesNamespace)
                .Replace("{RepositoriesNamespace}", _option.RepositoriesNamespace)
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{TableNameDesc}", replaceDto.TableDesc)
                .Replace("{TableName}", replaceDto.TableName)
                .Replace("{KeyTypeName}", replaceDto.KeyTypeName);
            WriteAndSave(fullPath, content);
            return Tuple.Create(fullPath, content);
        }

        #endregion

        #region 生成Service
        /// <summary>
        /// 生成IService文件
        /// </summary>
        private static Tuple<string, string> GenerateIService(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var parentPath = "..";
            var iServicesPath = parentPath + "\\" + _option.IServicsNamespace + "\\Business\\IBusService\\";
            if (!Directory.Exists(iServicesPath))
            {
                Directory.CreateDirectory(iServicesPath);
            }
            var fullPath = $"{iServicesPath}\\I{replaceDto.ModelTypeName}Service.cs";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, "");
            var content = ReadTemplate("IServiceTemplate.txt");
            content = content.Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{TableNameDesc}", replaceDto.TableDesc)
                .Replace("{DtosNamespace}", _option.DtosNamespace)
                .Replace("{IServicsNamespace}", _option.IServicsNamespace)
                .Replace("{RepositoriesNamespace}", _option.RepositoriesNamespace)
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{KeyTypeName}", replaceDto.KeyTypeName);
            WriteAndSave(fullPath, content);
            return Tuple.Create(fullPath, content);
        }

        /// <summary>
        /// 生成Service文件
        /// </summary>
        private static Tuple<string, string> GenerateService(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var parentPath = "..";
            var servicesPath = parentPath + "\\" + _option.ServicesNamespace + "\\Business\\";
            if (!Directory.Exists(servicesPath))
            {
                Directory.CreateDirectory(servicesPath);
            }
            var fullPath = servicesPath + replaceDto.ModelTypeName + "Service.cs";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, "");
            var content = ReadTemplate("ServiceTemplate.txt");
            content = content
                .Replace("{IRepositoriesNamespace}", _option.IRepositoriesNamespace)
                .Replace("{DtosNamespace}", _option.DtosNamespace)
                .Replace("{IServicsNamespace}", _option.IServicsNamespace)
                .Replace("{TableNameDesc}", replaceDto.TableDesc)
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{ServicesNamespace}", _option.ServicesNamespace)
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{KeyTypeName}", replaceDto.KeyTypeName);
            WriteAndSave(fullPath, content);
            return Tuple.Create(fullPath, content);
        }

        #endregion

        #region 生成Controller
        /// <summary>
        /// 生成控制器ApiControllers文件
        /// </summary>
        private static Tuple<string, string> GenerateControllers(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            var parentPath = "..";
            var servicesPath = parentPath + "\\" + _option.ApiControllerNamespace + "\\Controllers\\business\\";
            if (!Directory.Exists(servicesPath))
            {
                Directory.CreateDirectory(servicesPath);
            }
            var fullPath = servicesPath + replaceDto.ModelTypeName + "Controller.cs";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, "");
            var content = ReadTemplate("ControllersTemplate.txt");
            content = content
                .Replace("{ApiControllerNamespace}", _option.ApiControllerNamespace)
                .Replace("{ServicesNamespace}", _option.ServicesNamespace)
                .Replace("{ModelsNamespace}", _option.ModelsNamespace)
                .Replace("{TableDesc}", replaceDto.TableDesc)
                .Replace("{ModelName}", replaceDto.ModelTypeName)
                .Replace("{Permission}", replaceDto.ModelTypeName.ToLower())
                .Replace("{PrimaryKey}", replaceDto.PrimaryKey)
                .Replace("{UpdateColumn}", replaceDto.updateColumn)
                .Replace("{KeyTypeName}", replaceDto.KeyTypeName);
            WriteAndSave(fullPath, content);
            return Tuple.Create(fullPath, content);
        }
        #endregion

        #region 生成Vue页面
        /// <summary>
        /// 生成Vue页面
        private static Tuple<string, string> GenerateVueViews(ReplaceDto replaceDto, GenerateDto generateDto)
        {
            //var parentPath = "..\\CodeGenerate";//若要生成到项目中将路径改成 “..\\ZR.Vue\\src”
            var parentPath = "..\\ZR.Vue\\src";
            var servicesPath = parentPath + "\\views\\" + FirstLowerCase(replaceDto.ModelTypeName);
            if (!Directory.Exists(servicesPath))
            {
                Directory.CreateDirectory(servicesPath);
            }
            var fullPath = servicesPath + "\\" + "index.vue";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, ""); ;
            var content = ReadTemplate("VueTemplate.txt");
            content = content
                .Replace("{fileClassName}", FirstLowerCase(replaceDto.ModelTypeName))
                .Replace("{VueViewListContent}", replaceDto.VueViewListContent)//查询 table列
                .Replace("{VueViewFormContent}", replaceDto.VueViewFormContent)//添加、修改表单
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{Permission}", replaceDto.ModelTypeName.ToLower())
                .Replace("{VueViewEditFormContent}", replaceDto.VueViewEditFormContent)
                .Replace("{vueJsMethod}", replaceDto.VueJsMethod)
                //.Replace("{VueViewEditFromBindContent}", vueViewEditFromBindContent)
                //.Replace("{VueViewSaveBindContent}", vueViewSaveBindContent)
                .Replace("{primaryKey}", FirstLowerCase(replaceDto.PrimaryKey))
                .Replace("{VueViewEditFormRuleContent}", replaceDto.VueViewEditFormRuleContent);//添加、修改表单验证规则
            WriteAndSave(fullPath, content);

            //api js
            servicesPath = parentPath + "\\api\\";
            Directory.CreateDirectory(servicesPath);
            fullPath = servicesPath + "\\" + FirstLowerCase(replaceDto.ModelTypeName) + ".js";
            Console.WriteLine(fullPath);
            if (File.Exists(fullPath) && !generateDto.coverd)
                return Tuple.Create(fullPath, "");
            content = ReadTemplate("VueJsTemplate.txt");
            content = content
                .Replace("{ModelTypeName}", replaceDto.ModelTypeName)
                .Replace("{ModelTypeDesc}", replaceDto.TableDesc);
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
        public static string GetModelClassName(string modelTypeName)
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
