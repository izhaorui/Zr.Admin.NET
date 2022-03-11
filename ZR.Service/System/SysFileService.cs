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
using SqlSugar.DistributedSystem.Snowflake;

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

        public SysFileService(SysFileRepository repository) : base(repository)
        {
            SysFileRepository = repository;
        }

        /// <summary>
        /// 上传文件到阿里云
        /// </summary>
        /// <param name="picdir"></param>
        /// <param name="formFile"></param>
        /// <returns></returns>
        public (bool, string, string) SaveFile(string picdir, IFormFile formFile)
        {
            return SaveFile(picdir, formFile, "", "");
        }

        /// <summary>
        /// 存储文件
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
            MD5 md5 = MD5.Create();
            return BitConverter.ToString(md5.ComputeHash(Encoding.Default.GetBytes(str)), 4, 8).Replace("-", "");
        }

        public long InsertFile(SysFile file)
        {
            try
            {
                return Insertable(file).ExecuteReturnSnowflakeId();//单条插入返回雪花ID;
            }
            catch (Exception ex)
            {
                Console.WriteLine("存储图片失败" + ex.Message);
                throw new Exception(ex.Message);
            }
        }
    }
}
