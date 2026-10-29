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
        /// 判断 targetPath 是否位于 rootPath 目录之内（含 rootPath 自身）。
        /// 用于阻断 ../ 与绝对路径造成的越界读写（上传落盘、文件下载等入口都要过一遍）。
        /// </summary>
        public static bool IsPathUnder(string rootPath, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(targetPath))
            {
                return false;
            }

            try
            {
                var root = Path.GetFullPath(rootPath);
                var target = Path.GetFullPath(targetPath);
                var relative = Path.GetRelativePath(root, target);

                return !Path.IsPathRooted(relative)
                    && relative != ".."
                    && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                // 含非法字符等无法解析的路径，一律按越界处理
                return false;
            }
        }

        /// <summary>
        /// 清洗用户可控的相对目录（如上传接口的 fileDir）：统一成 / 分隔、去掉盘符与根、
        /// 丢弃 . 与 .. 段、剔除非法字符，只返回相对路径。
        /// 调用方解析成绝对路径后仍需用 <see cref="IsPathUnder"/> 兜底校验。
        /// </summary>
        public static string SanitizeRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var safeSegments = new List<string>();
            foreach (var segment in segments)
            {
                var safe = SanitizeFileName(segment);
                if (!string.IsNullOrEmpty(safe))
                {
                    safeSegments.Add(safe);
                }
            }

            return string.Join("/", safeSegments);
        }

        /// <summary>
        /// 清洗用户可控的文件名/单个目录名：只取最后一段，去掉分隔符、非法字符以及首尾的点与空白。
        /// 返回空串表示该名字不可用（调用方应回退成随机名）。
        /// </summary>
        public static string SanitizeFileName(string name, int maxLength = 100)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            // Windows 下 \ 和 / 都是分隔符，Linux 下 \ 只是普通字符，这里两种都按分隔符切、只保留最后一段，
            // 阻断 ..\..\appsettings、/etc/passwd 这类跨目录写法。
            var segments = name.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return string.Empty;
            }

            var lastSegment = segments[segments.Length - 1];
            var invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(lastSegment.Length);
            foreach (var ch in lastSegment)
            {
                if (char.IsControl(ch) || Array.IndexOf(invalidChars, ch) >= 0)
                {
                    continue;
                }
                builder.Append(ch);
            }

            // 首尾的点与空格在 Windows 上会被系统截断，也可能拼出 . / .. 这类特殊名
            var safe = builder.ToString().Trim().Trim('.').Trim();
            if (safe.Length > maxLength)
            {
                safe = safe.Substring(0, maxLength);
            }

            return safe;
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
