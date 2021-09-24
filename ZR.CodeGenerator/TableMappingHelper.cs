using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Infrastructure.Extensions;
using ZR.Common.Extension;

namespace ZR.CodeGenerator.CodeGenerator
{
    public class TableMappingHelper
    {
        /// <summary>
        /// UserService转成userService
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string FirstLetterLowercase(string instanceName)
        {
            instanceName = instanceName.ParseToString();
            if (!instanceName.IsEmpty())
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(instanceName[0].ToString().ToLower() + instanceName.Substring(1));
                return sb.ToString();
            }
            else
            {
                return instanceName;
            }
        }

        /// <summary>
        /// sys_menu_authorize变成MenuAuthorize
        /// </summary>
        public static string GetClassNamePrefix(string tableName)
        {
            string[] arr = tableName.Split('_');
            if (arr.Length <= 0) return tableName;

            StringBuilder sb = new StringBuilder();
            for (int i = 1; i < arr.Length; i++)
            {
                sb.Append(arr[i][0].ToString().ToUpper() + arr[i].Substring(1));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 获取C# 类型
        /// </summary>
        /// <param name="sDatatype"></param>
        /// <returns></returns>
        public static string GetCSharpDatatype(string sDatatype)
        {
            sDatatype = sDatatype.ToLower();
            string sTempDatatype = sDatatype switch
            {
                "int" or "number" or "integer" or "smallint" => "int",
                "bigint" => "long",
                "tinyint" => "byte",
                "numeric" or "real" or "float" => "float",
                "decimal" or "numer(8,2)" => "decimal",
                "bit" => "bool",
                "date" or "datetime" or "datetime2" or "smalldatetime" => "DateTime",
                "money" or "smallmoney" => "double",
                _ => "string",
            };
            return sTempDatatype;
        }

        public static bool IsNumber(string tableDataType)
        {
            string[] arr = new string[] { "int", "long" };
            return arr.Any(f => f.Contains(GetCSharpDatatype(tableDataType)));
        }
    }
}
