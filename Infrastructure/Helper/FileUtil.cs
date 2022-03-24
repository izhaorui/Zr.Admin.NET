using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure
{
    public class FileUtil
    {
        /// <summary>
        /// 按时间来创建文件夹
        /// </summary>
        /// <param name="path"></param>
        /// <returns>eg: /{yourPath}/2020/11/3/</returns>
        public static string GetdirPath(string path = "")
        {
            DateTime date = DateTime.Now;
            string timeDir = date.ToString("yyyyMMdd");// date.ToString("yyyyMM/dd/HH/");

            if (!string.IsNullOrEmpty(path))
            {
                timeDir = Path.Combine(path, timeDir);
            }
            return timeDir;
        }

        /// <summary>
        /// 取文件名的MD5值(16位)
        /// </summary>
        /// <param name="str">文件名，不包括扩展名</param>
        /// <returns></returns>
        public static string HashFileName(string str = null)
        {
            if (string.IsNullOrEmpty(str))
            {
                str = Guid.NewGuid().ToString();
            }
            MD5 md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(Encoding.Default.GetBytes(str)), 4, 8).Replace("-", "");
        }
    }
}
