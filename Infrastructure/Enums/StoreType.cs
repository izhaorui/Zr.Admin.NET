using System.ComponentModel;

namespace Infrastructure.Enums
{
    /// <summary>
    /// 文件存储位置
    /// </summary>
    public enum StoreType
    {
        /// <summary>
        /// 本地
        /// </summary>
        [Description("本地")]
        LOCAL = 1,

        /// <summary>
        /// 阿里云
        /// </summary>
        [Description("阿里云")]
        ALIYUN = 2,

        /// <summary>
        /// 腾讯云
        /// </summary>
        [Description("腾讯云")]
        TENCENT = 3,

        /// <summary>
        /// 七牛
        /// </summary>
        [Description("七牛云")]
        QINIU = 4,

        /// <summary>
        /// 远程
        /// </summary>
        [Description("远程")]
        REMOTE = 5
    }
}
