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

namespace ZR.Service.System
{
    /// <summary>
    /// 文件管理
    /// </summary>
    [AppService(ServiceType = typeof(ISysFileService), ServiceLifetime = LifeTime.Transient)]
    public class SysFileService : ISysFileService
    {
        private string domainUrl = ConfigUtils.Instance.GetConfig("ALIYUN_OSS:domainUrl");

        /// <summary>
        /// 上传文件到阿里云
        /// </summary>
        /// <param name="picdir"></param>
        /// <param name="formFile"></param>
        /// <returns></returns>
        public (bool, string, string) SaveFile(string picdir, IFormFile formFile)
        {
            // eg: idcard/2020/08/18
            string dir = GetdirPath(picdir.ToString());
            string tempName = HashFileName();
            string fileExt = Path.GetExtension(formFile.FileName);
            string fileName = $"{tempName}{fileExt}";
            string webUrl = $"{domainUrl}/{dir}/{fileName}";

            HttpStatusCode statusCode = AliyunOssHelper.PutObjectFromFile(formFile.OpenReadStream(), Path.Combine(dir, fileName));

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

    }
}
