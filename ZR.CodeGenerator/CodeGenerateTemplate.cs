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
