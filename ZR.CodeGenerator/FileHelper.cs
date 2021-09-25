using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ZR.CodeGenerator.Model;

namespace ZR.CodeGenerator
{
    public class FileHelper
    {
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
        /// <param name="content"></param>
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
            Console.WriteLine("写入文件：" + path);
            try
            {
                //实例化一个文件流--->与写入文件相关联
                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
                //实例化一个StreamWriter-->与fs相关联
                using var sw = new StreamWriter(fs);
                //开始写入
                sw.Write(content);
                //清空缓冲区
                sw.Flush();
                //关闭流
                sw.Close();
                fs.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("写入文件出错了:" + ex.Message);
            }
        }


        /// <summary>
        /// 从代码模板中读取内容
        /// </summary>
        /// <param name="templateName">模板名称，应包括文件扩展名称。比如：template.txt</param>
        /// <returns></returns>
        public static string ReadTemplate(string templateName)
        {
            string path = Environment.CurrentDirectory;
            string fullName = $"{path}/wwwroot/CodeGenTemplate/{templateName}";

            Console.WriteLine("开始读取模板=" + fullName);
            string temp = fullName;
            string str = "";
            if (!File.Exists(temp))
            {
                return str;
            }
            StreamReader sr = null;
            try
            {
                sr = new StreamReader(temp);
                str = sr.ReadToEnd(); // 读取文件
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取模板出错了{ex.Message}");
            }
            sr?.Close();
            sr?.Dispose();
            return str;
        }


        /// <summary>
        /// 压缩代码
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        public static string ZipGenCode(GenerateDto dto)
        {
            try
            {
                //生成压缩包
                string zipReturnFileName = dto.GenTable.BaseNameSpace + DateTime.Now.ToString("yyyyMMddHHmmss") + ".zip";

                CreateDirectory(dto.GenCodePath);
                string zipFileName = Path.Combine(dto.ZipPath, zipReturnFileName);
                if (File.Exists(zipFileName))
                {
                    File.Delete(zipFileName);
                }

                ZipFile.CreateFromDirectory(dto.GenCodePath, zipFileName);
                DeleteDirectory(dto.GenCodePath);
                dto.ZipFileName = zipReturnFileName;
                return zipReturnFileName;
            }
            catch (Exception ex)
            {
                Console.WriteLine("压缩文件出错。" + ex.Message);
                return "";
            }
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
                //Log4NetHelper.Error("代码生成异常", ex);
            }
        }

    }
}
