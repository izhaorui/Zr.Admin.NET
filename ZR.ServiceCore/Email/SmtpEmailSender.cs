using Infrastructure.Attribute;
using Infrastructure.Model;
using Microsoft.Extensions.Options;
using System.Linq;
using ZR.Common.Email;
using ZR.Infrastructure.Helper;

namespace ZR.ServiceCore.Email
{
    /// <summary>
    /// 默认邮件发送实现（MailKit）。
    /// - 按 MailOptions 数组取发件配置（FromName 匹配，未匹配取第一条）；
    /// - MailOptions.Enabled=false 时为模拟发送，只记日志不连 SMTP；
    /// - 失败不抛异常，统一返回错误码/错误信息，由调用方决定是否重试。
    /// 业务侧应依赖 <see cref="IEmailSender"/> 或 IEmailSendService，不要直接 new 本类。
    /// </summary>
    [AppService(ServiceType = typeof(IEmailSender), ServiceLifetime = LifeTime.Singleton)]
    public class SmtpEmailSender : IEmailSender
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly IOptions<OptionsSetting> optionsSetting;

        public SmtpEmailSender(IOptions<OptionsSetting> options)
        {
            optionsSetting = options;
        }

        /// <summary>
        /// 批量发送：按发件配置分组，每组复用一条 SMTP 连接。
        /// 返回结果与 messages 顺序一致，失败原因逐封记录。
        /// </summary>
        public async Task<IReadOnlyList<EmailSendResult>> SendBatchAsync(IReadOnlyList<EmailMessage> messages,
            CancellationToken cancellationToken = default)
        {
            var results = new EmailSendResult[messages?.Count ?? 0];
            if (messages == null || messages.Count == 0) return results;

            // 不同 FromName 对应不同发件配置（账号/服务器），必须分组后才能复用连接
            var groups = new Dictionary<MailOptions, List<int>>();
            for (var i = 0; i < messages.Count; i++)
            {
                if (messages[i] == null)
                {
                    results[i] = EmailSendResult.Fail("INVALID_PARAM", "邮件内容不能为空");
                    continue;
                }

                var mailOptions = ResolveOptions(messages[i].FromName);
                if (mailOptions == null)
                {
                    results[i] = EmailSendResult.Fail("NO_CONFIG", "未配置发件邮箱（MailOptions）");
                    continue;
                }

                if (!groups.TryGetValue(mailOptions, out var indexes))
                {
                    indexes = new List<int>();
                    groups[mailOptions] = indexes;
                }
                indexes.Add(i);
            }

            foreach (var group in groups)
            {
                var indexes = group.Value;
                var groupMessages = indexes.Select(i => messages[i]).ToList();
                var groupResults = await MailKitTransport.SendBatchAsync(group.Key, groupMessages, cancellationToken)
                    .ConfigureAwait(false);

                for (var i = 0; i < indexes.Count; i++)
                {
                    results[indexes[i]] = i < groupResults.Count && groupResults[i] != null
                        ? groupResults[i]
                        : EmailSendResult.Fail("SEND_ERROR", "发送结果缺失");
                }

                LogBatchResult(group.Key, groupMessages, groupResults);
            }

            return results;
        }

        public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (message == null)
            {
                return EmailSendResult.Fail("INVALID_PARAM", "邮件内容不能为空");
            }

            var mailOptions = ResolveOptions(message.FromName);
            if (mailOptions == null)
            {
                logger.Warn($"邮件发送失败：未配置 MailOptions，FromName={message.FromName}");
                return EmailSendResult.Fail("NO_CONFIG", "未配置发件邮箱（MailOptions）");
            }

            var result = await MailKitTransport.SendAsync(mailOptions, message, cancellationToken).ConfigureAwait(false);

            var to = MaskRecipients(message.To);
            if (result.Success)
            {
                if (result.Simulated)
                {
                    logger.Info($"【模拟邮件】已跳过真实投递 To={to} Subject={message.Subject}");
                }
                else
                {
                    logger.Info($"邮件发送成功 To={to} Subject={message.Subject} MessageId={result.MessageId}");
                }
            }
            else
            {
                logger.Warn($"邮件发送失败 To={to} Subject={message.Subject} Code={result.ErrorCode} Msg={result.ErrorMsg}");
            }

            return result;
        }

        /// <summary>按 FromName 取发件配置；统一走 EmailOptionsResolver（去空格、忽略大小写、未匹配回退第一条）。</summary>
        public MailOptions ResolveOptions(string fromName)
        {
            return EmailOptionsResolver.Resolve(optionsSetting?.Value?.MailOptions, fromName);
        }

        /// <summary>批量结果日志：汇总一行，失败逐封记录（便于定位具体收件人）</summary>
        private static void LogBatchResult(MailOptions options, List<EmailMessage> messages, IReadOnlyList<EmailSendResult> results)
        {
            var success = results.Count(x => x != null && x.Success);
            var failed = results.Count - success;

            if (failed == 0)
            {
                logger.Info($"邮件批量发送成功 Count={success} From={options.FromEmail}");
                return;
            }

            logger.Warn($"邮件批量发送完成 成功={success} 失败={failed} From={options.FromEmail}");
            for (var i = 0; i < results.Count && i < messages.Count; i++)
            {
                if (results[i] == null || results[i].Success) continue;

                logger.Warn($"邮件批量发送失败 To={MaskRecipients(messages[i].To)} Code={results[i].ErrorCode} Msg={results[i].ErrorMsg}");
            }
        }

        private static string MaskRecipients(IEnumerable<string> recipients)
        {
            if (recipients == null) return string.Empty;

            var list = recipients.Where(x => !string.IsNullOrWhiteSpace(x)).Take(5)
                .Select(MaskUtil.MaskEmail)
                .ToList();
            return string.Join(",", list);
        }
    }
}
