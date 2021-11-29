using Aliyun.OSS;
using Aliyun.OSS.Common;
using Infrastructure;
using System;
using System.IO;

namespace ZR.Common
{
    public class AliyunOssHelper
    { 
        static string accessKeyId = ConfigUtils.Instance.GetConfig("ALIYUN_OSS:KEY");
        static string accessKeySecret = ConfigUtils.Instance.GetConfig("ALIYUN_OSS:SECRET");
        static string endpoint = ConfigUtils.Instance.GetConfig("ALIYUN_OSS:REGIONID");
        static string bucketName1 = ConfigUtils.Instance.GetConfig("ALIYUN_OSS:bucketName");
        
        /// <summary>
        /// 上传到阿里云
        /// </summary>
        /// <param name="filestreams"></param>
        /// <param name="dirPath">存储路径 eg： upload/2020/01/01/xxx.png</param>
        /// <param name="bucketName">存储桶 如果为空默认取配置文件</param>
        public static System.Net.HttpStatusCode PutObjectFromFile(Stream filestreams, string dirPath, string bucketName = "")
        {
            OssClient client = new OssClient(endpoint, accessKeyId, accessKeySecret);
            if (string.IsNullOrEmpty(bucketName)) { bucketName = bucketName1; }
            try
            {
                dirPath = dirPath.Replace("\\", "/");
                PutObjectResult putObjectResult = client.PutObject(bucketName, dirPath, filestreams);
                // Console.WriteLine("Put object:{0} succeeded", directory);

                return putObjectResult.HttpStatusCode;
            }
            catch (OssException ex)
            {
                Console.WriteLine("Failed with error code: {0}; Error info: {1}. \nRequestID:{2}\tHostID:{3}",
                  ex.ErrorCode, ex.Message, ex.RequestId, ex.HostId);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed with error info: {0}", ex.Message);
            }
            return System.Net.HttpStatusCode.BadRequest;
        }

    }
}
