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

        /// <summary>
        /// 对一整段自由文本做批量脱敏：自动识别其中的手机号、身份证、邮箱、银行卡、金额，
        /// 统一打码后返回。用于 AI 场景（表单填报内容 / 用户自然语言描述）发送给第三方模型前，
        /// 避免个人隐私与企业敏感数据明文出网。无法识别的普通文本原样保留。
        /// </summary>
        public static string MaskSensitiveText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            // 手机号（1 开头，11 位）
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"(?&lt;!\d)1[3-9]\d{9}(?!\d)", m => MaskPhone(m.Value));

            // 身份证（15 或 18 位，18 位末位可为 X）
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"(?&lt;!\d)\d{15}(?!\d)|(?&lt;!\d)\d{17}[\dXx](?!\d)", m => MaskIdCard(m.Value));

            // 邮箱
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", m => MaskEmail(m.Value));

            // 银行卡（16~19 位纯数字，前后非数字边界）
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"(?&lt;!\d)\d{16,19}(?!\d)", m => MaskBankCard(m.Value));

            // 金额（带货币符号或「元/万元」单位，如 ¥12345、12345.00元、12.3万元）
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"(?&lt;!\d)\d{1,3}(,\d{3})*(\.\d+)?\s*(元|万元|块钱|RMB|￥|$)", m => MaskAmount(m.Value));

            return text;
        }

        /// <summary>
        /// 银行卡脱敏：保留前 6 位与后 4 位，中间打码。
        /// </summary>
        public static string MaskBankCard(string card)
        {
            if (string.IsNullOrEmpty(card) || card.Length < 10) return card;
            return card.Substring(0, 6) + new string('*', card.Length - 10) + card.Substring(card.Length - 4);
        }

        /// <summary>
        /// 金额脱敏：仅保留数量级，具体数值打码（如 12345.00元 -> *****元）。
        /// 保留单位便于模型理解上下文，但不泄露精确金额。
        /// </summary>
        public static string MaskAmount(string amount)
        {
            if (string.IsNullOrWhiteSpace(amount)) return amount;
            // 末位是单位或货币符号时保留，前面的数字部分打码
            var last = amount[amount.Length - 1];
            var unit = char.IsDigit(last) || last == '.' || last == ',' ? string.Empty : last.ToString();
            var digits = amount.Substring(0, amount.Length - unit.Length);
            return new string('*', digits.Length) + unit;
        }
    }
}
