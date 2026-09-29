using Infrastructure.Enums;

namespace ZR.ServiceCore.Oss
{
    /// <summary>
    /// 按存储类型获取对象存储实现。
    ///
    /// 为什么需要它：`StoreType` 表达的是"这份文件要存到哪里"，由上传调用方（前端存储类型下拉）显式选择；
    /// 而 `IOssProvider` 单例只由全局配置决定。两者一旦不一致就会出现静默错配 ——
    /// 前端选了「S3 兼容存储」，落库类型是 S3，实际却拿阿里云客户端去传（两边落库类型不同却共用同一实现）。
    /// 由本工厂让 `StoreType` 直接决定实现，全局配置只作为兜底默认。
    /// </summary>
    public interface IOssProviderFactory
    {
        /// <summary>
        /// 按存储类型获取实现。
        /// 未与对象存储对应的取值（LOCAL / TENCENT / QINIU / REMOTE）回退到配置指定的默认实现。
        /// </summary>
        IOssProvider Get(StoreType storeType);

        /// <summary>
        /// 配置指定的默认实现（`OptionsSetting.OssProvider`，缺省 Aliyun）。
        /// 保留它只为兼容历史行为与其它潜在调用方；上传链路请用 <see cref="Get"/>。
        /// </summary>
        IOssProvider GetDefault();
    }
}
