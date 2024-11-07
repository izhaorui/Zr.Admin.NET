﻿using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ZR.Common;
using ZR.Model.System;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 文件管理
    /// </summary>
    [AppService(ServiceType = typeof(ISysFileService), ServiceLifetime = LifeTime.Transient)]
    public class SysFileService( IOptions<OptionsSetting> options) : BaseService<SysFile>, ISysFileService
    {
        private string domainUrl = AppSettings.GetConfig("ALIYUN_OSS:domainUrl");
        private OptionsSetting OptionsSetting = options.Value;

        /// <summary>
        /// 存储本地
        /// </summary>
        /// <param name="fileDir">存储文件夹</param>
        /// <param name="rootPath">存储根目录</param>
        /// <param name="fileName">自定文件名</param>
        /// <param name="formFile">上传的文件流</param>
        /// <param name="userName"></param>
        /// <returns></returns>
        public async Task<SysFile> SaveFileToLocal(string rootPath, string fileName, string fileDir, string userName, IFormFile formFile)
        {
            string fileExt = Path.GetExtension(formFile.FileName);
            fileName = (fileName.IsEmpty() ? HashFileName() : fileName) + fileExt;

            string filePath = GetdirPath(fileDir);
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
            string uploadUrl = OptionsSetting.Upload.UploadUrl;
            string accessPath = string.Concat(filePath.Replace("\\", "/"), "/", fileName);
            Uri baseUri = new(uploadUrl);
            Uri fullUrl = new(baseUri, accessPath);
            SysFile file = new(formFile.FileName, fileName, fileExt, fileSize + "kb", filePath, userName)
            {
                StoreType = (int)StoreType.LOCAL,
                FileType = formFile.ContentType,
                FileUrl = finalFilePath.Replace("\\", "/"),
                AccessUrl = fullUrl.AbsoluteUri
            };
            file.Id = await InsertFile(file);
            return file;
        }

        /// <summary>
        /// 上传文件到阿里云
        /// </summary>
        /// <param name="file"></param>
        /// <param name="formFile"></param>
        /// <returns></returns>
        public async Task<SysFile> SaveFileToAliyun(SysFile file, IFormFile formFile)
        {
            file.FileName = (file.FileName.IsEmpty() ? HashFileName() : file.FileName) + file.FileExt;
            file.StorePath = GetdirPath(file.StorePath);
            string finalPath = Path.Combine(file.StorePath, file.FileName);
            HttpStatusCode statusCode = AliyunOssHelper.PutObjectFromFile(formFile.OpenReadStream(), finalPath, "");
            if (statusCode != HttpStatusCode.OK) return file;

            file.StorePath = file.StorePath;
            file.FileUrl = finalPath;
            file.AccessUrl = string.Concat(domainUrl, "/", file.StorePath.Replace("\\", "/"), "/", file.FileName);
            file.Id = await InsertFile(file);

            return file;
        }

        /// <summary>
        /// 获取文件存储目录
        /// </summary>
        /// <param name="storePath"></param>
        /// <param name="byTimeStore">是否按年月日存储</param>
        /// <returns></returns>
        public string GetdirPath(string storePath = "", bool byTimeStore = true)
        {
            DateTime date = DateTime.Now;
            string timeDir = date.ToString("yyyy/MMdd");

            if (!string.IsNullOrEmpty(storePath))
            {
                timeDir = Path.Combine(storePath, timeDir);
            }
            return timeDir;
        }

        public string HashFileName(string str = null)
        {
            if (string.IsNullOrEmpty(str))
            {
                str = Guid.NewGuid().ToString().ToLower();
            }
            return BitConverter.ToString(MD5.HashData(Encoding.Default.GetBytes(str)), 4, 8).Replace("-", "");
        }

        public Task<long> InsertFile(SysFile file)
        {
            return Insertable(file).ExecuteReturnSnowflakeIdAsync();//单条插入返回雪花ID;
        }
    }
}
