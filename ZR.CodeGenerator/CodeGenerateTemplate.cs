using System;
using System.Linq;
using System.Text;
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
        public static string TplVueFormContent(GenTableColumn dbFieldInfo, GenTable genTable)
        {
            string columnName = dbFieldInfo.CsharpFieldFl;
            string labelName = CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnComment, columnName);
            string labelDisabled = dbFieldInfo.IsPk ? ":disabled=\"true\"" : "";
            StringBuilder sb = new();
            string value = CodeGeneratorTool.IsNumber(dbFieldInfo.CsharpType) ? "parseInt(item.dictValue)" : "item.dictValue";

            if (GenConstants.inputDtoNoField.Any(f => f.Contains(dbFieldInfo.CsharpField, StringComparison.OrdinalIgnoreCase)))
            {
                return sb.ToString();
            }
            if (!dbFieldInfo.IsInsert && !dbFieldInfo.IsEdit)
            {
                sb.AppendLine("    <el-col :lg=\"12\" v-if=\"opertype == 2\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\">{{{{form.{columnName}}}}}</el-form-item>");
                sb.AppendLine("    </el-col>");
                return sb.ToString();
            }

            //树
            if (genTable.TplCategory.Equals("tree", StringComparison.OrdinalIgnoreCase) && genTable.TreeParentCode != null && dbFieldInfo.CsharpField.Equals(genTable.TreeParentCode))
            {
                sb.AppendLine(@"    <el-col :lg=""24"">");
                sb.AppendLine($@"      <el-form-item label=""父级id"" prop=""{columnName}"">");
                sb.AppendLine($@"        <treeselect v-model=""form.{columnName}"" :options=""dataList"" :normalizer=""normalizer"" :show-count=""true"" placeholder=""选择上级菜单"" />");
                sb.AppendLine(@"      </el-form-item>");
                sb.AppendLine(@"    </el-col>");
                return sb.ToString();
            }
            //主键、非自增要插入，不能编辑
            if (dbFieldInfo.IsPk || dbFieldInfo.IsIncrement)
            {
                sb.AppendLine("    <el-col :lg=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                //主键非自增 显示input
                if (dbFieldInfo.IsPk && !dbFieldInfo.IsIncrement)
                {
                    sb.AppendLine($"        <el-input-number v-model.number=\"form.{columnName}\" controls-position=\"right\" placeholder=\"请输入{labelName}\" :disabled=\"title=='修改数据'\"/>");
                }
                else if (dbFieldInfo.IsIncrement)  //只有是 自增 就显示label
                {
                    sb.AppendLine($"        <span v-html=\"form.{columnName}\"/>");
                }

                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
                return sb.ToString();
            }
            if (dbFieldInfo.HtmlType == GenConstants.HTML_INPUT_NUMBER)
            {
                //数字框
                sb.AppendLine("    <el-col :lg=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-input-number v-model.number=\"form.{columnName}\" controls-position=\"right\" placeholder=\"请输入{labelName}\" {labelDisabled}/>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_DATETIME)
            {
                //时间
                sb.AppendLine("      <el-col :lg=\"12\">");
                sb.AppendLine($"        <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"           <el-date-picker v-model=\"form.{columnName}\" format=\"yyyy-MM-dd HH:mm:ss\" value-format=\"yyyy-MM-dd HH:mm:ss\" type=\"datetime\" placeholder=\"选择日期时间\"> </el-date-picker>");
                sb.AppendLine("         </el-form-item>");
                sb.AppendLine("     </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_IMAGE_UPLOAD)
            {
                //图片
                sb.AppendLine("    <el-col :lg=\"24\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($@"        <UploadImage v-model=""form.{columnName}"" column=""{columnName}"" @input=""handleUploadSuccess"" />");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_FILE_UPLOAD)
            {
                //文件
                sb.AppendLine("    <el-col :lg=\"24\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($@"        <UploadFile v-model=""form.{columnName}"" column=""{columnName}"" @input=""handleUploadSuccess"" />");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_RADIO)
            {
                //单选按钮
                sb.AppendLine("    <el-col :lg=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-radio-group v-model=\"form.{columnName}\">");
                sb.AppendLine($"          <el-radio v-for=\"item in {columnName}Options\" :key=\"item.dictValue\" :label=\"{value}\">{{{{item.dictLabel}}}}</el-radio>");
                sb.AppendLine("        </el-radio-group>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_TEXTAREA)
            {
                //文本域
                sb.AppendLine("    <el-col :lg=\"24\">");
                sb.AppendLine($"      <el-form-item label=\"{ labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-input type=\"textarea\" v-model=\"form.{columnName}\" placeholder=\"请输入{labelName}\"/>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_EDITOR)
            {
                //编辑器
                sb.AppendLine("    <el-col :lg=\"24\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"        <editor v-model=\"form.{columnName}\" :min-height=\"200\" />");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_SELECT)
            {
                //下拉框
                sb.AppendLine("    <el-col :lg=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-select v-model=\"form.{columnName}\" placeholder=\"请选择{labelName}\"> ");
                sb.AppendLine($"          <el-option v-for=\"item in {columnName}Options\" :key=\"item.dictValue\" :label=\"item.dictLabel\" :value=\"{value}\"></el-option>");
                sb.AppendLine("        </el-select>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (dbFieldInfo.HtmlType == GenConstants.HTML_CHECKBOX)
            {
                //多选框
                sb.AppendLine("    <el-col :lg=\"24\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-checkbox-group v-model=\"form.{columnName}Checked\"> ");
                sb.AppendLine($"          <el-checkbox v-for=\"item in {columnName}Options\" :key=\"item.dictValue\" :label=\"item.dictValue\">{{{{item.dictLabel}}}}</el-checkbox>");
                sb.AppendLine("        </el-checkbox-group>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else
            {
                string inputNumTxt = CodeGeneratorTool.IsNumber(dbFieldInfo.CsharpType) ? ".number" : "";
                sb.AppendLine("    <el-col :lg=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                sb.AppendLine($"        <el-input v-model{inputNumTxt}=\"form.{columnName}\" placeholder=\"请输入{labelName}\" {labelDisabled}/>");
                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }

            return sb.ToString();
        }

        ///// <summary>
        ///// Vue 查询表单
        ///// </summary>
        ///// <param name="dbFieldInfo"></param>
        ///// <returns></returns>
        //public static string TplQueryFormHtml(GenTableColumn dbFieldInfo)
        //{
        //    StringBuilder sb = new();
        //    string columnName = dbFieldInfo.CsharpFieldFl;
        //    string labelName = CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnComment, dbFieldInfo.CsharpField);
        //    if (!dbFieldInfo.IsQuery) return sb.ToString();
        //    if (dbFieldInfo.HtmlType == GenConstants.HTML_DATETIME)
        //    {
        //        sb.AppendLine($"      <el-form-item label=\"{labelName}\">");
        //        sb.AppendLine($"        <el-date-picker v-model=\"dateRange{dbFieldInfo.CsharpField}\" style=\"width: 240px\" value-format=\"yyyy-MM-dd\" type=\"daterange\" range-separator=\"-\" start-placeholder=\"开始日期\"");
        //        sb.AppendLine($"          end-placeholder=\"结束日期\" placeholder=\"请选择{dbFieldInfo.ColumnComment}\" :picker-options=\"{{ firstDayOfWeek: 1}}\"></el-date-picker>");
        //        sb.AppendLine("      </el-form-item>");
        //    }
        //    else if (dbFieldInfo.HtmlType == GenConstants.HTML_SELECT || dbFieldInfo.HtmlType == GenConstants.HTML_RADIO)
        //    {
        //        sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
        //        sb.AppendLine($"        <el-select v-model=\"queryParams.{columnName}\" placeholder=\"请选择{dbFieldInfo.ColumnComment}\" >");
        //        sb.AppendLine($"          <el-option v-for=\"item in {columnName}Options\" :key=\"item.dictValue\" :label=\"item.dictLabel\" :value=\"item.dictValue\"></el-option>");
        //        sb.AppendLine("        </el-select>");
        //        sb.AppendLine("      </el-form-item>");
        //    }
        //    else
        //    {
        //        string inputNumTxt = CodeGeneratorTool.IsNumber(dbFieldInfo.CsharpType) ? ".number" : "";
        //        sb.AppendLine($"      <el-form-item label=\"{ labelName}\" prop=\"{columnName}\">");
        //        sb.AppendLine($"        <el-input v-model{inputNumTxt}=\"queryParams.{columnName}\" placeholder=\"请输入{dbFieldInfo.ColumnComment}\" />");
        //        sb.AppendLine("      </el-form-item>");
        //    }

        //    return sb.ToString();
        //}

        ///// <summary>
        ///// Vue 查询列表
        ///// </summary>
        ///// <param name="dbFieldInfo"></param>
        ///// <param name="genTable"></param>
        ///// <returns></returns>
        //public static string TplTableColumn(GenTableColumn dbFieldInfo, GenTable genTable)
        //{
        //    string columnName = dbFieldInfo.CsharpFieldFl;
        //    string label = CodeGeneratorTool.GetLabelName(dbFieldInfo.ColumnComment, columnName);
        //    string showToolTip = ShowToolTip(dbFieldInfo);
        //    string formatter = GetFormatter(dbFieldInfo.HtmlType, columnName);
        //    StringBuilder sb = new StringBuilder();
        //    //自定义排序字段
        //    if (GenConstants.HTML_CUSTOM_INPUT.Equals(dbFieldInfo.HtmlType) && !dbFieldInfo.IsPk)
        //    {
        //        sb.AppendLine($@"      <el-table-column prop=""{columnName}"" label=""{label}"" width=""90"" sortable align=""center"">");
        //        sb.AppendLine(@"        <template slot-scope=""scope"">");
        //        sb.AppendLine($@"          <span v-show=""editIndex != scope.$index"" @click=""editCurrRow(scope.$index,'rowkeY')"">{{{{scope.row.{columnName}}}}}</span>");
        //        sb.AppendLine(@"          <el-input :id=""scope.$index+'rowkeY'"" size=""mini"" v-show=""(editIndex == scope.$index)""");
        //        sb.AppendLine($@"            v-model=""scope.row.{columnName}"" @blur=""handleChangeSort(scope.row)""></el-input>");
        //        sb.AppendLine(@"        </template>");
        //        sb.AppendLine(@"      </el-table-column>");
        //    }
        //    else if (dbFieldInfo.IsList && dbFieldInfo.HtmlType.Equals(GenConstants.HTML_IMAGE_UPLOAD))
        //    {
        //        sb.AppendLine($"      <el-table-column prop=\"{columnName}\" align=\"center\" label=\"{label}\">");
        //        sb.AppendLine("         <template slot-scope=\"scope\">");
        //        sb.AppendLine($"            <el-image class=\"table-td-thumb\" fit=\"contain\" :src=\"scope.row.{columnName}\" :preview-src-list=\"[scope.row.{columnName}]\">");
        //        sb.AppendLine("              <div slot=\"error\"><i class=\"el-icon-document\" /></div>");
        //        sb.AppendLine("            </el-image>");
        //        sb.AppendLine("         </template>");
        //        sb.AppendLine("       </el-table-column>");
        //    }
        //    else if (dbFieldInfo.IsList && !string.IsNullOrEmpty(formatter))
        //    {
        //        sb.AppendLine($@"      <el-table-column label=""{label}"" align=""center"" prop=""{columnName}"">");
        //        sb.AppendLine(@"        <template slot-scope=""scope"">");
        //        string checkboxHtml = string.Empty;
        //        if (dbFieldInfo.HtmlType == GenConstants.HTML_CHECKBOX)
        //        {
        //            checkboxHtml = $" ? scope.row.{columnName}.split(',') : []";
        //        }
        //        sb.AppendLine($"          <dict-tag :options=\"{columnName}Options\" :value=\"scope.row.{columnName}{checkboxHtml}\"/>");
        //        sb.AppendLine(@"        </template>");
        //        sb.AppendLine(@"      </el-table-column>");
        //    }
        //    else if (dbFieldInfo.IsList)
        //    {
        //        sb.AppendLine($"      <el-table-column prop=\"{columnName}\" label=\"{label}\" align=\"center\"{showToolTip}{formatter}/>");
        //    }
        //    return sb.ToString();
        //}

        #endregion

        //模板调用
        public static string QueryExp(string propertyName, string queryType)
        {
            if (queryType.Equals("EQ"))
            {
                return $"it => it.{ propertyName} == parm.{propertyName})";
            }
            if (queryType.Equals("GTE"))
            {
                return $"it => it.{ propertyName} >= parm.{propertyName})";
            }
            if (queryType.Equals("GT"))
            {
                return $"it => it.{ propertyName} > parm.{propertyName})";
            }
            if (queryType.Equals("LT"))
            {
                return $"it => it.{ propertyName} < parm.{propertyName})";
            }
            if (queryType.Equals("LTE"))
            {
                return $"it => it.{ propertyName} <= parm.{propertyName})";
            }
            if (queryType.Equals("NE"))
            {
                return $"it => it.{ propertyName} != parm.{propertyName})";
            }
            if (queryType.Equals("LIKE"))
            {
                return $"it => it.{ propertyName}.Contains(parm.{propertyName}))";
            }
            return $"it => it.{ propertyName} == parm.{propertyName})";
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

        /// <summary>
        /// 超出隐藏
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public static string ShowToolTip(GenTableColumn column)
        {
            if (column.CsharpType.Equals("string") ||
                column.HtmlType.Equals(GenConstants.HTML_DATETIME))
            {
                return $" :show-overflow-tooltip=\"true\"";
            }
            return "";
        }
    }
}
