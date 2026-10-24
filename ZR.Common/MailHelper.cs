using Infrastructure;
using Infrastructure.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZR.Common.Email;

namespace ZR.Common
{
    /// <summary>
    /// 邮件发送历史兼容壳。
    ///
    /// 新代码请优先注入 <see cref="IEmailSender"/>（ZR.ServiceCore.Email.SmtpEmailSender）或 IEmailSendService，
    /// 以获得 Cc/Bcc、模板、发送记录、失败重试等能力；本类仅为不破坏既有调用方而保留，
    /// 内部已统一走 <see cref="MailKitTransport"/>，历史 Bug（多附件文件名取错、附件流不释放、
    /// 连接/认证异常直接抛出、证书无条件信任）已在传输层修复。
    /// </summary>
    [Obsolete("MailHelper 为历史兼容层（同步阻塞、绕过 DI）。新代码请注入 IEmailSender 或 IEmailSendService。")]
    public class MailHelper
    {
        private readonly MailOptions mailOptions;

        public MailHelper()
        {
            var options = App.OptionsSetting?.MailOptions;
            if (options == null || options.Count == 0)
            {
                // 容错：静态配置尚未就绪时回退读取 appsettings，且空配置不再抛 InvalidOperationException
                options = new List<MailOptions>();
                try
                {
                    AppSettings.Bind("MailOptions", options);
                }
                catch
                {
                    // 配置缺失属合法状态，发送时会返回 NO_CONFIG
                }
            }

            mailOptions = EmailOptionsResolver.Resolve(options, null) ?? new MailOptions();
        }

        public MailHelper(MailOptions _mailOptions)
        {
            mailOptions = _mailOptions ?? new MailOptions();
        }

        /// <summary>发送给单个收件人，返回 "fail" 表示失败（兼容历史判断）。</summary>
        public string SendMail(string toAddress, string subject, string text, string path = "", string html = "")
        {
            return SendMail(new[] { toAddress }, subject, text, path, html);
        }

        /// <summary>发送给多个收件人，返回 "fail" 表示失败（兼容历史判断）。</summary>
        public string SendMail(string[] toAddress, string subject, string text, string path = "", string html = "")
        {
            return SendMailAsync(toAddress, subject, text, path, html).GetAwaiter().GetResult();
        }

        /// <summary>异步发送（推荐新代码使用）。返回 "fail" 表示失败。</summary>
        public async Task<string> SendMailAsync(string[] toAddress, string subject, string text, string path = "", string html = "")
        {
            var message = new EmailMessage
            {
                FromName = mailOptions.FromName,
                To = toAddress?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>(),
                Subject = subject,
                TextBody = text,
                HtmlBody = html,
            };

            if (!string.IsNullOrWhiteSpace(path))
            {
                foreach (var file in path.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    message.Attachments.Add(new EmailAttachment { FilePath = file.Trim() });
                }
            }

            var result = await MailKitTransport.SendAsync(mailOptions, message, default).ConfigureAwait(false);
            if (result.Success)
            {
                return result.Response ?? result.MessageId ?? "ok";
            }

            Log.WriteLine(ConsoleColor.Red, $"发送邮件失败：{result.ErrorCode} {result.ErrorMsg}");
            return "fail";
        }
    }
}
