using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    }
}
