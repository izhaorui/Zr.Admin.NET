using System;
using System.Collections.Generic;
using System.Text;

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
    }
}
