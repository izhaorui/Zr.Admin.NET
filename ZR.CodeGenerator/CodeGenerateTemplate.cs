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
        #region vue 模板

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
            StringBuilder sb = new StringBuilder();
            string value = CodeGeneratorTool.IsNumber(dbFieldInfo.CsharpType) ? "parseInt(item.dictValue)" : "item.dictValue";

            if (GenConstants.inputDtoNoField.Any(f => f.ToLower().Contains(dbFieldInfo.CsharpField.ToLower())))
            {
                return sb.ToString();
            }
            if (!dbFieldInfo.IsInsert && !dbFieldInfo.IsEdit && !dbFieldInfo.IsPk)
            {
                return sb.ToString();
            }
            if (dbFieldInfo.HtmlType == GenConstants.HTML_INPUT_NUMBER)
            {
                //数字框
                sb.AppendLine("    <el-col :span=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-input-number v-model.number=\"form.{columnName}\" placeholder=\"请输入{labelName}\" {labelDisabled}/>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_DATETIME)
            {
                //时间
                sb.AppendLine("      <el-col :span=\"12\">");
                sb.AppendLine($"        <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"           <el-date-picker v-model=\"form.{columnName}\" format=\"yyyy-MM-dd HH:mm:ss\" value-format=\"yyyy-MM-dd HH:mm:ss\"  type=\"datetime\"  placeholder=\"选择日期时间\"> </el-date-picker>");
                sb.AppendLine("         </el-form-item>");
                sb.AppendLine("     </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_IMAGE_UPLOAD)
            {
                //图片
                sb.AppendLine("    <el-col :span=\"24\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($@"        <UploadImage v-model=""form.{columnName}"" column=""{columnName}"" @input=""handleUploadSuccess"" />");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_FILE_UPLOAD)
            {
                //文件
                sb.AppendLine("    <el-col :span=\"24\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($@"        <UploadFile v-model=""form.{columnName}"" column=""{columnName}"" @input=""handleUploadSuccess"" />");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_RADIO)
            {
                //单选按钮
                sb.AppendLine("    <el-col :span=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-radio-group v-model=\"form.{columnName}\">");
                if (string.IsNullOrEmpty(dbFieldInfo.DictType))
                {
                    sb.AppendLine("           <el-radio :label=\"1\">请选择字典生成</el-radio>");
                }
                sb.AppendLine($"          <el-radio v-for=\"item in {columnName}Options\" :key=\"item.dictValue\" :label=\"{value}\">{{{{item.dictLabel}}}}</el-radio>");
                sb.AppendLine("        </el-radio-group>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_TEXTAREA)
            {
                //文本域
                sb.AppendLine("    <el-col :span=\"24\">");
                sb.AppendLine($"      <el-form-item label=\"{ labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-input type=\"textarea\" v-model=\"form.{columnName}\" placeholder=\"请输入{labelName}\"/>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_EDITOR)
            {
                //编辑器
                sb.AppendLine("    <el-col :span=\"24\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"        <editor v-model=\"form.{columnName}\" :min-height=\"200\" />");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_SELECT)
            {
                //下拉框
                sb.AppendLine("    <el-col :span=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-select v-model=\"form.{columnName}\" placeholder=\"请选择{labelName}\"> ");
                if (string.IsNullOrEmpty(dbFieldInfo.DictType))
                {
                    sb.AppendLine($"          <el-option label=\"请选择字典生成\" value=\"\"></el-option>");
                }
                sb.AppendLine($"          <el-option v-for=\"item in {columnName}Options\" :key=\"item.dictValue\" :label=\"item.dictLabel\" :value=\"{value}\"></el-option>");
                sb.AppendLine("        </el-select>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if( dbFieldInfo.HtmlType == GenConstants.HTML_CHECKBOX)
            {
                //多选框
                sb.AppendLine("    <el-col :span=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-checkbox-group v-model=\"form.{columnName}Checked\"> ");
                if (string.IsNullOrEmpty(dbFieldInfo.DictType))
                {
                    sb.AppendLine($"          <el-checkbox>请选择字典生成</el-checkbox>");
                }
                sb.AppendLine($"          <el-checkbox v-for=\"item in {columnName}Options\" :key=\"item.dictValue\" :label=\"item.dictValue\">{{{{item.dictLabel}}}}</el-checkbox>");
                sb.AppendLine("        </el-checkbox-group>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else
            {
                string inputNumTxt = CodeGeneratorTool.IsNumber(dbFieldInfo.CsharpType) ? ".number" : "";
                sb.AppendLine("    <el-col :span=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-input v-model{inputNumTxt}=\"form.{columnName}\" placeholder=\"请输入{labelName}\" {labelDisabled}/>");
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
            if (!dbFieldInfo.IsQuery) return sb.ToString();
            if (dbFieldInfo.HtmlType == GenConstants.HTML_DATETIME)
            {
                sb.AppendLine($"      <el-form-item label=\"{labelName}\">");
                sb.AppendLine($"        <el-date-picker v-model=\"dateRange{dbFieldInfo.CsharpField}\" size=\"small\" value-format=\"yyyy-MM-dd\" type=\"daterange\" range-separator=\"-\" start-placeholder=\"开始日期\"");
                sb.AppendLine($"          end-placeholder=\"结束日期\" placeholder=\"请选择{dbFieldInfo.ColumnComment}\" ></el-date-picker>");
                sb.AppendLine("      </el-form-item>");
            }
            else if ((dbFieldInfo.HtmlType == GenConstants.HTML_SELECT || dbFieldInfo.HtmlType == GenConstants.HTML_RADIO))
            {
                //string value = CodeGeneratorTool.IsNumber(dbFieldInfo.CsharpType) ? "parseInt(item.dictValue)" : "item.dictValue";
                sb.AppendLine($"      <el-form-item label=\"{ labelName}\" prop=\"{dbFieldInfo.ColumnName}\">");
                sb.AppendLine($"        <el-select v-model=\"queryParams.{dbFieldInfo.ColumnName}\" placeholder=\"请选择{dbFieldInfo.ColumnComment}\" size=\"small\" >");
                sb.AppendLine($"          <el-option v-for=\"item in {dbFieldInfo.ColumnName}Options\" :key=\"item.dictValue\" :label=\"item.dictLabel\" :value=\"item.dictValue\"></el-option>");
                sb.AppendLine("        </el-select>");
                sb.AppendLine("      </el-form-item>");
            }
            else if(dbFieldInfo.IsQuery)
            {
                string inputNumTxt = CodeGeneratorTool.IsNumber(dbFieldInfo.CsharpType) ? ".number" : "";
                sb.AppendLine($"      <el-form-item label=\"{ labelName}\" prop=\"{dbFieldInfo.ColumnName}\">");
                sb.AppendLine($"        <el-input v-model{inputNumTxt}=\"queryParams.{dbFieldInfo.ColumnName}\" placeholder=\"请输入{dbFieldInfo.ColumnComment}\" size=\"small\"/>");
                sb.AppendLine("      </el-form-item>");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Vue 查询列表
        /// </summary>
        /// <param name="dbFieldInfo"></param>
        /// <param name="genTable"></param>
        /// <returns></returns>
        public static string TplTableColumn(GenTableColumn dbFieldInfo, GenTable genTable)
        {
            string columnName = dbFieldInfo.ColumnName;
            string label = CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnComment, columnName);
            string showToolTip = dbFieldInfo.CsharpType == "string" ? ":show-overflow-tooltip=\"true\"" : "";
            string formatter = GetFormatter(dbFieldInfo.HtmlType, columnName);
            StringBuilder sb = new StringBuilder();
            //自定义排序字段
            if (GenConstants.HTML_SORT.Equals(dbFieldInfo.HtmlType) && !dbFieldInfo.IsPk && CodeGeneratorTool.IsNumber(dbFieldInfo.CsharpType))
            {
                sb.AppendLine($@"      <el-table-column prop=""{columnName}"" label=""{label}"" width=""90"" sortable align=""center"">");
                sb.AppendLine(@"        <template slot-scope=""scope"">");
                sb.AppendLine($@"          <el-input size=""mini"" style=""width:50px"" controls-position=""no"" v-model.number=""scope.row.{columnName}"" @blur=""handleChangeSort(scope.row, scope.row.{columnName})"" v-if=""showEditSort"" />");
                sb.AppendLine($"          <span v-else>{{{{scope.row.{columnName}}}}}</span>");
                sb.AppendLine(@"        </template>");
                sb.AppendLine(@"      </el-table-column>");
            }
            else if (dbFieldInfo.IsList && dbFieldInfo.HtmlType.Equals(GenConstants.HTML_IMAGE_UPLOAD))
            {
                sb.AppendLine($"      <el-table-column prop=\"{columnName}\" label=\"{label}\">");
                sb.AppendLine("         <template slot-scope=\"scope\">");
                sb.AppendLine($"            <el-image class=\"table-td-thumb\" :src=\"scope.row.{columnName}\" :preview-src-list=\"[scope.row.{columnName}]\"></el-image>");
                sb.AppendLine("         </template>");
                sb.AppendLine("       </el-table-column>");
            }
            else if (dbFieldInfo.IsList && !string.IsNullOrEmpty(dbFieldInfo.DictType))
            {
                sb.AppendLine($@"      <el-table-column label=""{label}"" align=""center"" prop=""{columnName}"">");
                sb.AppendLine(@"        <template slot-scope=""scope"">");
                if (dbFieldInfo.HtmlType == GenConstants.HTML_CHECKBOX)
                {
                    sb.AppendLine($@"          <dict-tag :options=""{columnName}Options"" :value=""scope.row.{columnName} ? scope.row.{columnName}.split(',') : []""/>");
                }
                else
                {
                    sb.AppendLine($@"          <dict-tag :options=""{columnName}Options"" :value=""scope.row.{columnName}""/>");
                }
                sb.AppendLine(@"        </template>");
                sb.AppendLine(@"      </el-table-column>");
            }
            else if (dbFieldInfo.IsList)
            {
                sb.AppendLine($"      <el-table-column prop=\"{columnName}\" label=\"{label}\" align=\"center\" {showToolTip}{formatter}/>");
            }
            return sb.ToString();
        }

        #endregion
        //模板调用
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
        /// <summary>
        /// 格式化字典数据显示到table
        /// </summary>
        /// <param name="htmlType"></param>
        /// <param name="columnName"></param>
        /// <returns></returns>
        public static string GetFormatter(string htmlType, string columnName)
        {
            if (htmlType.Equals(GenConstants.HTML_CHECKBOX) || 
                htmlType.Equals(GenConstants.HTML_SELECT) || 
                htmlType.Equals(GenConstants.HTML_RADIO))
            {
                return $" :formatter=\"{columnName}Format\"";
            }
            return "";
        }
    }
}
