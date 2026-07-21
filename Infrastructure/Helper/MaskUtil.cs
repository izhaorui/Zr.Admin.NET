namespace ZR.Infrastructure.Helper
{
    public static class MaskUtil
    {
        /// <summary>
        /// 手机号脱敏
        /// </summary>
        /// <param name="phone"></param>
        /// <returns></returns>
        public static string MaskPhone(string phone)
        {
            if (string.IsNullOrEmpty(phone) || phone.Length < 7) return phone;
            return phone[..3] + "****" + phone.Substring(7);
        }

        /// <summary>
        /// 身份证号
        /// </summary>
        /// <param name="idCard"></param>
        /// <returns></returns>
        public static string MaskIdCard(string idCard)
        {
            if (string.IsNullOrEmpty(idCard) || idCard.Length < 8) return idCard;
            return idCard.Substring(0, 4) + "********" + idCard.Substring(idCard.Length - 4);
        }

        /// <summary>
        /// 昵称
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string MaskName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (name.Length == 2) return name[..1] + "*";
            if (name.Length > 2) return name[..1] + new string('*', name.Length - 2) + name.Substring(name.Length - 1);
            return "*";
        }

        /// <summary>
        /// 邮箱脱敏（保留首字符与完整域名，如 zhangsan@example.com -> z****@example.com）
        /// </summary>
        public static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return email;
            var atIndex = email.IndexOf('@');
            var name = email[..atIndex];
            var domain = email[(atIndex + 1)..];
            if (name.Length <= 1) return email;
            return name[0] + "****@" + domain;
        }

        /// <summary>
        /// 脱敏 IP 地址（支持 IPv4 和 IPv6）
        /// </summary>
        public static string MaskIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return ip;

            if (System.Net.IPAddress.TryParse(ip, out var ipAddress))
            {
                if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    // IPv4：123.45.67.89 -> 123.45.*.*
                    var parts = ip.Split('.');
                    if (parts.Length == 4)
                    {
                        return $"{parts[0]}.{parts[1]}.*.*";
                    }
                }
                else if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    // IPv6：保留前3段，其他替换为 ****
                    var parts = ip.Split(':');
                    for (int i = 3; i < parts.Length; i++)
                    {
                        parts[i] = "****";
                    }
                    return string.Join(":", parts);
                }
            }

            return "***.***.***.***"; // fallback
        }
    }
}
