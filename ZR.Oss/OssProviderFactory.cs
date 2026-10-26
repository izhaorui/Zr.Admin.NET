using Infrastructure.Enums;
using Infrastructure.Model;
using Infrastructure.Oss;
using Microsoft.Extensions.Options;

namespace ZR.Oss
{
    /// <summary>
    /// <see cref="IOssProviderFactory"/> 默认实现。
    ///
    /// 每个实现都用 <see cref="Lazy{T}"/> 只构造一次并缓存：像 `AmazonS3Client` 这类客户端自带连接池与
    /// 凭据缓存，每次上传都新建会明显拖慢高并发下的表现（Provider 构造时就会创建客户端）。
    /// 同时 Lazy 也保证"没配 S3 就不构造 S3 客户端"，避免未启用的存储类型产生无谓开销。
    /// </summary>
    public class OssProviderFactory : IOssProviderFactory
    {
        private readonly IOptions<OptionsSetting> _options;
        private readonly Lazy<IOssProvider> _aliyun;
        private readonly Lazy<IOssProvider> _s3;
        private readonly Lazy<IOssProvider> _configured;

        public OssProviderFactory(IOptions<OptionsSetting> options)
        {
            _options = options;
            _aliyun = new Lazy<IOssProvider>(() => new AliyunOssProvider(_options));
            _s3 = new Lazy<IOssProvider>(() => new S3CompatibleOssProvider(_options));
            _configured = new Lazy<IOssProvider>(ResolveConfigured);
        }

        public IOssProvider Get(StoreType storeType) => storeType switch
        {
            // 存储类型与实现一一对应：调用方选什么就用什么客户端，不再受全局配置影响
            StoreType.S3 => _s3.Value,
            StoreType.ALIYUN => _aliyun.Value,
            // 其余类型不涉及对象存储；万一被调用，沿用配置的默认实现（保持历史行为）
            _ => GetDefault()
        };

        public IOssProvider GetDefault() => _configured.Value;

        private IOssProvider ResolveConfigured()
        {
            var provider = (_options.Value.OssProvider ?? "Aliyun").Trim().ToLowerInvariant();
            return provider switch
            {
                "s3" => _s3.Value,
                "aliyun" => _aliyun.Value,
                // 未知取值回退阿里云，保持历史行为（厂商名写错不会报错，注意排查配置拼写）
                _ => _aliyun.Value
            };
        }
    }
}
