using Infrastructure;
using Infrastructure.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ZR.Common.Email
{
    /// <summary>
    /// 邮件发送统一抽象。
    /// 业务侧只依赖本接口，不直接 new MailHelper；发件配置、协议细节、异常与重试由实现负责。
    /// 现有实现见 ZR.ServiceCore.Email.SmtpEmailSender（MailKit + appsettings MailOptions）。
    /// </summary>
    public interface IEmailSender
    {
        /// <summary>发送单封邮件（异步）。失败不抛异常，统一通过 <see cref="EmailSendResult"/> 返回。</summary>
        Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);

        /// <summary>
        /// 批量发送（复用 SMTP 连接）。返回结果与 messages 顺序一一对应；
        /// 供后台队列批量投递使用，交互式单发请用 <see cref="SendAsync"/>。
        /// </summary>
        Task<IReadOnlyList<EmailSendResult>> SendBatchAsync(IReadOnlyList<EmailMessage> messages,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 邮件消息。收件人/抄送/密送均为纯邮箱地址列表（支持 "张三 &lt;a@b.com&gt;" 形式，解析后取地址）。
    /// </summary>
    public class EmailMessage
    {
        /// <summary>发件人配置名（对应 MailOptions 数组中的 FromName），为空取第一条配置</summary>
        public string FromName { get; set; }

        /// <summary>收件人</summary>
        public List<string> To { get; set; } = new List<string>();

        /// <summary>抄送</summary>
        public List<string> Cc { get; set; } = new List<string>();

        /// <summary>密送</summary>
        public List<string> Bcc { get; set; } = new List<string>();

        /// <summary>主题</summary>
        public string Subject { get; set; }

        /// <summary>HTML 正文</summary>
        public string HtmlBody { get; set; }

        /// <summary>纯文本正文（与 HtmlBody 同时提供时客户端优先显示 HTML）</summary>
        public string TextBody { get; set; }

        /// <summary>附件</summary>
        public List<EmailAttachment> Attachments { get; set; } = new List<EmailAttachment>();
    }

    /// <summary>
    /// 邮件附件：仅支持本地文件路径。
    /// FilePath 会落库到 email_log.FileUrl，重启后重发时可恢复。
    /// </summary>
    public class EmailAttachment
    {
        /// <summary>附件显示名称；为空时取 FilePath 的文件名</summary>
        public string FileName { get; set; }

        /// <summary>本地文件绝对路径（读取后随邮件发送）</summary>
        public string FilePath { get; set; }
    }

    /// <summary>邮件发送状态（对应 email_log.Status）</summary>
    public static class EmailSendStatus
    {
        /// <summary>待发送（已落库、已入队）</summary>
        public const int Pending = 0;
        /// <summary>发送成功</summary>
        public const int Success = 1;
        /// <summary>发送失败（可按 NextRetryTime 重试）</summary>
        public const int Failed = 2;
        /// <summary>发送中（后台已抢占，用于多实例防重复）</summary>
        public const int Sending = 3;
        /// <summary>草稿（只落库不发送，不参与自动重试）</summary>
        public const int Draft = 4;
    }

    /// <summary>
    /// 投递方式（对应 email_log.DispatchMode），决定进程异常退出后的处理策略：
    /// - Direct：调用方同步等待结果（「立即发送」），进程中断时只标记失败，绝不自动重发，避免重复投递；
    /// - Queue：落库后由后台队列投递，可被启动恢复/失败重试接管。
    /// </summary>
    public static class EmailDispatchMode
    {
        /// <summary>同步直发，不参与自动恢复与重试</summary>
        public const int Direct = 0;

        /// <summary>后台队列投递，参与恢复与重试</summary>
        public const int Queue = 1;
    }

    /// <summary>邮件发送结果</summary>
    public class EmailSendResult
    {
        /// <summary>是否发送成功</summary>
        public bool Success { get; set; }

        /// <summary>是否为模拟发送（MailOptions.Enabled=false 时仅记录日志，未真实投递）</summary>
        public bool Simulated { get; set; }

        /// <summary>邮件 Message-Id，便于与收件方/服务端日志对账</summary>
        public string MessageId { get; set; }

        /// <summary>SMTP 服务端返回的自由文本（成功时为最终响应）</summary>
        public string Response { get; set; }

        /// <summary>错误码（NO_CONFIG/NO_RECIPIENT/INVALID_ADDRESS/AUTH_FAILED/TIMEOUT/SMTP_xxx/SEND_ERROR）</summary>
        public string ErrorCode { get; set; }

        /// <summary>错误描述</summary>
        public string ErrorMsg { get; set; }

        public static EmailSendResult Ok(string messageId = null, string response = null, bool simulated = false)
        {
            return new EmailSendResult { Success = true, MessageId = messageId, Response = response, Simulated = simulated };
        }

        public static EmailSendResult Fail(string code, string msg)
        {
            return new EmailSendResult { Success = false, ErrorCode = code, ErrorMsg = msg };
        }
    }

    /// <summary>
    /// 邮件相关的字符串处理工具：统一收发件人/附件地址列表的拆分与合并规则。
    /// </summary>
    public static class EmailTextUtil
    {
        /// <summary>拆分逗号/分号分隔的列表，去空、去首尾空格、忽略大小写去重。</summary>
        public static List<string> SplitList(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new List<string>();

            return value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>合并列表为逗号分隔字符串；空集合返回 null（便于落库后判空）。</summary>
        public static string JoinList(IEnumerable<string> values)
        {
            if (values == null) return null;

            var list = values.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
            return list.Count == 0 ? null : string.Join(",", list);
        }
    }

    /// <summary>
    /// 发件配置解析（MailOptions）。
    /// 统一规则：FromName 去空格后按忽略大小写匹配，未匹配回退第一条，空配置返回 null。
    /// </summary>
    public static class EmailOptionsResolver
    {
        private const int DefaultMaxRetry = 3;

        /// <summary>从全局配置解析发件配置</summary>
        public static MailOptions Resolve(string fromName)
        {
            return Resolve(App.OptionsSetting?.MailOptions, fromName);
        }

        /// <summary>按 FromName 解析发件配置；未匹配回退第一条，空集合返回 null</summary>
        public static MailOptions Resolve(IEnumerable<MailOptions> options, string fromName)
        {
            if (options == null) return null;

            var list = options as IList<MailOptions> ?? options.ToList();
            if (list.Count == 0) return null;

            if (!string.IsNullOrWhiteSpace(fromName))
            {
                var key = fromName.Trim();
                var matched = list.FirstOrDefault(x =>
                    string.Equals(x.FromName, key, StringComparison.OrdinalIgnoreCase));
                if (matched != null)
                {
                    return matched;
                }
            }

            return list[0];
        }

        /// <summary>取该发件配置的最大重试次数，未配置时返回默认值 3</summary>
        public static int ResolveMaxRetry(IEnumerable<MailOptions> options, string fromName)
        {
            var max = Resolve(options, fromName)?.MaxRetryCount ?? 0;
            return max > 0 ? max : DefaultMaxRetry;
        }

        /// <summary>取全部发件配置中的最大重试次数（用于批量扫描 SQL 的粗过滤）</summary>
        public static int ResolveGlobalMaxRetry(IEnumerable<MailOptions> options)
        {
            var list = options?.ToList();
            if (list == null || list.Count == 0) return DefaultMaxRetry;

            var max = list.Max(x => x.MaxRetryCount);
            return max > 0 ? max : DefaultMaxRetry;
        }
    }
}
