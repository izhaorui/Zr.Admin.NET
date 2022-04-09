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
            }
            else if (!dbFieldInfo.IsInsert && !dbFieldInfo.IsEdit)
            {
                sb.AppendLine("    <el-col :lg=\"12\" v-if=\"opertype == 2\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\">{{{{form.{columnName}}}}}</el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else if (genTable.TplCategory.Equals("tree", StringComparison.OrdinalIgnoreCase) && genTable.TreeParentCode != null && dbFieldInfo.CsharpField.Equals(genTable.TreeParentCode))
            {
                //树
                sb.AppendLine(@"    <el-col :lg=""24"">");
                sb.AppendLine($@"      <el-form-item label=""父级id"" prop=""{columnName}"">");
                sb.AppendLine($@"        <treeselect v-model=""form.{columnName}"" :options=""dataList"" :normalizer=""normalizer"" :show-count=""true"" placeholder=""选择上级菜单"" />");
                sb.AppendLine(@"      </el-form-item>");
                sb.AppendLine(@"    </el-col>");
            }
            //主键、非自增要插入，不能编辑
            else if (dbFieldInfo.IsPk || dbFieldInfo.IsIncrement)
            {
                sb.AppendLine("    <el-col :lg=\"12\">");
                sb.AppendLine($"      <el-form-item label=\"{labelName}\" prop=\"{columnName}\">");
                //主键非自增 显示input
                if (!dbFieldInfo.IsIncrement)
                {
                    sb.AppendLine($"        <el-input-number v-model.number=\"form.{columnName}\" controls-position=\"right\" placeholder=\"请输入{labelName}\" :disabled=\"title=='修改数据'\"/>");
                }
                else if (dbFieldInfo.IsIncrement)  //只有是 自增 就显示label
                {
                    sb.AppendLine($"        <span v-html=\"form.{columnName}\"/>");
                }

                sb.AppendLine("      </el-form-item>");
                sb.AppendLine("    </el-col>");
            }
            else
            {
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
            }
            return sb.ToString();
        }

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

    }
}
