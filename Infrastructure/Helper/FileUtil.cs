using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
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

        /// <summary>
        /// 删除指定目录下的所有文件及文件夹(保留目录)
        /// </summary>
        /// <param name="file">文件目录</param>
        public static void DeleteDirectory(string file)
        {
            try
            {
                //判断文件夹是否还存在
                if (Directory.Exists(file))
                {
                    DirectoryInfo fileInfo = new DirectoryInfo(file);
                    //去除文件夹的只读属性
                    fileInfo.Attributes = FileAttributes.Normal & FileAttributes.Directory;
                    foreach (string f in Directory.GetFileSystemEntries(file))
                    {
                        if (File.Exists(f))
                        {
                            //去除文件的只读属性
                            File.SetAttributes(file, FileAttributes.Normal);
                            //如果有子文件删除文件
                            File.Delete(f);
                        }
                        else
                        {
                            //循环递归删除子文件夹
                            DeleteDirectory(f);
                        }
                    }
                    //删除空文件夹
                    Directory.Delete(file);
                }

            }
            catch (Exception ex) // 异常处理
            {
                Console.WriteLine("代码生成异常" + ex.Message);
            }
        }

        /// <summary>
        /// 压缩代码
        /// </summary>
        /// <param name="zipPath"></param>
        /// <param name="genCodePath"></param>
        /// <param name="zipFileName">压缩后的文件名</param>
        /// <returns></returns>
        public static bool ZipGenCode(string zipPath, string genCodePath, string zipFileName)
        {
            if (string.IsNullOrEmpty(zipPath)) return false;
            try
            {
                CreateDirectory(genCodePath);
                string zipFileFullName = Path.Combine(zipPath, zipFileName);
                if (File.Exists(zipFileFullName))
                {
                    File.Delete(zipFileFullName);
                }

                ZipFile.CreateFromDirectory(genCodePath, zipFileFullName);
                DeleteDirectory(genCodePath);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("压缩文件出错。" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 创建文件夹
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool CreateDirectory(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                path = path.Replace("\\", "/").Replace("//", "/");
            }
            try
            {
                if (!Directory.Exists(path))
                {
                    DirectoryInfo info = Directory.CreateDirectory(path);
                    Console.WriteLine("不存在创建文件夹" + info);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建文件夹出错了,{ex.Message}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 写文件
        /// </summary>
        /// <param name="path">完整路径带扩展名的</param>
        /// <param name="content">写入文件内容</param>
        public static void WriteAndSave(string path, string content)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                path = path.Replace("\\", "/").Replace("//", "/");
            }
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            Console.WriteLine("开始写入文件，Path=" + path);
            try
            {
                File.WriteAllText(path, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine("写入文件出错了:" + ex.Message);
            }
        }
    }
}
