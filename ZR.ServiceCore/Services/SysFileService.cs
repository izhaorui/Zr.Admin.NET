using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Infrastructure.Oss;
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
        /// <summary>
        /// 对象存储实现工厂：上传时按调用方传入的 StoreType 选择实现。
        /// 不用单例 IOssProvider —— 否则会出现"前端选 S3、后端却拿阿里云客户端去传"的错配。
        /// </summary>
        private readonly IOssProviderFactory _ossProviderFactory;
        private OptionsSetting OptionsSetting;

        public SysFileService(ISysConfigService sysConfigService, IOssProviderFactory ossProviderFactory, IOptions<OptionsSetting> options)
        {
            SysConfigService = sysConfigService;
            _ossProviderFactory = ossProviderFactory;
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
        /// <param name="storeType">存储类型：决定使用哪个对象存储实现（2=阿里云 / 6=S3 兼容）</param>
        /// <returns></returns>
        public async Task<SysFile> SaveFileToOss(SysFile file, UploadDto dto, IFormFile formFile, StoreType storeType = StoreType.ALIYUN)
        {
            // 按存储类型取实现，与调用方落库的 StoreType 保持一致，
            // 避免"类型记成 S3、实际却用阿里云客户端上传"的错配
            var ossProvider = _ossProviderFactory.Get(storeType);

            string fileExt = string.IsNullOrWhiteSpace(file.FileExt) ? Path.GetExtension(formFile.FileName) : file.FileExt;
            file.FileExt = fileExt;
            file.FileName = BuildFileName(file.FileName, fileExt);
            file.StorePath = GetdirPath(file.StorePath);

            string finalPath = CombineOssPath(file.StorePath, file.FileName);
            HttpStatusCode statusCode;

            await using (var uploadStream = await CreateUploadStreamAsync(formFile, dto.Quality, fileExt))
            {
                statusCode = await ossProvider.UploadAsync(uploadStream, finalPath);
            }

            if (statusCode != HttpStatusCode.OK) return file;

            file.FileUrl = finalPath;
            file.AccessUrl = ossProvider.GetAccessUrl(finalPath);
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
            string timeDir = byTimeStore ? DateTime.Now.ToString("yyyy/MMdd") : string.Empty;

            if (!string.IsNullOrEmpty(storePath))
            {
                timeDir = string.IsNullOrEmpty(timeDir) ? storePath : Path.Combine(storePath, timeDir);
            }

            if (App.IsTenantEnabled())
            {
                var tenantSeg = SanitizeTenantPathSegment(App.GetCurrentTenantId());
                timeDir = Path.Combine("tenant", tenantSeg, timeDir);
            }

            return timeDir.Replace("\\", "/").Trim('/');
        }

        private static string SanitizeTenantPathSegment(string tenantId)
        {
            var raw = string.IsNullOrWhiteSpace(tenantId) ? App.MainDbConfigId : tenantId.Trim();
            var chars = raw.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray();
            var safe = new string(chars);
            return string.IsNullOrEmpty(safe) ? "unknown" : safe;
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

        /// <summary>
        /// 上传流：直接返回原始文件流。
        /// </summary>
        /// <remarks>
        /// 图片压缩能力已移除（SkiaSharp 的 native 依赖在发布包里占比过大，与收益不成正比）。
        /// quality / fileExt 参数保留是为了不改动两条上传链路的调用签名：
        /// 前端仍可传 Quality，但服务端不再据此转码，文件按原始字节落盘/上云。
        /// </remarks>
        private Task<Stream> CreateUploadStreamAsync(IFormFile formFile, int quality, string fileExt)
        {
            return Task.FromResult(formFile.OpenReadStream());
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
