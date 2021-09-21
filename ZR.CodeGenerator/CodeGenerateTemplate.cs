using System.Linq;
using ZR.CodeGenerator.CodeGenerator;
using ZR.Model.System.Generate;

namespace ZR.CodeGenerator
{
    /// <summary>
    /// 代码生成模板
    /// </summary>
    public class CodeGenerateTemplate
    {
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
                js += $"handleUpload{columnName}Success(res, file) {{\r\n";
                js += $"      this.form.{columnName} = URL.createObjectURL(file.raw);\r\n";
                js += "      // this.$refs.upload.clearFiles();\r\n";
                js += "    },\r";
            }
            return js;
        }

        //rules
        public static string GetFormRules(GenTableColumn dbFieldInfo)
        {
            string vueViewEditFromRuleContent = "";
            //Rule 规则验证
            if ((!dbFieldInfo.IsPk && !dbFieldInfo.IsIncrement) && dbFieldInfo.IsRequired)
            {
                vueViewEditFromRuleContent += $"        {dbFieldInfo.ColumnName}: [\r\n";
                vueViewEditFromRuleContent += $"        {{ required: true, message: '请输入{dbFieldInfo.ColumnComment}', trigger: \"blur\"}},\r\n";
                vueViewEditFromRuleContent += "        ],\r\n";
            }
            else if (TableMappingHelper.IsNumber(dbFieldInfo.ColumnType) && dbFieldInfo.IsRequired)
            {
                vueViewEditFromRuleContent += $"        {dbFieldInfo.ColumnName}: [\r\n";
                vueViewEditFromRuleContent += $"        {{ type: 'number', message: '{dbFieldInfo.ColumnName}必须为数字值', trigger: \"blur\"}},\r\n";
                vueViewEditFromRuleContent += "        ],\r\n";
            }

            return vueViewEditFromRuleContent;
        }

        //model 属性
        public static string GetModelTemplate(GenTableColumn dbFieldInfo)
        {
            var modelcontent = "";
            modelcontent += "        /// <summary>\r\n";
            modelcontent += $"        /// 描述 :{dbFieldInfo.ColumnComment}\r\n";
            modelcontent += $"        /// 空值 :{!dbFieldInfo.IsRequired}\r\n";
            modelcontent += "        /// </summary>\r\n";
            if (dbFieldInfo.IsPk || dbFieldInfo.IsIncrement)
            {
                modelcontent += $"        [SqlSugar.SugarColumn(IsPrimaryKey = {dbFieldInfo.IsPk.ToString().ToLower()}, IsIdentity = {dbFieldInfo.IsIncrement.ToString().ToLower()})]\r\n";
            }
            modelcontent += $"        public {dbFieldInfo.CsharpType}{(GetModelRequired(dbFieldInfo))} {dbFieldInfo.CsharpField} {{ get; set; }}\r\n";
            return modelcontent;
        }
        public static string GetModelRequired(GenTableColumn dbFieldInfo)
        {
            string str = "";
            if (!dbFieldInfo.IsRequired && (dbFieldInfo.CsharpType == "int" || dbFieldInfo.CsharpType == "long" || dbFieldInfo.CsharpType == "DateTime"))
            {
                str = "?";
            }

            return str;
        }
        //DTO model
        public static string GetDtoProperty(GenTableColumn dbFieldInfo)
        {
            string InputDtoContent = "";
            if (CodeGeneratorTool.inputDtoNoField.Any(f => f.Replace("_", "").ToLower().Contains(dbFieldInfo.CsharpField.ToLower().Replace("_", ""))))
            {
                return InputDtoContent;
            }
            else if (dbFieldInfo.IsInsert || dbFieldInfo.IsEdit || dbFieldInfo.IsPk || dbFieldInfo.IsIncrement)
            {
                InputDtoContent += $"        public {dbFieldInfo.CsharpType}{GetModelRequired(dbFieldInfo)} {dbFieldInfo.CsharpField} {{ get; set; }}\r\n";
            }

            return InputDtoContent;
        }
        /// <summary>
        /// 查询Dto属性
        /// </summary>
        /// <param name="dbFieldInfo"></param>
        /// <returns></returns>
        public static string GetQueryDtoProperty(GenTableColumn dbFieldInfo)
        {
            string QueryDtoContent = "";
            if (dbFieldInfo.IsQuery && !CodeGeneratorTool.inputDtoNoField.Any(f => f.Replace("_", "").ToLower().Contains(dbFieldInfo.CsharpField.ToLower().Replace("_", ""))))
            {
                QueryDtoContent += $"        public {dbFieldInfo.CsharpType} {dbFieldInfo.CsharpField} {{ get; set; }}\r\n";
            }

            return QueryDtoContent;
        }

        //form-item
        public static string GetVueViewFormContent(GenTableColumn dbFieldInfo)
        {
            string columnName = dbFieldInfo.ColumnName;
            string labelName = CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnComment, columnName);
            string vueViewFromContent = "";
            string labelDisabled = dbFieldInfo.IsPk ? ":disabled=\"true\"" : "";
            string placeHolder = dbFieldInfo.IsIncrement ? "" : $"请输入{labelName}";
            if (CodeGeneratorTool.inputDtoNoField.Any(f => f.Replace("_", "").ToLower().Contains(dbFieldInfo.CsharpField.ToLower().Replace("_", ""))))
            {
                return vueViewFromContent;
            }
            if (!dbFieldInfo.IsInsert || !dbFieldInfo.IsEdit)
            {
                return vueViewFromContent;
            }
            if (dbFieldInfo.HtmlType == GenConstants.HTML_DATETIME)
            {
                //时间
                vueViewFromContent += $"        <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">\r\n";
                vueViewFromContent += $"           <el-date-picker v-model=\"form.{columnName}\"  type=\"datetime\"  placeholder=\"选择日期时间\"  default-time=\"12:00:00\"> </el-date-picker>\r\n";
                vueViewFromContent += "         </el-form-item>\r\n";
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_IMAGE_UPLOAD)
            {
                //图片
                vueViewFromContent += $"       <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">\r\n";
                vueViewFromContent += $"         <el-upload class=\"avatar-uploader\" name=\"file\" action=\"/api/upload/saveFile/\" :show-file-list=\"false\" :on-success=\"handleUpload{columnName}Success\" :before-upload=\"beforeFileUpload\">\r\n";
                vueViewFromContent += $"            <img v-if=\"form.{columnName}\" :src=\"form.{columnName}\" class=\"icon\">\r\n";
                vueViewFromContent += "             <i v-else class=\"el-icon-plus uploader-icon\"></i>\r\n";
                vueViewFromContent += "          </el-upload>\r\n";
                vueViewFromContent += $"         <el-input v-model=\"form.{columnName}\" placeholder=\"请上传文件或手动输入文件地址\"></el-input>\r\n";
                vueViewFromContent += "        </el-form-item>\r\n";
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_RADIO)
            {
                vueViewFromContent += $"       <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">\r\n";
                vueViewFromContent += $"         <el-radio-group v-model=\"form.{columnName}\">\r\n";
                vueViewFromContent += "           <el-radio :key=\"1\" :label=\"1\">是</el-radio>\r\n";
                vueViewFromContent += "           <el-radio :key=\"0\" :label=\"0\">否</el-radio>\r\n";
                vueViewFromContent += "          </el-radio-group>\r\n";
                vueViewFromContent += "        </el-form-item>\r\n";
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_TEXTAREA)
            {
                vueViewFromContent += $"        <el-form-item label=\"{ labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">\r\n";
                vueViewFromContent += $"           <el-input type=\"textarea\" v-model=\"form.{columnName}\" placeholder=\"请输入内容\"/>\r\n";
                vueViewFromContent += "         </el-form-item>\r\n";
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_SELECT && !string.IsNullOrEmpty(dbFieldInfo.DictType))
            {
                vueViewFromContent += $"        <el-form-item label=\"{ labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">\r\n";
                vueViewFromContent += $"           <el-select v-model=\"form.{columnName}\" > ";
                vueViewFromContent += $"            <el-option v-for=\"item in {columnName}Options\" :key=\"item.dictValue\" :label=\"item.dictLabel\" :value=\"item.dictValue\"></el-option>\r\n";
                vueViewFromContent += "           </el-select>\r\n";
                vueViewFromContent += "        </el-form-item>\r\n";
            }
            else
            {
                string inputNumTxt = TableMappingHelper.IsNumber(dbFieldInfo.CsharpType) ? ".number" : "";
                vueViewFromContent += $"        <el-form-item label=\"{ labelName}\" :label-width=\"labelWidth\" prop=\"{CodeGeneratorTool.FirstLowerCase(columnName)}\">\r\n";
                vueViewFromContent += $"           <el-input v-model{inputNumTxt}=\"form.{CodeGeneratorTool.FirstLowerCase(columnName)}\" placeholder=\"{placeHolder}\" {labelDisabled}/>\r\n";
                vueViewFromContent += "         </el-form-item>\r\n";
            }

            return vueViewFromContent;
        }

        /// <summary>
        /// 查询表单
        /// </summary>
        /// <param name="dbFieldInfo"></param>
        /// <returns></returns>
        public static string GetQueryFormHtml(GenTableColumn dbFieldInfo)
        {
            string queryFormHtml = "";
            string labelName = CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnComment, dbFieldInfo.ColumnName);
            if (!dbFieldInfo.IsQuery || dbFieldInfo.HtmlType == GenConstants.HTML_FILE_UPLOAD) return queryFormHtml;

            if (dbFieldInfo.HtmlType == GenConstants.HTML_DATETIME)
            {
                queryFormHtml += "<el-form-item label=\"时间\">\r\n";
                queryFormHtml += "    <el-date-picker v-model=\"dateRange\" size=\"small\" value-format=\"yyyy-MM-dd\" type=\"daterange\" range-separator=\"-\" start-placeholder=\"开始日期\" end-placeholder=\"结束日期\"></el-date-picker>\r\n";
                queryFormHtml += "</el-form-item>\r\n";
            }
            else
            {
                string inputNumTxt = TableMappingHelper.IsNumber(dbFieldInfo.CsharpType) ? ".number" : "";
                queryFormHtml += $"        <el-form-item label=\"{ labelName}\" :label-width=\"labelWidth\">\r\n";
                queryFormHtml += $"           <el-input v-model{inputNumTxt}=\"queryParams.{CodeGeneratorTool.FirstLowerCase(dbFieldInfo.CsharpField)}\"/>\r\n";
                queryFormHtml += "        </el-form-item>\r\n";
            }

            return queryFormHtml;
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
                vueViewListContent += $"      <el-table-column prop=\"{ columnName}\" label=\"图片\">\r\n";
                vueViewListContent += "         <template slot-scope=\"scope\">\r\n";
                vueViewListContent += $"            <el-image class=\"table-td-thumb\" :src=\"scope.row.{columnName}\" :preview-src-list=\"[scope.row.{columnName}]\"></el-image>\r\n";
                vueViewListContent += "         </template>\r\n";
                vueViewListContent += "       </el-table-column>\r\n";
            }
            //else if (dbFieldInfo.HtmlType.Equals(GenConstants.HTML_RADIO))
            //{
            //    vueViewListContent += $"        <el-table-column prop=\"{columnName}\" label=\"{label}\" width=\"120\" >\r\n";
            //    vueViewListContent += "          <template slot-scope=\"scope\">\r\n";
            //    vueViewListContent += $"            <el-tag :type=\"scope.row.{columnName} === true ? 'success' : 'info'\"  disable-transitions >";
            //    vueViewListContent += $"                {{scope.row.{columnName}===true?'启用':'禁用'}} </el-tag>\r\n";
            //    vueViewListContent += "          </template>\r\n";
            //    vueViewListContent += "        </el-table-column>\r\n";
            //}
            else
            {
                vueViewListContent += $"      <el-table-column prop=\"{columnName}\" label=\"{label}\" align=\"center\" width=\"100\" {showToolTip} />\r\n";
            }
            return vueViewListContent;
        }
    }
}
