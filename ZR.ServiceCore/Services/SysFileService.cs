using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ZR.Common;
using ZR.Model.Dto;
using ZR.Model.System;

namespace ZR.ServiceCore.Services
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
        /// <param name="rootPath">存储根目录</param>
        /// <param name="formFile">上传的文件流</param>
        /// <param name="clasifyType">分类类型</param>
        /// <param name="userName"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        public async Task<SysFile> SaveFileToLocal(string rootPath, UploadDto dto, string userName, string clasifyType, IFormFile formFile)
        {
            var fileName = dto.FileName;
            var fileDir = dto.FileDir;
            string fileExt = Path.GetExtension(formFile.FileName);
            fileName = (fileName.IsEmpty() ? HashFileName() : fileName) + fileExt;

            string filePath = GetdirPath(fileDir);
            string finalFilePath = Path.Combine(rootPath, filePath, fileName);
            double fileSize = Math.Round(formFile.Length / 1024.0, 2);

            if (!Directory.Exists(Path.GetDirectoryName(finalFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(finalFilePath));
            }
            // 常见的图片扩展名
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };

            // 检查扩展名是否在图片扩展名列表中
            bool isImageByExtension = imageExtensions.Contains(fileExt);
            if (dto.Quality > 0 && isImageByExtension)
            {
                await SaveCompressedImageAsync(formFile, finalFilePath);
            }
            else
            {
                using (var stream = new FileStream(finalFilePath, FileMode.Create))
                {
                    await formFile.CopyToAsync(stream);
                }
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
                AccessUrl = fullUrl.AbsoluteUri,
                ClassifyType = clasifyType
            };
            file.Id = await InsertFile(file);
            return file;
        }
        //public async Task<SysFile> SaveFileToLocal(string rootPath, string fileName, string fileDir, string userName, IFormFile formFile)
        //{
        //    return await SaveFileToLocal(rootPath, fileName, fileDir, userName, string.Empty, formFile);
        //}
        public async Task<SysFile> SaveFileToLocal(string rootPath, UploadDto dto, string userName, IFormFile formFile)
        {
            return await SaveFileToLocal(rootPath, dto, userName, dto.ClassifyType, formFile);
        }
        /// <summary>
        /// 上传文件到阿里云
        /// </summary>
        /// <param name="file"></param>
        /// <param name="dto"></param>
        /// <param name="formFile"></param>
        /// <returns></returns>
        public async Task<SysFile> SaveFileToAliyun(SysFile file, UploadDto dto, IFormFile formFile)
        {
            file.FileName = (file.FileName.IsEmpty() ? HashFileName() : file.FileName) + file.FileExt;
            file.StorePath = GetdirPath(file.StorePath);
            string finalPath = Path.Combine(file.StorePath, file.FileName);
            HttpStatusCode statusCode;
            if (dto.Quality > 0)
            {
                // 压缩图片
                using var stream = new MemoryStream();
                await CompressImageAsync(formFile, stream, dto.Quality);
                stream.Position = 0;
                statusCode = AliyunOssHelper.PutObjectFromFile(stream, finalPath, "");
            }
            else
            {
                statusCode = AliyunOssHelper.PutObjectFromFile(formFile.OpenReadStream(), finalPath, "");
            }
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
            Console.WriteLine("文件存储目录" + timeDir);
            return timeDir.Replace("\\", "/");
        }

        public string HashFileName(string str = null)
        {
            if (string.IsNullOrEmpty(str))
            {
                str = Guid.NewGuid().ToString().ToLower();
            }
            return BitConverter.ToString(MD5.HashData(Encoding.Default.GetBytes(str)), 4, 8).Replace("-", "").ToLower();
        }

        public Task<long> InsertFile(SysFile file)
        {
            return Insertable(file).ExecuteReturnSnowflakeIdAsync();//单条插入返回雪花ID;
        }

        /// <summary>
        /// 修改文件存储表
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int UpdateFile(SysFile model)
        {
            return Update(model, t => new { t.ClassifyType, }, true);
        }

        /// <summary>
        /// 压缩图片
        /// </summary>
        /// <param name="formFile"></param>
        /// <param name="finalFilePath"></param>
        /// <param name="quality"></param>
        /// <returns></returns>
        public static async Task SaveCompressedImageAsync(IFormFile formFile, string finalFilePath, int quality = 75)
        {
            using (var image = await Image.LoadAsync(formFile.OpenReadStream()))
            {
                // 进行压缩和调整大小（可选）
                //image.Mutate(x => x.Resize(new ResizeOptions
                //{
                //    Mode = ResizeMode.Max,
                //    Size = new Size(1920, 1080) // 限制最大尺寸，避免超大图片
                //}));

                // 保存为压缩的 JPEG
                var encoder = new JpegEncoder { Quality = quality }; // 质量参数控制压缩程度

                await using (var stream = new FileStream(finalFilePath, FileMode.Create))
                {
                    await image.SaveAsync(stream, encoder);
                }
            }
        }
        private async Task CompressImageAsync(IFormFile file, Stream outputStream, int quality)
        {
            // 加载图片
            using var image = await Image.LoadAsync(file.OpenReadStream());

            // 设置 JPEG 编码器并指定质量
            var encoder = new JpegEncoder { Quality = quality };

            // 保存压缩后的图片到输出流
            await image.SaveAsync(outputStream, encoder);
        }
    }
}
