using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Infrastructure.Oss
{
    /// <summary>
    /// 对象存储(OSS)提供程序抽象，便于对接阿里云 / 腾讯云 / 七牛等不同厂商。
    /// </summary>
    /// <remarks>
    /// 抽象放在 Infrastructure 而不是具体实现所在项目：ZR.ServiceCore 的 SysFileService 只依赖本接口，
    /// 实现（含 AWSSDK）由 ZR.Oss 提供、仅被需要上传功能的宿主引用，
    /// 避免 AWSSDK 顺着"业务项目 → ServiceCore"的引用链进入 ZR.Tasks 等无关宿主。
    /// </remarks>
    public interface IOssProvider
    {
        /// <summary>
        /// 提供程序名称（Aliyun / Tencent / Qiniu / S3 ...）
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// 单文件大小上限(MB)。
        /// 由各厂商自己的配置决定，供上传入口统一校验 —— 调用方不应再去读某个具体厂商的配置节
        /// （否则每加一个厂商都要在上传入口复制一份分支）。
        /// </summary>
        int MaxSize { get; }

        /// <summary>
        /// 关键配置是否齐全（端点 / 密钥 / 桶名等）。
        /// 上传入口据此直接给出可读错误，而不是让底层 SDK 抛异常。
        /// 约定：配置不齐全时构造函数<b>不得</b>抛异常（避免"环境没配好导致宿主起不来"）。
        /// </summary>
        bool IsConfigured { get; }

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
