using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ZR.ServiceCore.Oss;
using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Model.System.Model;
using ZR.Repository;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 文件管理
    /// </summary>
    [AppService(ServiceType = typeof(ISysFileService), ServiceLifetime = LifeTime.Transient)]
    public class SysFileService : BaseService<SysFile>, ISysFileService
    {
        private readonly ISysConfigService SysConfigService;
        private readonly IOssProvider _ossProvider;
        private OptionsSetting OptionsSetting;

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp"
        };

        public SysFileService(ISysConfigService sysConfigService, IOssProvider ossProvider, IOptions<OptionsSetting> options)
        {
            SysConfigService = sysConfigService;
            _ossProvider = ossProvider;
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
            var fileDir = dto.FileDir;
            string fileExt = Path.GetExtension(formFile.FileName);
            string fileName = BuildFileName(dto.FileName, fileExt);

            string filePath = GetdirPath(fileDir);
            string finalFilePath = Path.Combine(rootPath, filePath, fileName);
            double fileSize = Math.Round(formFile.Length / 1024.0, 2);

            var targetDir = Path.GetDirectoryName(finalFilePath);
            if (!string.IsNullOrWhiteSpace(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            await using (var uploadStream = await CreateUploadStreamAsync(formFile, dto.Quality, fileExt))
            await using (var fileStream = new FileStream(finalFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fileSize = Math.Round(uploadStream.Length / 1024.0, 2); // 更新文件大小为压缩后的大小
                await uploadStream.CopyToAsync(fileStream);
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
                ClassifyType = clasifyType,
                CategoryId = dto.CategoryId,
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
        /// 上传文件到对象存储(OSS)
        /// </summary>
        /// <param name="file"></param>
        /// <param name="dto"></param>
        /// <param name="formFile"></param>
        /// <returns></returns>
        public async Task<SysFile> SaveFileToOss(SysFile file, UploadDto dto, IFormFile formFile)
        {
            string fileExt = string.IsNullOrWhiteSpace(file.FileExt) ? Path.GetExtension(formFile.FileName) : file.FileExt;
            file.FileExt = fileExt;
            file.FileName = BuildFileName(file.FileName, fileExt);
            file.StorePath = GetdirPath(file.StorePath);

            string finalPath = CombineOssPath(file.StorePath, file.FileName);
            HttpStatusCode statusCode;

            await using (var uploadStream = await CreateUploadStreamAsync(formFile, dto.Quality, fileExt))
            {
                statusCode = await _ossProvider.UploadAsync(uploadStream, finalPath);
            }

            if (statusCode != HttpStatusCode.OK) return file;

            file.FileUrl = finalPath;
            file.AccessUrl = _ossProvider.GetAccessUrl(finalPath);
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

        public async Task<long> InsertFile(SysFile file)
        {
            // INSERT 不经全局过滤，需手动填入租户ID
            file.TenantId = App.GetCurrentTenantId();
            return await Insertable(file).ExecuteReturnSnowflakeIdAsync();//单条插入返回雪花ID;
        }

        /// <summary>
        /// 修改文件存储表
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public int UpdateFile(SysFile model)
        {
            return Update(model, t => new { t.ClassifyType, t.RealName, t.CategoryId }, true);
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
            using var image = await Image.LoadAsync(file.OpenReadStream());

            var ext = Path.GetExtension(file.FileName).ToLower();

            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    await image.SaveAsJpegAsync(outputStream,
                        new JpegEncoder
                        {
                            Quality = quality
                        });
                    break;

                case ".png":
                    image.Metadata.ExifProfile = null;

                    image.Mutate(x =>
                    {
                        x.Quantize(new WuQuantizer(new QuantizerOptions
                        {
                            MaxColors = 128//画质最好 体积稍大, 256/ 64/ 32/ 16/ 8/ 4/ 2
                        }));
                    });

                    await image.SaveAsPngAsync(
                        outputStream,
                        new PngEncoder
                        {
                            CompressionLevel = PngCompressionLevel.BestCompression,
                            ColorType = PngColorType.Palette
                        });

                    break;

                default:
                    await image.SaveAsync(outputStream,
                        image.Metadata.DecodedImageFormat);
                    break;
            }
        }

        private string BuildFileName(string sourceName, string fileExt)
        {
            var finalName = sourceName.IsEmpty() ? HashFileName() : sourceName;

            if (!string.IsNullOrWhiteSpace(fileExt) &&
                finalName.EndsWith(fileExt, StringComparison.OrdinalIgnoreCase))
            {
                return finalName;
            }

            return string.Concat(finalName, fileExt);
        }

        private async Task<Stream> CreateUploadStreamAsync(IFormFile formFile, int quality, string fileExt)
        {
            if (quality > 0 && IsImageExtension(fileExt))
            {
                var compressedStream = new MemoryStream();
                await CompressImageAsync(formFile, compressedStream, Math.Clamp(quality, 1, 100));
                compressedStream.Position = 0;
                return compressedStream;
            }

            return formFile.OpenReadStream();
        }

        private static bool IsImageExtension(string fileExt)
        {
            return !string.IsNullOrWhiteSpace(fileExt) && ImageExtensions.Contains(fileExt);
        }

        private static string CombineOssPath(params string[] segments)
        {
            return string.Join("/",
                segments
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim().Trim('/', '\\')));
        }

        public PagedInfo<SysFileDto> GetSysFiles(SysFileQueryDto parm)
        {
            var predicate = Expressionable.Create<SysFile>();

            predicate = predicate.AndIF(parm.BeginCreate_time != null, it => it.Create_time >= parm.BeginCreate_time);
            predicate = predicate.AndIF(parm.EndCreate_time != null, it => it.Create_time <= parm.EndCreate_time);
            predicate = predicate.AndIF(parm.StoreType != null, it => it.StoreType == parm.StoreType);
            predicate = predicate.AndIF(parm.FileId != null, it => it.Id == parm.FileId);
            predicate = predicate.AndIF(parm.ClassifyType != null, it => it.ClassifyType == parm.ClassifyType);
            predicate = predicate.AndIF(parm.CategoryId > 0, it => it.CategoryId == parm.CategoryId);
            predicate = predicate.AndIF(parm.CategoryId == -1, it => it.CategoryId == 0);
            predicate = predicate.AndIF(parm.RealName.IsNotEmpty(), it => it.RealName.Contains(parm.RealName));

            var query = Queryable()
                .LeftJoin<SysFileGroup>((it, g) => it.CategoryId == g.GroupId)
                .Where(predicate.ToExpression())
                .OrderBy(it => it.Id, OrderByType.Desc)
                .Select((it, g) => new SysFileDto
                {
                    GroupName = g.GroupName,
                }, true);

            return query.ToPage(parm);
        }
    }
}
