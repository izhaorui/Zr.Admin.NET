using Infrastructure.Model;
using Microsoft.Extensions.Options;
using System.Net;

namespace ZR.ServiceCore.Oss
{
    /// <summary>
    /// 阿里云 OSS 实现
    ///
    /// ⚠️ 现状：上传/删除<b>尚未实现</b>（内部调 SDK 的代码被注释掉，UploadAsync 恒返回 BadRequest）。
    /// 因此生产环境<b>不要</b>把 OptionsSetting.OssProvider 配成 Aliyun —— 需要对象存储时请使用
    /// S3CompatibleOssProvider（配置 OssProvider=s3，适用于 RustFS / MinIO / Ceph 等 S3 兼容服务）。
    /// 本类保留是因为阿里云 OSS 不提供 S3 兼容端点，将来要真正支持阿里云需在此用阿里云官方 SDK 实现。
    /// </summary>
    public class AliyunOssProvider : IOssProvider
    {
        private readonly ALIYUN_OSS _config;

        public string ProviderName => "Aliyun";

        /// <summary>
        /// 配置缺失（_config 为 null，例如宿主未配置 ALIYUN_OSS 节点）时回退到默认上限
        /// </summary>
        public int MaxSize => _config != null && _config.MaxSize > 0 ? _config.MaxSize : 100;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_config?.REGIONID)
            && !string.IsNullOrWhiteSpace(_config?.KEY)
            && !string.IsNullOrWhiteSpace(_config?.SECRET)
            && !string.IsNullOrWhiteSpace(_config?.BucketName);

        public AliyunOssProvider(IOptions<OptionsSetting> options)
        {
            _config = options.Value.ALIYUN_OSS;
        }

        public async Task<HttpStatusCode> UploadAsync(Stream stream, string objectKey, string bucketName = "")
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(bucketName))
                {
                    bucketName = _config.BucketName;
                }

                //try
                //{
                    //objectKey = objectKey.Replace("\\", "/");
                //    OssClient client = new(_config.REGIONID, _config.KEY, _config.SECRET);
                //    PutObjectResult result = client.PutObject(bucketName, objectKey, stream);
                //    return result.HttpStatusCode;
                //}
                //catch (OssException ex)
                //{
                //    Console.WriteLine("Failed with error code: {0}; Error info: {1}. \nRequestID:{2}\tHostID:{3}",
                //        ex.ErrorCode, ex.Message, ex.RequestId, ex.HostId);
                //}
                //catch (Exception ex)
                //{
                //    Console.WriteLine("Failed with error info: {0}", ex.Message);
                //}

                return HttpStatusCode.BadRequest;
            });
        }

        public async Task<HttpStatusCode> DeleteAsync(string objectKey, string bucketName = "")
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(bucketName))
                {
                    bucketName = _config.BucketName;
                }

                try
                {
                    //OssClient client = new(_config.REGIONID, _config.KEY, _config.SECRET);
                    //client.DeleteObject(bucketName, objectKey);
                    return HttpStatusCode.OK;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed with error info: {0}", ex.Message);
                    return HttpStatusCode.BadRequest;
                }
            });
        }

        public string GetAccessUrl(string objectKey)
        {
            if (string.IsNullOrWhiteSpace(_config.DomainUrl))
            {
                return objectKey.Replace("\\", "/");
            }

            return $"{_config.DomainUrl.TrimEnd('/')}/{objectKey.TrimStart('/').Replace("\\", "/")}";
        }
    }
}
