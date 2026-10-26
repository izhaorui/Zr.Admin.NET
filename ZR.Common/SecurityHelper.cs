using System;
using System.Security.Cryptography;
using System.Text;

namespace ZR.Common
{
    /// <summary>
    /// 安全相关帮助方法。
    /// </summary>
    public static class SecurityHelper
    {
        /// <summary>
        /// 计算字符串的 MD5（UTF-8 编码，32 位大写十六进制）。
        /// </summary>
        /// <remarks>
        /// 输出与原 NETCore.Encrypt.EncryptProvider.Md5 完全一致（已用 ASCII / 纯中文 / 混合输入实测比对），
        /// 因此替换后不影响存量密码校验，也与种子数据里的 sys_user.password 同格式。
        /// </remarks>
        public static string Md5(string source)
        {
            return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(source)));
        }
    }
}
