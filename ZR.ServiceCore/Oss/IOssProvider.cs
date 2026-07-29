using System.Net;

namespace ZR.ServiceCore.Oss
{
    /// <summary>
    /// 对象存储(OSS)提供程序抽象，便于对接阿里云 / 腾讯云 / 七牛等不同厂商
    /// </summary>
    public interface IOssProvider
    {
        /// <summary>
        /// 提供程序名称（Aliyun / Tencent / Qiniu ...）
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// 上传对象
        /// </summary>
        /// <param name="stream">文件流</param>
        /// <param name="objectKey">对象在存储中的路径 eg: upload/2020/01/01/xxx.png</param>
        /// <param name="bucketName">存储桶，为空取默认配置</param>
        /// <returns>Http 状态码</returns>
        Task<HttpStatusCode> UploadAsync(Stream stream, string objectKey, string bucketName = "");

        /// <summary>
        /// 删除对象
        /// </summary>
        /// <param name="objectKey">对象路径</param>
        /// <param name="bucketName">存储桶，为空取默认配置</param>
        /// <returns>Http 状态码</returns>
        Task<HttpStatusCode> DeleteAsync(string objectKey, string bucketName = "");

        /// <summary>
        /// 根据对象路径拼接可访问的完整 URL
        /// </summary>
        /// <param name="objectKey">对象路径</param>
        /// <returns>可访问的完整 URL</returns>
        string GetAccessUrl(string objectKey);
    }
}
