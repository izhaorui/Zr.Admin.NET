using Infrastructure.Model;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using MimeKit.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZR.Common.Email
{
    /// <summary>
    /// MailKit 传输层：只负责「用给定发件配置把 EmailMessage 投递出去」，不做配置解析、不写业务日志。
    /// 统一入口是 ZR.ServiceCore.Email.SmtpEmailSender；MailHelper 作为历史兼容壳也复用本类。
    ///
    /// 连接模型：
    /// - SendAsync：单封邮件，独立连接（交互式发送，需要尽快返回结果）；
    /// - SendBatchAsync：同批发件配置只建立一次 SMTP 连接，依次投递，供后台队列批量补发/重发使用。
    /// </summary>
    public static class MailKitTransport
    {
        /// <summary>默认超时（秒）。MailKit 自身默认 120 秒，Web 请求里等太久没有意义。</summary>
        private const int DefaultTimeoutSeconds = 30;

        /// <summary>单封发送</summary>
        public static async Task<EmailSendResult> SendAsync(MailOptions options, EmailMessage message, CancellationToken cancellationToken)
        {
            var basicError = ValidateBasicOptions(options);
            if (basicError != null) return basicError;

            var resources = new List<IDisposable>();
            try
            {
                EmailSendResult prepareError;
                MimeMessage mime;
                try
                {
                    prepareError = Prepare(options, message, resources, out mime);
                }
                catch (Exception ex)
                {
                    // 附件读取、非法地址等构建期异常统一转为失败结果，避免异常穿透且资源泄漏（finally 兜底释放）
                    return EmailSendResult.Fail("SEND_ERROR", ex.Message);
                }

                if (prepareError != null) return prepareError;

                // 未启用真实发送：只走模拟，避免开发/测试环境误发
                if (!options.Enabled) return EmailSendResult.Ok(simulated: true);

                var smtpError = ValidateSmtpOptions(options);
                if (smtpError != null) return smtpError;

                using var client = CreateClient(options);
                try
                {
                    await ConnectAsync(client, options, cancellationToken).ConfigureAwait(false);

                    var response = await client.SendAsync(mime, cancellationToken).ConfigureAwait(false);
                    await DisconnectAsync(client, cancellationToken).ConfigureAwait(false);

                    return EmailSendResult.Ok(mime.MessageId, response);
                }
                catch (Exception ex)
                {
                    return MapException(ex, options);
                }
            }
            finally
            {
                DisposeAll(resources);
            }
        }

        /// <summary>
        /// 批量发送（复用同一条 SMTP 连接）。返回结果与 messages 顺序一一对应：
        /// 单封失败只影响该封（如收件人不存在）；连接级异常会把当前及后续未发送的邮件统一标记失败，由调用方重试。
        /// </summary>
        public static async Task<IReadOnlyList<EmailSendResult>> SendBatchAsync(MailOptions options,
            IReadOnlyList<EmailMessage> messages, CancellationToken cancellationToken)
        {
            var results = new EmailSendResult[messages?.Count ?? 0];
            if (messages == null || messages.Count == 0) return results;

            var basicError = ValidateBasicOptions(options);
            if (basicError != null)
            {
                Fill(results, basicError);
                return results;
            }

            // 先把每封邮件校验/构建好：地址、附件在连接前就失败的，不需要占用 SMTP 会话
            var prepared = new List<(int Index, MimeMessage Mime, List<IDisposable> Resources)>();
            for (var i = 0; i < messages.Count; i++)
            {
                var resources = new List<IDisposable>();
                EmailSendResult failure;
                MimeMessage mime;
                try
                {
                    failure = Prepare(options, messages[i], resources, out mime);
                }
                catch (Exception ex)
                {
                    // 单封构建异常不影响整批，释放该封已打开的资源
                    DisposeAll(resources);
                    results[i] = EmailSendResult.Fail("SEND_ERROR", ex.Message);
                    continue;
                }

                if (failure != null)
                {
                    DisposeAll(resources);
                    results[i] = failure;
                    continue;
                }

                prepared.Add((i, mime, resources));
            }

            try
            {
                if (prepared.Count == 0) return results;

                if (!options.Enabled)
                {
                    foreach (var item in prepared)
                    {
                        results[item.Index] = EmailSendResult.Ok(simulated: true);
                    }
                    return results;
                }

                var smtpError = ValidateSmtpOptions(options);
                if (smtpError != null)
                {
                    foreach (var item in prepared)
                    {
                        results[item.Index] = smtpError;
                    }
                    return results;
                }

                using var client = CreateClient(options);
                try
                {
                    await ConnectAsync(client, options, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // 连接/认证失败：整批都不可投递
                    var failure = MapException(ex, options);
                    foreach (var item in prepared)
                    {
                        results[item.Index] = failure;
                    }
                    return results;
                }

                for (var i = 0; i < prepared.Count; i++)
                {
                    var item = prepared[i];
                    try
                    {
                        var response = await client.SendAsync(item.Mime, cancellationToken).ConfigureAwait(false);
                        results[item.Index] = EmailSendResult.Ok(item.Mime.MessageId, response);
                    }
                    catch (SmtpCommandException ex)
                    {
                        // 单封被拒（如收件人不存在）继续发后续，不中断整批
                        results[item.Index] = EmailSendResult.Fail($"SMTP_{(int)ex.StatusCode}", ex.Message);

                        if (ex.StatusCode == SmtpStatusCode.ServiceNotAvailable)
                        {
                            MarkRemaining(results, prepared, i + 1, results[item.Index]);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 连接级异常（超时/协议/断开）：剩余邮件统一标记失败，交由重试
                        var failure = MapException(ex, options);
                        MarkRemaining(results, prepared, i, failure);
                        break;
                    }
                }

                await DisconnectAsync(client, cancellationToken).ConfigureAwait(false);
                return results;
            }
            finally
            {
                foreach (var item in prepared)
                {
                    DisposeAll(item.Resources);
                }
            }
        }

        /// <summary>
        /// 单封邮件的校验与 MIME 构建：地址格式、收件人去重、附件大小/存在性。
        /// 返回 null 表示成功；resources 中的流/文件句柄由调用方统一释放。
        /// </summary>
        private static EmailSendResult Prepare(MailOptions options, EmailMessage message,
            List<IDisposable> resources, out MimeMessage mime)
        {
            mime = null;
            if (message == null)
            {
                return EmailSendResult.Fail("INVALID_PARAM", "邮件内容不能为空");
            }

            var to = ParseAddresses(message.To, out var invalid);
            var cc = ParseAddresses(message.Cc, out var invalidCc);
            var bcc = ParseAddresses(message.Bcc, out var invalidBcc);

            invalid.AddRange(invalidCc);
            invalid.AddRange(invalidBcc);
            if (invalid.Count > 0)
            {
                return EmailSendResult.Fail("INVALID_ADDRESS", $"收件人地址格式错误：{string.Join(",", invalid.Take(5))}");
            }

            // 去重：同一地址同时出现在 To/Cc/Bcc 时只保留优先级最高的那一组，避免重复投递
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            to = to.Where(x => seen.Add(x)).ToList();
            cc = cc.Where(x => seen.Add(x)).ToList();
            bcc = bcc.Where(x => seen.Add(x)).ToList();

            if (to.Count == 0 && cc.Count == 0 && bcc.Count == 0)
            {
                return EmailSendResult.Fail("NO_RECIPIENT", "收件人不能为空");
            }

            // 附件总量校验放在建 MIME 之前，便于返回明确错误码
            var totalBytes = message.Attachments?.Sum(GetAttachmentLength) ?? 0;
            var maxBytes = options.MaxAttachmentMB > 0 ? options.MaxAttachmentMB * 1024L * 1024L : long.MaxValue;
            if (totalBytes > maxBytes)
            {
                return EmailSendResult.Fail("ATTACHMENT_TOO_LARGE", $"附件总大小超过限制（{options.MaxAttachmentMB}MB）");
            }

            mime = BuildMessage(options, message, to, cc, bcc, resources);
            if (mime == null)
            {
                return EmailSendResult.Fail("ATTACHMENT_NOT_FOUND", "附件文件不存在或不可读");
            }

            return null;
        }

        private static EmailSendResult ValidateBasicOptions(MailOptions options)
        {
            if (options == null || string.IsNullOrWhiteSpace(options.FromEmail))
            {
                return EmailSendResult.Fail("NO_CONFIG", "未配置发件邮箱（MailOptions.FromEmail）");
            }

            return null;
        }

        private static EmailSendResult ValidateSmtpOptions(MailOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Smtp) || options.Port <= 0)
            {
                return EmailSendResult.Fail("NO_CONFIG", "未配置 SMTP 服务器或端口（MailOptions.Smtp / Port）");
            }

            return null;
        }

        /// <summary>把 prepared 中从 startIndex 开始的邮件统一标记为同一失败结果</summary>
        private static void MarkRemaining(EmailSendResult[] results,
            List<(int Index, MimeMessage Mime, List<IDisposable> Resources)> prepared, int startIndex, EmailSendResult failure)
        {
            for (var i = startIndex; i < prepared.Count; i++)
            {
                results[prepared[i].Index] = failure;
            }
        }

        private static void Fill(EmailSendResult[] results, EmailSendResult failure)
        {
            for (var i = 0; i < results.Length; i++)
            {
                results[i] = failure;
            }
        }

        private static SmtpClient CreateClient(MailOptions options)
        {
            var client = new SmtpClient
            {
                Timeout = ResolveTimeoutMs(options),
            };

            // 默认使用系统证书校验；仅内网自签 SMTP 才允许显式跳过
            if (options.IgnoreCertificateErrors)
            {
                client.ServerCertificateValidationCallback = (s, certificate, chain, errors) => true;
            }

            return client;
        }

        private static async Task ConnectAsync(SmtpClient client, MailOptions options, CancellationToken cancellationToken)
        {
            await client.ConnectAsync(options.Smtp, options.Port, ResolveSocketOptions(options), cancellationToken)
                .ConfigureAwait(false);

            // 未配置密码时按匿名中继处理（部分内网 SMTP 不需要认证）
            if (!string.IsNullOrWhiteSpace(options.Password))
            {
                await client.AuthenticateAsync(Encoding.UTF8, options.FromEmail, options.Password, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        private static async Task DisconnectAsync(SmtpClient client, CancellationToken cancellationToken)
        {
            try
            {
                await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // 断开失败不影响投递结果，连接最终由 Dispose 释放
            }
        }

        private static EmailSendResult MapException(Exception ex, MailOptions options)
        {
            return ex switch
            {
                OperationCanceledException => EmailSendResult.Fail("TIMEOUT", $"邮件发送超时或已取消（{options.TimeoutSeconds}秒）"),
                MailKit.Security.AuthenticationException => EmailSendResult.Fail("AUTH_FAILED", ex.Message),
                SmtpCommandException command => EmailSendResult.Fail($"SMTP_{(int)command.StatusCode}", command.Message),
                SmtpProtocolException => EmailSendResult.Fail("SMTP_PROTOCOL", ex.Message),
                ServiceNotConnectedException => EmailSendResult.Fail("NOT_CONNECTED", ex.Message),
                _ => EmailSendResult.Fail("SEND_ERROR", ex.Message),
            };
        }

        private static void DisposeAll(List<IDisposable> resources)
        {
            foreach (var item in resources)
            {
                try { item.Dispose(); } catch { /* 释放失败不影响结果 */ }
            }
        }

        /// <summary>
        /// 构建 MimeMessage。附件流/文件句柄由调用方通过 attachments 统一释放。
        /// 返回 null 表示附件不可用。
        /// </summary>
        private static MimeMessage BuildMessage(MailOptions options, EmailMessage message,
            List<string> to, List<string> cc, List<string> bcc, List<IDisposable> attachments)
        {
            var mime = new MimeMessage();
            var displayName = string.IsNullOrWhiteSpace(options.FromName) ? options.FromEmail : options.FromName;
            mime.From.Add(new MailboxAddress(displayName, options.FromEmail));

            foreach (var item in to) mime.To.Add(new MailboxAddress(string.Empty, item));
            foreach (var item in cc) mime.Cc.Add(new MailboxAddress(string.Empty, item));
            foreach (var item in bcc) mime.Bcc.Add(new MailboxAddress(string.Empty, item));

            mime.Subject = string.IsNullOrWhiteSpace(message.Subject) ? "(无主题)" : message.Subject;
            mime.Date = DateTimeOffset.Now;
            mime.MessageId = MimeUtils.GenerateMessageId();

            // 正文：HTML + 纯文本双版本；签名统一追加在纯文本末尾（保持历史行为）
            var alternative = new MultipartAlternative();
            if (!string.IsNullOrWhiteSpace(message.HtmlBody))
            {
                alternative.Add(new TextPart(TextFormat.Html) { Text = message.HtmlBody });
            }

            var plain = message.TextBody;
            if (!string.IsNullOrWhiteSpace(options.Signature))
            {
                plain = string.IsNullOrWhiteSpace(plain) ? options.Signature : $"{plain}\r\n\r\n{options.Signature}";
            }
            if (!string.IsNullOrWhiteSpace(plain) || alternative.Count == 0)
            {
                alternative.Add(new TextPart(TextFormat.Plain) { Text = plain ?? string.Empty });
            }

            MimeEntity body = alternative;
            if (message.Attachments != null && message.Attachments.Count > 0)
            {
                var mixed = new Multipart("mixed");
                mixed.Add(alternative);

                foreach (var attachment in message.Attachments)
                {
                    var part = CreateAttachmentPart(attachment, attachments);
                    if (part == null) return null;
                    mixed.Add(part);
                }
                body = mixed;
            }

            mime.Body = body;
            return mime;
        }

        private static MimePart CreateAttachmentPart(EmailAttachment attachment, List<IDisposable> disposables)
        {
            if (attachment == null || string.IsNullOrWhiteSpace(attachment.FilePath) || !File.Exists(attachment.FilePath))
            {
                return null;
            }

            var stream = File.OpenRead(attachment.FilePath);
            disposables.Add(stream);

            // 修复历史 Bug：多附件时文件名取错（原来对整串 path 取 GetFileName）
            var fileName = string.IsNullOrWhiteSpace(attachment.FileName)
                ? Path.GetFileName(attachment.FilePath)
                : attachment.FileName;

            return new MimePart(MimeTypes.GetMimeType(fileName))
            {
                Content = new MimeContent(stream, ContentEncoding.Default),
                ContentTransferEncoding = ContentEncoding.Base64,
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                FileName = fileName,
            };
        }

        private static long GetAttachmentLength(EmailAttachment attachment)
        {
            if (attachment == null || string.IsNullOrWhiteSpace(attachment.FilePath)) return 0;

            try { return new FileInfo(attachment.FilePath).Length; } catch { return 0; }
        }

        /// <summary>
        /// 解析地址列表，兼容 "张三 &lt;a@b.com&gt;" 与 "a@b.com"；返回解析后的纯地址，非法地址收集到 invalid。
        /// </summary>
        private static List<string> ParseAddresses(IEnumerable<string> source, out List<string> invalid)
        {
            invalid = new List<string>();
            var result = new List<string>();
            if (source == null) return result;

            foreach (var raw in source)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                // 允许 "a@b.com;c@d.com" 这类误传格式，拆分规则统一由 EmailTextUtil 负责
                foreach (var value in EmailTextUtil.SplitList(raw))
                {
                    if (MailboxAddress.TryParse(value, out var mailbox)
                        && !string.IsNullOrWhiteSpace(mailbox.Address))
                    {
                        result.Add(mailbox.Address);
                    }
                    else
                    {
                        invalid.Add(value);
                    }
                }
            }

            return result;
        }

        private static int ResolveTimeoutMs(MailOptions options)
        {
            var seconds = options.TimeoutSeconds > 0
                ? Math.Min(options.TimeoutSeconds, 600)
                : DefaultTimeoutSeconds;
            return seconds * 1000;
        }

        /// <summary>
        /// 安全连接方式：显式配置优先（Auto/StartTls/StartTlsWhenAvailable/SslOnConnect/None）。
        /// 未配置时按历史 UseSsl 映射：true→Auto（465 直连 TLS、587 自动 STARTTLS），false→None。
        /// </summary>
        private static SecureSocketOptions ResolveSocketOptions(MailOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.Security)
                && Enum.TryParse<SecureSocketOptions>(options.Security.Trim(), true, out var parsed))
            {
                return parsed;
            }

            return options.UseSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;
        }
    }
}
