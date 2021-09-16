using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZR.CodeGenerator.CodeGenerator;

namespace ZR.CodeGenerator
{
    /// <summary>
    /// 代码生成模板
    /// </summary>
    public class CodeGenerateTemplate
    {
        #region Template

        /// <summary>
        /// 生成vuejs模板，目前只有上传文件方法
        /// </summary>
        /// <param name="dbColumnInfo"></param>
        /// <returns></returns>
        public static string GetVueJsMethod(DbColumnInfo dbColumnInfo)
        {
            string columnName = CodeGeneratorTool.FirstLowerCase(dbColumnInfo.DbColumnName);
            string js = "";
            if (CodeGeneratorTool.imageFiled.Any(f => columnName.Contains(f)))
            {
                js += $"handleUpload{columnName}Success(res, file) {{\n";
                js += $"     this.form.{columnName} = URL.createObjectURL(file.raw);\n";
                js += "     // this.$refs.upload.clearFiles();\n";
                js += "},\n";
            }
            return js;
        }

        //rules
        public static string GetFormRules(DbColumnInfo dbFieldInfo)
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
        public static string GetModelTemplate(DbColumnInfo dbFieldInfo)
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
        public static string GetDtoContent(DbColumnInfo dbFieldInfo)
        {
            string columnName = dbFieldInfo.DbColumnName.Substring(0, 1).ToUpper() + dbFieldInfo.DbColumnName[1..];
            string InputDtoContent = "";
            InputDtoContent += $"        public {TableMappingHelper.GetPropertyDatatype(dbFieldInfo.DataType)} {columnName} {{ get; set; }}\n\r";

            return InputDtoContent;
        }

        //form-item
        public static string GetVueViewFormContent(DbColumnInfo dbFieldInfo)
        {
            string columnName = CodeGeneratorTool.FirstLowerCase(dbFieldInfo.DbColumnName);
            string labelName = CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnDescription, columnName);
            string vueViewFromContent = "";
            string labelDisabled = dbFieldInfo.IsIdentity ? ":disabled=\"true\"" : "";
            string placeHolder = dbFieldInfo.IsIdentity ? "" : $"请输入{CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnDescription, columnName)}";

            if (dbFieldInfo.DataType == "datetime")
            {
                //时间
                vueViewFromContent += $"       <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">\n";
                vueViewFromContent += $"         <el-date-picker v-model=\"form.{columnName}\"  type=\"datetime\"  placeholder=\"选择日期时间\"  default-time=\"12:00:00\"> </el-date-picker>\n";
                vueViewFromContent += "        </el-form-item>\n";
            }
            else if (CodeGeneratorTool.imageFiled.Any(f => columnName.Contains(f)))
            {
                //图片
                vueViewFromContent += $"       <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">\n";
                vueViewFromContent += $"         <el-upload class=\"avatar-uploader\" name=\"file\" action=\"/api/upload/saveFile/\" :show-file-list=\"false\" :on-success=\"handleUpload{columnName}Success\" :before-upload=\"beforeFileUpload\">\n";
                vueViewFromContent += $"        <img v-if=\"form.{columnName}\" :src=\"form.{columnName}\" class=\"icon\">\n";
                vueViewFromContent += "         <i v-else class=\"el-icon-plus uploader-icon\"></i>\n";
                vueViewFromContent += "         </el-upload>\n";
                vueViewFromContent += $"        <el-input v-model=\"form.{columnName}\" placeholder=\"请上传文件或手动输入文件地址\"></el-input>\n";
                vueViewFromContent += "        </el-form-item>\n";
            }
            else if (CodeGeneratorTool.radioFiled.Any(f => columnName.Contains(f)) && (dbFieldInfo.DataType == "bool" || dbFieldInfo.DataType == "tinyint" || dbFieldInfo.DataType == "int"))
            {
                vueViewFromContent += $"       <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">";
                vueViewFromContent += $"         <el-radio-group v-model=\"form.{columnName}\">\n";
                vueViewFromContent += "           <el-radio v-for=\"dict in statusOptions\" :key=\"dict.dictValue\" :label=\"dict.dictValue\">{{dict.dictLabel}}</el-radio>\n";
                vueViewFromContent += "          </el-radio-group>\n";
                vueViewFromContent += "        </el-form-item>\n";
            }
            else
            {
                vueViewFromContent += $"        <el-form-item label=\"{ CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnDescription, columnName)}\" :label-width=\"labelWidth\" prop=\"{CodeGeneratorTool.FirstLowerCase(columnName)}\">\n";
                vueViewFromContent += $"           <el-input v-model=\"form.{CodeGeneratorTool.FirstLowerCase(columnName)}\" placeholder=\"{placeHolder}\" {labelDisabled}/>\n";
                vueViewFromContent += "        </el-form-item>\n";
            }

            return vueViewFromContent;
        }

        //table-column
        public static string GetTableColumn(DbColumnInfo dbFieldInfo)
        {
            string columnName = CodeGeneratorTool.FirstLowerCase(dbFieldInfo.DbColumnName);
            string label = CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnDescription, columnName);
            string vueViewListContent = "";
            string showToolTip = dbFieldInfo.DataType.Contains("varchar") ? ":show-overflow-tooltip=\"true\"" : "";

            if (CodeGeneratorTool.imageFiled.Any(f => columnName.ToLower().Contains(f)))
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
                vueViewListContent += $"      <el-table-column prop=\"{CodeGeneratorTool.FirstLowerCase(columnName)}\" label=\"{CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnDescription, columnName)}\"  align=\"center\" width=\"100\" {showToolTip} />\n";
            }
            return vueViewListContent;
        }
        #endregion

    }
}
