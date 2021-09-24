using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZR.CodeGenerator
{
    public class FileHelper
    {
        /// <summary>
        /// 制作压缩包（多个文件压缩到一个压缩包，支持加密、注释）
        /// </summary>
        /// <param name="fileNames">要压缩的文件</param>
        /// <param name="topDirectoryName">压缩文件目录</param>
        /// <param name="zipedFileName">压缩包文件名</param>
        /// <param name="compresssionLevel">压缩级别 1-9</param>
        /// <param name="password">密码</param>
        /// <param name="comment">注释</param>
        public static void ZipFiles(string[] fileNames, string topDirectoryName, string zipedFileName, int? compresssionLevel, string password = "", string comment = "")
        {
            using (ZipOutputStream zos = new ZipOutputStream(File.Open(zipedFileName, FileMode.OpenOrCreate)))
            {
                if (compresssionLevel.HasValue)
                {
                    zos.SetLevel(compresssionLevel.Value);//设置压缩级别
                }

                if (!string.IsNullOrEmpty(password))
                {
                    zos.Password = password;//设置zip包加密密码
                }

                if (!string.IsNullOrEmpty(comment))
                {
                    zos.SetComment(comment);//设置zip包的注释
                }

                foreach (string file in fileNames)
                {
                    //string fileName = string.Format("{0}/{1}", topDirectoryName, file);
                    string fileName =  file;
                    
                    if (File.Exists(fileName))
                    {
                        FileInfo item = new FileInfo(fileName);
                        FileStream fs = File.OpenRead(item.FullName);
                        byte[] buffer = new byte[fs.Length];
                        fs.Read(buffer, 0, buffer.Length);

                        ZipEntry entry = new ZipEntry(item.Name);
                        zos.PutNextEntry(entry);
                        zos.Write(buffer, 0, buffer.Length);
                    }
                }
            }
        }

        /// <summary>
        /// 压缩多层目录
        /// </summary>
        /// <param name="topDirectoryName">压缩文件目录</param>
        /// <param name="zipedFileName">压缩包文件名</param>
        /// <param name="compresssionLevel">压缩级别 1-9 </param>
        /// <param name="password">密码</param>
        /// <param name="comment">注释</param>
        /// <param name="filetype">文件类型</param>
        public static void ZipFileDirectory(string topDirectoryName, string zipedFileName, int compresssionLevel, string password, string comment, string filetype)
        {
            using (System.IO.FileStream ZipFile = File.Open(zipedFileName, FileMode.OpenOrCreate))
            {
                using (ZipOutputStream zos = new ZipOutputStream(ZipFile))
                {
                    if (compresssionLevel != 0)
                    {
                        zos.SetLevel(compresssionLevel);//设置压缩级别
                    }
                    if (!string.IsNullOrEmpty(password))
                    {
                        zos.Password = password;//设置zip包加密密码
                    }
                    if (!string.IsNullOrEmpty(comment))
                    {
                        zos.SetComment(comment);//设置zip包的注释
                    }
                    ZipSetp(topDirectoryName, zos, "", filetype);
                }
            }
        }

        /// <summary>
        /// 递归遍历目录
        /// </summary>
        /// <param name="strDirectory">The directory.</param>
        /// <param name="s">The ZipOutputStream Object.</param>
        /// <param name="parentPath">The parent path.</param>
        private static void ZipSetp(string strDirectory, ZipOutputStream s, string parentPath, string filetype)
        {
            if (strDirectory[^1] != Path.DirectorySeparatorChar)
            {
                strDirectory += Path.DirectorySeparatorChar;
            }
            Console.WriteLine("strDirectory=" + strDirectory);
            Crc32 crc = new Crc32();

            string[] filenames = Directory.GetFileSystemEntries(strDirectory, filetype);
            foreach (string file in filenames)// 遍历所有的文件和目录
            {
                if (Directory.Exists(file))// 先当作目录处理如果存在这个目录就递归Copy该目录下面的文件
                {
                    string pPath = parentPath;
                    pPath += file[(file.LastIndexOf("/") + 1)..];
                    pPath += "/";
                    Console.WriteLine("递归路径" + pPath);
                    ZipSetp(file, s, pPath, filetype);
                }
                else // 否则直接压缩文件
                {
                    //打开压缩文件
                    using (FileStream fs = File.OpenRead(file))
                    {
                        byte[] buffer = new byte[fs.Length];
                        fs.Read(buffer, 0, buffer.Length);
                        string fileName = parentPath + file[(file.LastIndexOf("/") + 1)..];
                        ZipEntry entry = new ZipEntry(fileName);
                        entry.DateTime = DateTime.Now;
                        entry.Size = fs.Length;
                        fs.Close();
                        crc.Reset();
                        crc.Update(buffer);
                        entry.Crc = crc.Value;
                        s.PutNextEntry(entry);
                        s.Write(buffer, 0, buffer.Length);
                    }
                }
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
