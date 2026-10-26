using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Infrastructure.Model;
using Infrastructure.Oss;
using Microsoft.Extensions.Options;
using NLog;
using System.Net;

namespace ZR.Oss
{
    /// <summary>
    /// S3 兼容对象存储实现（RustFS / MinIO / Ceph RGW / SeaweedFS / Cloudflare R2 / Backblaze B2 / AWS S3 等）。
    ///
    /// 为什么只用一个通用实现：这些服务都讲 S3 协议，差异（端点、寻址风格、公开域名）全部由配置节吃掉，
    /// 按厂商各写一个 Provider 只是重复代码。反例是阿里云 OSS —— 它不提供 S3 端点，才需要用阿里云 SDK 单独实现。
    ///
    /// ⚠️ AWSSDK 对第三方 S3 兼容服务只能算"部分兼容"：不兼容的是 SDK 的<b>现代化默认行为</b>，
    /// 而不是我们用到的核心操作。下面三处开关缺一不可，<b>请勿"顺手清理"</b>：
    ///
    /// 1) <c>ForcePathStyle = true</c>
    ///    自建 S3 的访问形式是 http://ip:port/bucket/key；不开启时 SDK 按 Virtual-host 拼成
    ///    bucket.ip:port，域名根本无法解析 —— 自建 S3 最常见的翻车点。
    ///
    /// 2) <c>UseChunkEncoding = false</c>
    ///    关掉 aws-chunked 分块签名（STREAMING-AWS4-HMAC-SHA256-PAYLOAD），改用单块签名 + 预计算 SHA256。
    ///    多数兼容服务不支持分块签名，失败形态包括"HTTP 返回 200，但对象内容被包成 chunked 格式而损坏" ——
    ///    最隐蔽的坑，因此自测必须把对象下载回来比对字节，不能只看状态码。
    ///    ⚠️ 不要搭 <c>DisablePayloadSigning = true</c>：AWSSDK v4 规定该选项必须走 HTTPS，
    ///    而自建存储常用明文 http，客户端会直接拒绝发送（本项目实测报错）：
    ///    "AmazonClientException: When DisablePayloadSigning is true, the request must be sent over HTTPS."
    ///
    /// 3) <c>RequestChecksumCalculation = WHEN_REQUIRED</c>
    ///    SDK 3.7.4xx 起默认给 PutObject 附加 CRC32 校验头，多数兼容服务不认，需显式关掉。
    /// </summary>
    public class S3CompatibleOssProvider : IOssProvider
    {
        private readonly S3Compatible _config;
        private readonly IAmazonS3 _client;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary>扩展名 → MIME 类型；上传时不设置 ContentType 会让浏览器把图片当附件下载</summary>
        private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".gif"] = "image/gif",
            [".bmp"] = "image/bmp",
            [".webp"] = "image/webp",
            [".svg"] = "image/svg+xml",
            [".ico"] = "image/x-icon",
            [".avif"] = "image/avif",
            [".mp4"] = "video/mp4",
            [".webm"] = "video/webm",
            [".mov"] = "video/quicktime",
            [".mp3"] = "audio/mpeg",
            [".wav"] = "audio/wav",
            [".ogg"] = "audio/ogg",
            [".pdf"] = "application/pdf",
            [".txt"] = "text/plain",
            [".csv"] = "text/csv",
            [".json"] = "application/json",
            [".xml"] = "application/xml",
            [".html"] = "text/html",
            [".zip"] = "application/zip",
            [".rar"] = "application/vnd.rar",
            [".7z"] = "application/x-7z-compressed",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".ppt"] = "application/vnd.ms-powerpoint",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        };

        /// <summary>展示/日志用名称，取配置的 DisplayName（如 RustFS / MinIO）</summary>
        public string ProviderName => string.IsNullOrWhiteSpace(_config?.DisplayName) ? "S3" : _config.DisplayName.Trim();

        /// <summary>单文件上限(MB)：配置未填时回退默认值</summary>
        public int MaxSize => _config != null && _config.MaxSize > 0 ? _config.MaxSize : 100;

        /// <summary>
        /// 关键配置是否齐全。构造函数不因配置缺失而抛异常（避免"环境没配好导致宿主起不来"），
        /// 所以这里必须做 null 安全判断，由上传入口据此给出可读错误。
        /// </summary>
        public bool IsConfigured =>
            _config != null
            && !string.IsNullOrWhiteSpace(_config.Endpoint)
            && !string.IsNullOrWhiteSpace(_config.AccessKey)
            && !string.IsNullOrWhiteSpace(_config.SecretKey)
            && !string.IsNullOrWhiteSpace(_config.BucketName);

        public S3CompatibleOssProvider(IOptions<OptionsSetting> options)
        {
            _config = options.Value.S3;

            // 客户端在构造时创建一次并复用（本 Provider 由 DI 注册为单例）。
            // 每次上传都 new 一个客户端会丢掉连接池与凭据缓存，高并发下代价明显。
            if (IsConfigured)
            {
                _client = new AmazonS3Client(
                    new BasicAWSCredentials(_config.AccessKey.Trim(), _config.SecretKey.Trim()),
                    BuildClientConfig());
            }
        }

        public async Task<HttpStatusCode> UploadAsync(Stream stream, string objectKey, string bucketName = "")
        {
            if (!IsConfigured)
            {
                _logger.Error("S3 对象存储配置不完整（endpoint / accessKey / secretKey / bucketName 均需填写），已跳过上传。key={Key}", objectKey);
                return HttpStatusCode.BadRequest;
            }

            var bucket = ResolveBucket(bucketName);
            var key = NormalizeKey(objectKey);

            try
            {
                var request = new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = key,
                    InputStream = stream,
                    // 不设置 ContentType 会让浏览器访问变成"下载"而非预览
                    ContentType = ResolveContentType(objectKey),
                    // 流由调用方（SysFileService 的 await using）负责释放
                    AutoCloseStream = false,
                    // 关掉 aws-chunked 分块签名（改用单块签名 + 预计算 SHA256），见类注释第 2 条。
                    // ⚠️ 不要在这里加 DisablePayloadSigning = true —— AWSSDK v4 要求该选项必须走 HTTPS，
                    // 自建存储多用明文 http，客户端会直接拒绝发送：
                    //   AmazonClientException: When DisablePayloadSigning is true, the request must be sent over HTTPS.
                    UseChunkEncoding = false,
                };

                // 对象缓存头：让浏览器 / Nginx / CDN 缓存图片，避免每次浏览都重新下载
                //（自建存储 + 固定流量包场景下这是最省流量的一项，配置见 S3 配置节的 cacheControl）
                // 注意：AWSSDK v4 起 CacheControl 这类标头不再直接挂在 PutObjectRequest 上，
                // 而是统一收进 Headers 集合（Headers 由 SDK 初始化，可直接赋值子属性）
                if (!string.IsNullOrWhiteSpace(_config.CacheControl))
                {
                    request.Headers.CacheControl = _config.CacheControl.Trim();
                }

                var response = await _client.PutObjectAsync(request);
                return response.HttpStatusCode;
            }
            catch (AmazonS3Exception ex)
            {
                // 服务端返回了错误（4xx/5xx）：ErrorCode / HttpStatus / RequestId 是定位关键
                //（NoSuchBucket、SignatureDoesNotMatch、AccessDenied、InvalidRequest 等）
                LogFailure("上传", "服务端拒绝(4xx/5xx)", ex, bucket, key, ex.ErrorCode);
                return HttpStatusCode.BadRequest;
            }
            catch (AmazonClientException ex)
            {
                // 客户端侧：连不上 / DNS / 超时 / 证书 / 流不可重试 —— 请求根本没被服务端处理
                LogFailure("上传", "客户端异常(无法连接/超时/证书)", ex, bucket, key);
                return HttpStatusCode.BadRequest;
            }
            catch (Exception ex)
            {
                LogFailure("上传", "未预期异常", ex, bucket, key);
                return HttpStatusCode.BadRequest;
            }
        }

        public async Task<HttpStatusCode> DeleteAsync(string objectKey, string bucketName = "")
        {
            if (!IsConfigured)
            {
                _logger.Error("S3 对象存储配置不完整，已跳过删除。key={Key}", objectKey);
                return HttpStatusCode.BadRequest;
            }

            var bucket = ResolveBucket(bucketName);
            var key = NormalizeKey(objectKey);

            try
            {
                var response = await _client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = bucket,
                    Key = key,
                });
                return response.HttpStatusCode;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // 对象本就不存在 → 视为删除成功（幂等，避免重复删除报错）
                return HttpStatusCode.OK;
            }
            catch (AmazonS3Exception ex)
            {
                LogFailure("删除", "服务端拒绝(4xx/5xx)", ex, bucket, key, ex.ErrorCode);
                return HttpStatusCode.BadRequest;
            }
            catch (AmazonClientException ex)
            {
                LogFailure("删除", "客户端异常(无法连接/超时/证书)", ex, bucket, key);
                return HttpStatusCode.BadRequest;
            }
            catch (Exception ex)
            {
                LogFailure("删除", "未预期异常", ex, bucket, key);
                return HttpStatusCode.BadRequest;
            }
        }

        /// <summary>
        /// 拼接可公开访问的完整 URL（存进 SysFile.AccessUrl，前端 image src 直接使用）。
        /// 不用预签名 URL：它有有效期，落库后会集体失效。
        /// DomainUrl 未配置时降级返回对象键（与既有实现语义一致）。
        /// </summary>
        public string GetAccessUrl(string objectKey)
        {
            var key = NormalizeKey(objectKey);
            if (string.IsNullOrWhiteSpace(_config?.DomainUrl))
            {
                return key;
            }

            return $"{_config.DomainUrl.TrimEnd('/')}/{key.TrimStart('/')}";
        }

        private AmazonS3Config BuildClientConfig()
        {
            var endpoint = _config.Endpoint.Trim().TrimEnd('/');
            return new AmazonS3Config
            {
                ServiceURL = endpoint,
                // 端点写的是 http:// 时必须显式声明，否则部分 SDK 版本仍按 https 发起请求，握手直接失败
                UseHttp = endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
                // 见类注释第 1 条：自建服务必须用路径风格寻址
                ForcePathStyle = _config.ForcePathStyle,
                // SDK 签名必填项，自建服务通常忽略该值
                AuthenticationRegion = string.IsNullOrWhiteSpace(_config.Region) ? "us-east-1" : _config.Region.Trim(),
                Timeout = TimeSpan.FromMinutes(5),
                MaxErrorRetry = 3,
                // 见类注释第 3 条：关掉默认附加的 CRC32 校验头
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            };
        }

        /// <summary>
        /// 统一的失败日志：把异常链压进单行，便于"一眼定位"。
        /// 原先只记 endpoint/bucket/key，真正的异常类型与消息要靠堆栈上下文才能看到；
        /// 而 S3 的真实原因常藏在 InnerException 里
        ///（例如 AmazonClientException ← HttpRequestException: Connection refused ← SocketException）。
        /// </summary>
        private void LogFailure(string action, string reason, Exception ex, string bucket, string key, string errorCode = null)
        {
            _logger.Error(ex,
                "S3 {Action}失败（{Reason}）：{Error} | errorCode={ErrorCode} endpoint={Endpoint} bucket={Bucket} key={Key}",
                action, reason, DescribeException(ex), errorCode ?? "-", _config?.Endpoint, bucket, key);
        }

        /// <summary>
        /// 把异常链拍平成一行，最多展开 5 层（异常链可能很长，避免刷屏）：
        /// "AmazonClientException: xxx ← HttpRequestException: Connection refused ← SocketException: ..."
        /// </summary>
        private static string DescribeException(Exception ex)
        {
            var parts = new List<string>();
            for (var current = ex; current != null && parts.Count < 5; current = current.InnerException)
            {
                parts.Add($"{current.GetType().Name}: {current.Message}");
            }
            return string.Join(" ← ", parts);
        }

        private string ResolveBucket(string bucketName)
        {
            return string.IsNullOrWhiteSpace(bucketName) ? _config.BucketName : bucketName;
        }

        private static string NormalizeKey(string objectKey)
        {
            return (objectKey ?? string.Empty).Replace("\\", "/").TrimStart('/');
        }

        private static string ResolveContentType(string objectKey)
        {
            var ext = Path.GetExtension(objectKey ?? string.Empty);
            return !string.IsNullOrEmpty(ext) && ContentTypes.TryGetValue(ext, out var contentType)
                ? contentType
                : "application/octet-stream";
        }
    }
}
