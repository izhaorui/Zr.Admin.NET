using Infrastructure.Model;
using Microsoft.Extensions.Options;
using System.Net;

namespace ZR.ServiceCore.Oss
{
    /// <summary>
    /// 阿里云 OSS 实现
    /// </summary>
    public class AliyunOssProvider : IOssProvider
    {
        private readonly ALIYUN_OSS _config;

        public string ProviderName => "Aliyun";

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
