using System;
using System.Linq;
using System.Text;
using ZR.CodeGenerator.Model;
using ZR.Model.System.Generate;

namespace ZR.CodeGenerator
{
    /// <summary>
    /// 代码生成模板
    /// </summary>
    public class CodeGenerateTemplate
    {
        /// <summary>
        /// 查询Dto属性
        /// </summary>
        /// <param name="tbColumn"></param>
        /// <param name="replaceDto">替换字符对象</param>
        /// <returns></returns>
        public static void GetQueryDtoProperty(GenTableColumn tbColumn, ReplaceDto replaceDto)
        {
            if (tbColumn.IsQuery)
            {
                //字符串类型表达式
                if (tbColumn.CsharpType == GenConstants.TYPE_STRING)
                {
                    replaceDto.QueryCondition += $"            predicate = predicate.AndIF(!string.IsNullOrEmpty(parm.{tbColumn.CsharpField}), {QueryExp(tbColumn.CsharpField, tbColumn.QueryType)};\n";
                }
                //int类型表达式
                if (CodeGeneratorTool.IsNumber(tbColumn.CsharpType))
                {
                    replaceDto.QueryCondition += $"            predicate = predicate.AndIF(parm.{tbColumn.CsharpField} > 0, {QueryExp(tbColumn.CsharpField, tbColumn.QueryType)};\n";
                }
                //时间类型
                if (tbColumn.CsharpType == GenConstants.TYPE_DATE)
                {
                    replaceDto.QueryCondition += $"            predicate = predicate.AndIF(parm.BeginTime != null, it => it.{tbColumn.CsharpField} >= parm.BeginTime);\n";
                    replaceDto.QueryCondition += $"            predicate = predicate.AndIF(parm.EndTime != null, it => it.{tbColumn.CsharpField} <= parm.EndTime);\n";
                }
            }
        }

        #region vue 模板

        /// <summary>
        /// 生成vuejs模板，目前只有上传文件方法
        /// </summary>
        /// <param name="dbFieldInfo"></param>
        /// <param name="replaceDto"></param>
        /// <returns></returns>
        public static void TplVueJsMethod(GenTableColumn dbFieldInfo, ReplaceDto replaceDto)
        {
            string columnName = dbFieldInfo.ColumnName;
            var sb = new StringBuilder();

            if (dbFieldInfo.HtmlType.Equals(GenConstants.HTML_IMAGE_UPLOAD))
            {
                sb.AppendLine($"    //文件上传成功方法");
                sb.AppendLine($"    handleUpload{dbFieldInfo.CsharpField}Success(res, file) {{");
                sb.AppendLine($"      this.form.{columnName} = res.data;");
                sb.AppendLine($"      // this.form.{columnName} = URL.createObjectURL(file.raw);");
                sb.AppendLine($"      // this.$refs.upload.clearFiles();");
                sb.AppendLine($"    }},");
                replaceDto.VueBeforeUpload = TplJsBeforeUpload();
                replaceDto.VueUploadUrl = TplJsUploadUrl();
            }
            //有下拉框选项初列表查询数据
            if ((dbFieldInfo.HtmlType == GenConstants.HTML_SELECT || dbFieldInfo.HtmlType == GenConstants.HTML_RADIO) && !string.IsNullOrEmpty(dbFieldInfo.DictType))
            {
                sb.AppendLine(@$"    // {dbFieldInfo.ColumnComment}字典翻译");
                sb.AppendLine($"    {columnName}Format(row, column) {{");
                sb.AppendLine(@$"      return this.selectDictLabel(this.{columnName}Options, row.{columnName});");
                sb.AppendLine(@"    },");
            }
            replaceDto.VueJsMethod += sb.ToString();
        }

        /// <summary>
        /// Vue rules
        /// </summary>
        /// <param name="dbFieldInfo"></param>
        /// <returns></returns>
        public static string TplFormRules(GenTableColumn dbFieldInfo)
        {
            StringBuilder sbRule = new StringBuilder();
            //Rule 规则验证
            if (!dbFieldInfo.IsPk && !dbFieldInfo.IsIncrement && dbFieldInfo.IsRequired)
            {
                sbRule.AppendLine($"        {dbFieldInfo.ColumnName}: [{{ required: true, message: '请输入{dbFieldInfo.ColumnComment}', trigger: \"blur\"}}],");
            }
            else if (CodeGeneratorTool.IsNumber(dbFieldInfo.ColumnType) && dbFieldInfo.IsRequired)
            {
                sbRule.AppendLine($"        {dbFieldInfo.ColumnName}: [{{ type: 'number', message: '{dbFieldInfo.ColumnName}必须为数字值', trigger: \"blur\"}}],");
            }
            return sbRule.ToString();
        }

        /// <summary>
        /// Vue 添加修改表单
        /// </summary>
        /// <param name="dbFieldInfo"></param>
        /// <returns></returns>
        public static string TplVueFormContent(GenTableColumn dbFieldInfo)
        {
            string columnName = dbFieldInfo.ColumnName;
            string labelName = CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnComment, columnName);
            string labelDisabled = dbFieldInfo.IsPk ? ":disabled=\"true\"" : "";
            string placeHolder = dbFieldInfo.IsIncrement ? "" : $"请输入{labelName}";
            StringBuilder sb = new StringBuilder();
            if (GenConstants.inputDtoNoField.Any(f => f.ToLower().Contains(dbFieldInfo.CsharpField.ToLower())))
            {
                return sb.ToString();
            }
            if (!dbFieldInfo.IsInsert || !dbFieldInfo.IsEdit)
            {
                return sb.ToString();
            }
            if (dbFieldInfo.HtmlType == GenConstants.HTML_INPUT_NUMBER)
            {
                sb.AppendLine("    <el-col :span=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{CodeGeneratorTool.FirstLowerCase(columnName)}\">");
                sb.AppendLine($"        <el-input-number v-model.number=\"form.{CodeGeneratorTool.FirstLowerCase(columnName)}\" placeholder=\"{placeHolder}\" {labelDisabled}/>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_DATETIME)
            {
                //时间
                sb.AppendLine("      <el-col :span=\"12\">");
                sb.AppendLine($"        <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">");
                sb.AppendLine($"           <el-date-picker v-model=\"form.{columnName}\" format=\"yyyy-MM-dd HH:mm:ss\" value-format=\"yyyy-MM-dd HH:mm:ss\"  type=\"datetime\"  placeholder=\"选择日期时间\"> </el-date-picker>");
                sb.AppendLine("         </el-form-item>");
                sb.AppendLine("     </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_IMAGE_UPLOAD)
            {
                //图片
                sb.AppendLine("    <el-col :span=\"24\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-upload class=\"avatar-uploader\" name=\"file\" :action=\"uploadUrl\" :show-file-list=\"false\" :on-success=\"handleUpload{dbFieldInfo.CsharpField}Success\" :before-upload=\"beforeFileUpload\">");
                sb.AppendLine($"          <el-image v-if=\"form.{columnName}\" :src=\"form.{columnName}\" class=\"icon\"/>");
                sb.AppendLine("          <i v-else class=\"el-icon-plus uploader-icon\"></i>");
                sb.AppendLine("        </el-upload>");
                sb.AppendLine($"        <el-input v-model=\"form.{columnName}\" placeholder=\"请上传文件或手动输入文件地址\"></el-input>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_RADIO && !string.IsNullOrEmpty(dbFieldInfo.DictType))
            {
                string value = CodeGeneratorTool.IsNumber(dbFieldInfo.CsharpType) ? "parseInt(item.dictValue)" : "item.dictValue";
                sb.AppendLine("    <el-col :span=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-radio-group v-model=\"form.{columnName}\">");
                sb.AppendLine($"          <el-radio v-for=\"item in {columnName}Options\" :key=\"item.dictValue\" :label=\"{value}\">{{{{item.dictLabel}}}}</el-radio>");
                sb.AppendLine("        </el-radio-group>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_RADIO)
            {
                sb.AppendLine("    <el-col :span=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-radio-group v-model=\"form.{columnName}\">");
                sb.AppendLine("           <el-radio :key=\"1\" :label=\"1\">是</el-radio>");
                sb.AppendLine("           <el-radio :key=\"0\" :label=\"0\">否</el-radio>");
                sb.AppendLine("        </el-radio-group>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_TEXTAREA)
            {
                sb.AppendLine("    <el-col :span=\"24\">");
                sb.AppendLine($"      <el-form-item label=\"{ labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-input type=\"textarea\" v-model=\"form.{columnName}\" placeholder=\"请输入内容\"/>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_EDITOR)
            {
                sb.AppendLine("    <el-col :span=\"24\">");
                sb.AppendLine($"      <el-form-item label=\"{ labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">");
                sb.AppendLine($"        <editor v-model=\"form.{columnName}\" :min-height=\"200\" />");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_SELECT && !string.IsNullOrEmpty(dbFieldInfo.DictType))
            {
                string value = CodeGeneratorTool.IsNumber(dbFieldInfo.CsharpType) ? "parseInt(item.dictValue)" : "item.dictValue";
                sb.AppendLine("    <el-col :span=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{ labelName}\" :label-width=\"labelWidth\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-select v-model=\"form.{columnName}\">");
                sb.AppendLine($"          <el-option v-for=\"item in {columnName}Options\" :key=\"item.dictValue\" :label=\"item.dictLabel\" :value=\"{value}\"></el-option>");
                sb.AppendLine("        </el-select>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else
            {
                string inputNumTxt = CodeGeneratorTool.IsNumber(dbFieldInfo.CsharpType) ? ".number" : "";
                sb.AppendLine("    <el-col :span=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" :label-width=\"labelWidth\" prop=\"{CodeGeneratorTool.FirstLowerCase(columnName)}\">");
                sb.AppendLine($"        <el-input v-model{inputNumTxt}=\"form.{CodeGeneratorTool.FirstLowerCase(columnName)}\" placeholder=\"{placeHolder}\" {labelDisabled}/>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Vue 查询表单
        /// </summary>
        /// <param name="dbFieldInfo"></param>
        /// <returns></returns>
        public static string TplQueryFormHtml(GenTableColumn dbFieldInfo)
        {
            StringBuilder sb = new();
            string labelName = CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnComment, dbFieldInfo.ColumnName);
            if (!dbFieldInfo.IsQuery || dbFieldInfo.HtmlType == GenConstants.HTML_FILE_UPLOAD) return sb.ToString();
            if (dbFieldInfo.HtmlType == GenConstants.HTML_DATETIME)
            {
                sb.AppendLine("      <el-form-item label=\"时间\">");
                sb.AppendLine("        <el-date-picker v-model=\"timeRange\" size=\"small\" value-format=\"yyyy-MM-dd\" type=\"daterange\" range-separator=\"-\" start-placeholder=\"开始日期\"");
                sb.AppendLine("          end-placeholder=\"结束日期\"></el-date-picker>");
                sb.AppendLine("      </el-form-item>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_SELECT && !string.IsNullOrEmpty(dbFieldInfo.DictType))
            {
                //string value = CodeGeneratorTool.IsNumber(dbFieldInfo.CsharpType) ? "parseInt(item.dictValue)" : "item.dictValue";
                sb.AppendLine($"      <el-form-item label=\"{ labelName}\" :label-width=\"labelWidth\" prop=\"{dbFieldInfo.ColumnName}\">");
                sb.AppendLine($"        <el-select v-model=\"queryParams.{dbFieldInfo.ColumnName}\">");
                sb.AppendLine($"          <el-option v-for=\"item in {dbFieldInfo.ColumnName}Options\" :key=\"item.dictValue\" :label=\"item.dictLabel\" :value=\"item.dictValue\"></el-option>");
                sb.AppendLine("        </el-select>");
                sb.AppendLine("      </el-form-item>");
            }
            else
            {
                string inputNumTxt = CodeGeneratorTool.IsNumber(dbFieldInfo.CsharpType) ? ".number" : "";
                sb.AppendLine($"      <el-form-item label=\"{ labelName}\" :label-width=\"labelWidth\">");
                sb.AppendLine($"        <el-input v-model{inputNumTxt}=\"queryParams.{CodeGeneratorTool.FirstLowerCase(dbFieldInfo.CsharpField)}\" />");
                sb.AppendLine("      </el-form-item>");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Vue 查询列表
        /// </summary>
        /// <param name="dbFieldInfo"></param>
        /// <returns></returns>
        public static string TplTableColumn(GenTableColumn dbFieldInfo)
        {
            string columnName = dbFieldInfo.ColumnName;
            string label = CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnComment, columnName);
            string showToolTip = dbFieldInfo.CsharpType == "string" ? ":show-overflow-tooltip=\"true\"" : "";
            string formatter = !string.IsNullOrEmpty(dbFieldInfo.DictType) ? $" :formatter=\"{columnName}Format\"" : "";
            StringBuilder sb = new StringBuilder();
            if (dbFieldInfo.IsList && dbFieldInfo.HtmlType.Equals(GenConstants.HTML_IMAGE_UPLOAD))
            {
                sb.AppendLine($"      <el-table-column prop=\"{columnName}\" label=\"图片\">");
                sb.AppendLine("         <template slot-scope=\"scope\">");
                sb.AppendLine($"            <el-image class=\"table-td-thumb\" :src=\"scope.row.{columnName}\" :preview-src-list=\"[scope.row.{columnName}]\"></el-image>");
                sb.AppendLine("         </template>");
                sb.AppendLine("       </el-table-column>");
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
            else if (dbFieldInfo.IsList)
            {
                sb.AppendLine($"      <el-table-column prop=\"{columnName}\" label=\"{label}\" align=\"center\" {showToolTip}{formatter}/>");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 文件上传前方法判断
        /// </summary>
        /// <returns></returns>
        public static string TplJsBeforeUpload()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(@"    //文件上传前判断方法");
            sb.AppendLine(@"    beforeFileUpload(file) {");
            sb.AppendLine(@"      const isJPG = file.type === ""image/jpeg"";");
            sb.AppendLine(@"      const isLt2M = file.size / 1024 / 1024 < 2;");
            sb.AppendLine(@"      if (!isJPG) {");
            sb.AppendLine(@"        this.msgError(""上传图片只能是 JPG 格式!"");");
            sb.AppendLine(@"      }");
            sb.AppendLine(@"      if (!isLt2M) {");
            sb.AppendLine(@"        this.msgError(""上传图片大小不能超过 2MB!"");");
            sb.AppendLine(@"      }");
            sb.AppendLine(@"      return isJPG && isLt2M;");
            sb.AppendLine(@"    },");
            
            return sb.ToString();
        }
        public static string TplJsUploadUrl()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(@"    //文件上传前判断方法");
            sb.AppendLine(@"    uploadUrl: process.env.VUE_APP_BASE_API + ""upload/SaveFile"",");

            return sb.ToString();
        }
        #endregion

        public static string QueryExp(string propertyName, string queryType)
        {
            if (queryType.Equals("EQ"))
            {
                return $"m => m.{ propertyName} == parm.{propertyName})";
            }
            if (queryType.Equals("GTE"))
            {
                return $"m => m.{ propertyName} >= parm.{propertyName})";
            }
            if (queryType.Equals("GT"))
            {
                return $"m => m.{ propertyName} > parm.{propertyName})";
            }
            if (queryType.Equals("LT"))
            {
                return $"m => m.{ propertyName} < parm.{propertyName})";
            }
            if (queryType.Equals("LTE"))
            {
                return $"m => m.{ propertyName} <= parm.{propertyName})";
            }
            if (queryType.Equals("NE"))
            {
                return $"m => m.{ propertyName} != parm.{propertyName})";
            }
            if (queryType.Equals("LIKE"))
            {
                return $"m => m.{ propertyName}.Contains(parm.{propertyName}))";
            }
            return "";
        }
    }
}
