using Infrastructure.Attribute;
using Microsoft.AspNetCore.Http;
using System.IO;
using ZR.Service.System.IService;
using ZR.Common;
using Infrastructure;
using System;
using System.Text;
using System.Security.Cryptography;
using System.Net;
using ZR.Model.System;
using ZR.Repository.System;
using Infrastructure.Extensions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace ZR.Service.System
{
    /// <summary>
    /// 文件管理
    /// </summary>
    [AppService(ServiceType = typeof(ISysFileService), ServiceLifetime = LifeTime.Transient)]
    public class SysFileService : BaseService<SysFile>, ISysFileService
    {
        private string domainUrl = AppSettings.GetConfig("ALIYUN_OSS:domainUrl");
        private readonly SysFileRepository SysFileRepository;
        private OptionsSetting OptionsSetting;
        public SysFileService(SysFileRepository repository, IOptions<OptionsSetting> options)
        {
            SysFileRepository = repository;
            OptionsSetting = options.Value;
        }

        /// <summary>
        /// 存储本地
        /// </summary>
        /// <returns></returns>
        public async Task<SysFile> SaveFileLocal(string rootPath, string fileName, string fileDir, string userName, IFormFile formFile)
        {
            string fileExt = Path.GetExtension(formFile.FileName);
            string hashFileName = FileUtil.HashFileName();
            fileName = (fileName.IsEmpty() ? hashFileName : fileName) + fileExt;
            fileDir = fileDir.IsEmpty() ? "uploads" : fileDir;
            string filePath = FileUtil.GetdirPath(fileDir);
            string finalFilePath = Path.Combine(rootPath, filePath, fileName);
            double fileSize = Math.Round(formFile.Length / 1024.0, 2);

            if (!Directory.Exists(Path.GetDirectoryName(finalFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(finalFilePath));
            }

            using (var stream = new FileStream(finalFilePath, FileMode.Create))
            {
                await formFile.CopyToAsync(stream);
            }
            string accessPath = string.Concat(OptionsSetting.Upload.UploadUrl, "/", filePath.Replace("\\", "/"), "/", fileName);
            SysFile file = new(formFile.FileName, fileName, fileExt, fileSize + "kb", filePath, accessPath, userName)
            {
                StoreType = (int)Infrastructure.Enums.StoreType.LOCAL,
                FileType = formFile.ContentType,
                FileUrl = finalFilePath
            };
            file.Id = await InsertFile(file);
            return file;
        }

        /// <summary>
        /// 上传文件到阿里云
        /// </summary>
        /// <param name="picdir">文件夹</param>
        /// <param name="formFile"></param>
        /// <param name="customFileName">自定义文件名</param>
        /// <param name="bucketName">存储桶</param>
        /// <returns></returns>
        public (bool, string, string) SaveFile(string picdir, IFormFile formFile, string customFileName, string bucketName)
        {
            // eg: uploads/2020/08/18
            //string dir = GetdirPath(picdir.ToString());

            string tempName = customFileName.IsEmpty() ? HashFileName() : customFileName;
            string fileExt = Path.GetExtension(formFile.FileName);
            string fileName = tempName + fileExt;
            string webUrl = string.Concat(domainUrl, "/", picdir, "/", fileName);

            HttpStatusCode statusCode = AliyunOssHelper.PutObjectFromFile(formFile.OpenReadStream(), Path.Combine(picdir, fileName), bucketName);

            return (statusCode == HttpStatusCode.OK, webUrl, fileName);
        }
        public string GetdirPath(string path = "")
        {
            DateTime date = DateTime.Now;
            int year = date.Year;
            int month = date.Month;
            int day = date.Day;
            //int hour = date.Hour;

            string timeDir = $"{year}/{month}/{day}";// date.ToString("yyyyMM/dd/HH/");
            if (!string.IsNullOrEmpty(path))
            {
                timeDir = path + "/" + timeDir;
            }
            return timeDir;
        }

        public string HashFileName(string str = null)
        {
            if (string.IsNullOrEmpty(str))
            {
                str = Guid.NewGuid().ToString();
            }
            MD5CryptoServiceProvider md5 = new();
            return BitConverter.ToString(md5.ComputeHash(Encoding.Default.GetBytes(str)), 4, 8).Replace("-", "");
        }

        public Task<long> InsertFile(SysFile file)
        {
            try
            {
                return Insertable(file).ExecuteReturnSnowflakeIdAsync();//单条插入返回雪花ID;
            }
            catch (Exception ex)
            {
                Console.WriteLine("存储图片失败" + ex.Message);
                throw new Exception(ex.Message);
            }
        }
    }
}
