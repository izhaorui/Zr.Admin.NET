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
        private readonly ISysConfigService SysConfigService;
        private OptionsSetting OptionsSetting;
        public SysFileService(ISysConfigService sysConfigService, IOptions<OptionsSetting> options)
        {
            SysConfigService = sysConfigService;
            OptionsSetting = options.Value;
        }

        /// <summary>
        /// 存储本地
        /// </summary>
        /// <returns></returns>
        public async Task<SysFile> SaveFileToLocal(string rootPath, string fileName, string fileDir, string userName, IFormFile formFile)
        {
            string fileExt = Path.GetExtension(formFile.FileName);
            fileName = (fileName.IsEmpty() ? HashFileName() : fileName) + fileExt;
            fileDir = fileDir.IsEmpty() ? "uploads" : fileDir;
            string filePath = GetdirPath(fileDir);
            string finalFilePath = Path.Combine(rootPath, filePath, fileName);
            double fileSize = Math.Round(formFile.Length / 1024.0, 2);

            if (!Directory.Exists(Path.GetDirectoryName(finalFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(finalFilePath));
            }

            using (var stream = new FileStream(finalFilePath, FileMode.Create))
            {
                await formFile.CopyToAsync(stream);//await 不能少
            }
            string uploadUrl = SysConfigService.GetSysConfigByKey("sys.file.uploadUrl")?.ConfigValue ?? OptionsSetting.Upload.UploadUrl;
            string accessPath = string.Concat(uploadUrl, "/", filePath.Replace("\\", "/"), "/", fileName);
            SysFile file = new(formFile.FileName, fileName, fileExt, fileSize + "kb", filePath, userName)
            {
                StoreType = (int)Infrastructure.Enums.StoreType.LOCAL,
                FileType = formFile.ContentType,
                FileUrl = finalFilePath,
                AccessUrl = accessPath
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
            string timeDir = date.ToString("yyyyMMdd");

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
                str = Guid.NewGuid().ToString();
            }
            MD5 md5 = MD5.Create();
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
