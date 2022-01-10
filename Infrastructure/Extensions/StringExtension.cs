using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Infrastructure.Extensions
{
    public static class StringExtension
    {

        /// <summary>
        /// SQL条件拼接
        /// </summary>
        /// <param name="str"></param>
        /// <param name="condition"></param>
        /// <returns></returns>
        public static string If(this string str, bool condition)
        {
            return condition ? str : string.Empty;
        }
        /// <summary>
        /// 判断是否为空
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IfNotEmpty(this string str)
        {
            return !string.IsNullOrEmpty(str);
        }

        /// <summary>
        /// 注意：如果替换的旧值中有特殊符号，替换将会失败，解决办法 例如特殊符号是“(”：  要在调用本方法前加oldValue=oldValue.Replace("(","//(");
        /// </summary>
        /// <param name="input"></param>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        /// <returns></returns>
        public static string ReplaceFirst(this string input, string oldValue, string newValue)
        {
            Regex regEx = new Regex(oldValue, RegexOptions.Multiline);
            return regEx.Replace(input, newValue == null ? "" : newValue, 1);
        }

        /// <summary>
        /// 骆驼峰转下划线
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string ToSmallCamelCase(string name)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(name.Substring(0, 1).ToLower());

            for (var i = 0; i < name.Length; i++)
            {
                if (i == 0)
                {
                    stringBuilder.Append(name.Substring(0, 1).ToLower());
                }
                else
                {
                    if (name[i] >= 'A' && name[i] <= 'Z')
                    {
                        stringBuilder.Append($"_{name.Substring(i, 1).ToLower()}");
                    }
                    else
                    {
                        stringBuilder.Append(name[i]);
                    }
                }
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// 下划线命名转驼峰命名
        /// </summary>
        /// <param name="underscore"></param>
        /// <returns></returns>
        public static string UnderScoreToCamelCase(this string underscore)
        {
            string[] ss = underscore.Split("_");
            if (ss.Length == 1)
            {
                return underscore;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(ss[0]);
            for (int i = 1; i < ss.Length; i++)
            {
                sb.Append(ss[i].FirstUpperCase());
            }

            return sb.ToString();
        }

        /// <summary>
        /// 首字母转大写
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string FirstUpperCase(this string str)
        {
            return string.IsNullOrEmpty(str) ? str : str.Substring(0, 1).ToUpper() + str[1..];
        }

        /// <summary>
        /// 首字母转小写
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string FirstLowerCase(this string str)
        {
            return string.IsNullOrEmpty(str) ? str : str.Substring(0, 1).ToLower() + str[1..];
        }

        /// <summary>
        /// 截取指定字符串中间内容
        /// </summary>
        /// <param name="sourse"></param>
        /// <param name="startstr"></param>
        /// <param name="endstr"></param>
        /// <returns></returns>
        public static string SubstringBetween(this string sourse, string startstr, string endstr)
        {
            string result = string.Empty;
            int startindex, endindex;
            try
            {
                startindex = sourse.IndexOf(startstr);
                if (startindex == -1)
                    return result;
                string tmpstr = sourse.Substring(startindex + startstr.Length);
                endindex = tmpstr.IndexOf(endstr);
                if (endindex == -1)
                    return result;
                result = tmpstr.Remove(endindex);
            }
            catch (Exception ex)
            {
                Console.WriteLine("MidStrEx Err:" + ex.Message);
            }
            return result;
        }
    }
}
