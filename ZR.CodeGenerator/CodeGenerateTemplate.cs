using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZR.CodeGenerator.CodeGenerator;
using ZR.Model.System.Generate;

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
        public static string GetVueJsMethod(GenTableColumn dbColumnInfo)
        {
            string columnName = dbColumnInfo.ColumnName;
            string js = "";
            if (dbColumnInfo.HtmlType.Equals(GenConstants.HTML_IMAGE_UPLOAD))
            {
                js += $"handleUpload{columnName}Success(res, file) {{\n";
                js += $"     this.form.{columnName} = URL.createObjectURL(file.raw);\n";
                js += "     // this.$refs.upload.clearFiles();\n";
                js += "},\n";
            }
            return js;
        }

        //rules
        public static string GetFormRules(GenTableColumn dbFieldInfo)
        {
            string vueViewEditFromRuleContent = "";
            //Rule 规则验证
            if (!dbFieldInfo.IsPk && !dbFieldInfo.IsIncrement)
            {
                vueViewEditFromRuleContent += $"        {dbFieldInfo.ColumnName}: [\n";
                vueViewEditFromRuleContent += $"        {{ required: true, message: '请输入{dbFieldInfo.ColumnComment}', trigger: \"blur\"}},\n";
                vueViewEditFromRuleContent += "        ],\n";
            }
            else if (TableMappingHelper.IsNumber(dbFieldInfo.ColumnType) && dbFieldInfo.IsRequired)
            {
                vueViewEditFromRuleContent += $"        {dbFieldInfo.ColumnName}: [\n";
                vueViewEditFromRuleContent += $"        {{ type: 'number', message: '{dbFieldInfo.ColumnName}必须为数字值', trigger: \"blur\"}},\n";
                vueViewEditFromRuleContent += "        ],\n";
            }

            return vueViewEditFromRuleContent;
        }

        //model 属性
        public static string GetModelTemplate(GenTableColumn dbFieldInfo)
        {
            var modelcontent = "";
            modelcontent += "        /// <summary>\n";
            modelcontent += $"        /// 描述 :{dbFieldInfo.ColumnComment}\n";
            modelcontent += $"        /// 空值 :{dbFieldInfo.IsRequired}\n";
            modelcontent += "        /// </summary>\n";
            if (dbFieldInfo.IsPk || dbFieldInfo.IsIncrement)
            {
                modelcontent += $"[SqlSugar.SugarColumn(IsPrimaryKey = {dbFieldInfo.IsPk.ToString().ToLower()}, IsIdentity = {dbFieldInfo.IsIncrement.ToString().ToLower()})]\n";
            }
            modelcontent += $"public {dbFieldInfo.CsharpType} {dbFieldInfo.CsharpField} {{ get; set; }}\n\r";
            return modelcontent;
        }
        //DTO model
        public static string GetDtoContent(GenTableColumn dbFieldInfo)
        {
            string InputDtoContent = "";
            if (dbFieldInfo.IsInsert || dbFieldInfo.IsEdit)
            {
                InputDtoContent += $"        public {dbFieldInfo.CsharpType} {dbFieldInfo.CsharpField} {{ get; set; }}\n\r";
            }

            return InputDtoContent;
        }

        //form-item
        public static string GetVueViewFormContent(GenTableColumn dbFieldInfo)
        {
            string columnName = dbFieldInfo.ColumnName;
            string labelName = CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnComment, columnName);
            string vueViewFromContent = "";
            string labelDisabled = dbFieldInfo.IsPk ? ":disabled=\"true\"" : "";
            string placeHolder = dbFieldInfo.IsIncrement ? "" : $"请输入{labelName}";
            if (!dbFieldInfo.IsInsert || !dbFieldInfo.IsEdit)
            {
                return vueViewFromContent;
            }
            if (dbFieldInfo.HtmlType == GenConstants.HTML_DATETIME)
            {
                //时间
                vueViewFromContent += $"       <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">\n";
                vueViewFromContent += $"         <el-date-picker v-model=\"form.{columnName}\"  type=\"datetime\"  placeholder=\"选择日期时间\"  default-time=\"12:00:00\"> </el-date-picker>\n";
                vueViewFromContent += "        </el-form-item>\n";
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_IMAGE_UPLOAD)
            {
                //图片
                vueViewFromContent += $"       <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">\n";
                vueViewFromContent += $"         <el-upload class=\"avatar-uploader\" name=\"file\" action=\"/api/upload/saveFile/\" :show-file-list=\"false\" :on-success=\"handleUpload{columnName}Success\" :before-upload=\"beforeFileUpload\">\n";
                vueViewFromContent += $"            <img v-if=\"form.{columnName}\" :src=\"form.{columnName}\" class=\"icon\">\n";
                vueViewFromContent += "             <i v-else class=\"el-icon-plus uploader-icon\"></i>\n";
                vueViewFromContent += "          </el-upload>\n";
                vueViewFromContent += $"         <el-input v-model=\"form.{columnName}\" placeholder=\"请上传文件或手动输入文件地址\"></el-input>\n";
                vueViewFromContent += "        </el-form-item>\n";
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_RADIO)
            {
                vueViewFromContent += $"       <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">\n";
                vueViewFromContent += $"         <el-radio-group v-model=\"form.{columnName}\">\n";
                vueViewFromContent += "           <el-radio :key=\"1\" :label=\"1\">是</el-radio>\n";
                vueViewFromContent += "           <el-radio :key=\"0\" :label=\"0\">否</el-radio>\n";
                vueViewFromContent += "          </el-radio-group>\n";
                vueViewFromContent += "        </el-form-item>\n";
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_TEXTAREA)
            {
                vueViewFromContent += $"        <el-form-item label=\"{ labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">\n";
                vueViewFromContent += $"           <el-input type=\"textarea\" v-model=\"form.{columnName}\" placeholder=\"请输入内容\"/>\n";
                vueViewFromContent += "        </el-form-item>\n";
            }
            else
            {
                string inputNumTxt = TableMappingHelper.IsNumber(dbFieldInfo.CsharpType) ? ".number" : "";
                vueViewFromContent += $"        <el-form-item label=\"{ labelName}\" :label-width=\"labelWidth\" prop=\"{CodeGeneratorTool.FirstLowerCase(columnName)}\">\n";
                vueViewFromContent += $"           <el-input v-model{inputNumTxt}=\"form.{CodeGeneratorTool.FirstLowerCase(columnName)}\" placeholder=\"{placeHolder}\" {labelDisabled}/>\n";
                vueViewFromContent += "        </el-form-item>\n";
            }

            return vueViewFromContent;
        }

        //table-column
        public static string GetTableColumn(GenTableColumn dbFieldInfo)
        {
            string columnName = dbFieldInfo.ColumnName;
            string label = CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnComment, columnName);
            string vueViewListContent = "";
            string showToolTip = dbFieldInfo.ColumnType.Contains("varchar") ? ":show-overflow-tooltip=\"true\"" : "";
            if (!dbFieldInfo.IsList)
            {

            }
            else if (dbFieldInfo.HtmlType.Equals(GenConstants.HTML_IMAGE_UPLOAD))
            {
                vueViewListContent += $"      <el-table-column prop=\"{ columnName}\" label=\"图片\">\n";
                vueViewListContent += "         <template slot-scope=\"scope\">\n";
                vueViewListContent += $"            <el-image class=\"table-td-thumb\" :src=\"scope.row.{columnName}\" :preview-src-list=\"[scope.row.{columnName}]\"></el-image>\n";
                vueViewListContent += "         </template>\n";
                vueViewListContent += "       </el-table-column>\n";
            }
            //else if (dbFieldInfo.HtmlType.Equals(GenConstants.HTML_RADIO))
            //{
            //    vueViewListContent += $"        <el-table-column prop=\"{columnName}\" label=\"{label}\" width=\"120\" >\n";
            //    vueViewListContent += "          <template slot-scope=\"scope\">\n";
            //    vueViewListContent += $"            <el-tag :type=\"scope.row.{columnName} === true ? 'success' : 'info'\"  disable-transitions >";
            //    vueViewListContent += $"                {{scope.row.{columnName}===true?'启用':'禁用'}} </el-tag>\n";
            //    vueViewListContent += "          </template>\n";
            //    vueViewListContent += "        </el-table-column>\n";
            //}
            else
            {
                vueViewListContent += $"      <el-table-column prop=\"{columnName}\" label=\"{label}\" align=\"center\" width=\"100\" {showToolTip} />\n";
            }
            return vueViewListContent;
        }
        #endregion

    }
}
