using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Model;
using Microsoft.Extensions.Options;
using ZR.Common.Email;
using ZR.Model.Models;
using ZR.Repository;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.Email
{
    /// <summary>
    /// 邮件发送编排：落库（email_log）→ 后台队列 → 回写结果/安排重试。
    /// 业务代码统一走本接口，不直接调用 IEmailSender。
    /// </summary>
    public interface IEmailSendService
    {
        /// <summary>
        /// 后台通知用：立即返回，落库与入队在独立数据库连接上执行（不复用请求级连接）。
        /// 用于工作流/双因子这类在后台线程触发、且可能处于业务事务中的场景。
        /// </summary>
        void EnqueueDetached(EmailMessage message, string emailType = "notify");

        /// <summary>EnqueueDetached 的模板版本（模板读取同样使用独立连接）</summary>
        void EnqueueTemplateDetached(string code, IEnumerable<string> to, IDictionary<string, object> data,
            string emailType = "notify", string fromName = null);

        /// <summary>
        /// 「立即发送」入口：校验发件配置与附件、组装邮件并同步发送。
        /// 参数/配置类错误抛 CustomException，投递结果通过 EmailSendResult 返回。
        /// </summary>
        Task<EmailSendResult> SendFromDtoAsync(SendEmailDto dto);

        /// <summary>只落库不发送（SendEmailDto.IsSend=false 的草稿场景）；参数或附件不合法抛 CustomException</summary>
        EmailLog CreateDraft(SendEmailDto dto);

        /// <summary>从发送记录重发：重置为待发送并入队，返回处理条数</summary>
        int Resend(long[] ids);
    }

    /// <summary>
    /// 邮件发送编排实现。
    /// 关键约束：
    /// 1) 先落库再发送，进程重启后可由后台扫描恢复；
    /// 2) 发送失败不抛异常，通过 EmailSendResult 返回并由调度器决定重试；
    /// 3) 落库失败时不阻断业务：降级为后台直接发送（无记录，但邮件不丢）。
    /// </summary>
    [AppService(ServiceType = typeof(IEmailSendService), ServiceLifetime = LifeTime.Transient)]
    public class EmailSendService : IEmailSendService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly IEmailSender _emailSender;
        private readonly IEmailQueue _emailQueue;
        private readonly IEmailLogService _emailLogService;
        private readonly IEmailTplService _emailTplService;
        private readonly IEmailAttachmentResolver _attachmentResolver;
        private readonly IOptions<OptionsSetting> _options;

        public EmailSendService(IEmailSender emailSender, IEmailQueue emailQueue,
            IEmailLogService emailLogService, IEmailTplService emailTplService,
            IEmailAttachmentResolver attachmentResolver, IOptions<OptionsSetting> options)
        {
            _emailSender = emailSender;
            _emailQueue = emailQueue;
            _emailLogService = emailLogService;
            _emailTplService = emailTplService;
            _attachmentResolver = attachmentResolver;
            _options = options;
        }

        /// <summary>同步发送并落库（仅由「立即发送」入口调用，失败不自动重试）</summary>
        private async Task<EmailSendResult> SendNowCoreAsync(EmailMessage message, string emailType = "manual",
            CancellationToken cancellationToken = default)
        {
            if (message == null)
            {
                return EmailSendResult.Fail("INVALID_PARAM", "邮件内容不能为空");
            }

            // 同步发送直接置「发送中」且标记 Direct：进程若在投递途中退出，只会被标记失败，不会被自动重发
            var log = TryCreateLog(message, emailType, EmailSendStatus.Sending, EmailDispatchMode.Direct);

            EmailSendResult result;
            try
            {
                result = await _emailSender.SendAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result = EmailSendResult.Fail("SEND_ERROR", ex.Message);
            }

            if (log != null)
            {
                try
                {
                    // 同步发送不安排自动重试：用户已看到结果，自动补发容易造成重复投递
                    _emailLogService.UpdateSendResult(log.Id, result);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"邮件发送结果回写失败 LogId={log.Id}");
                }
            }

            return result;
        }

        public void EnqueueDetached(EmailMessage message, string emailType = "notify")
        {
            if (message == null) return;

            _ = Task.Run(() =>
            {
                try
                {
                    // 后台线程使用独立连接：不能复用请求线程构造时捕获的 DbContext
                    EnqueueCore(message, emailType, CreateDetachedDb());
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"邮件通知后台入队失败，降级直发 Subject={message.Subject}");
                    SendInBackground(message);
                }
            });
        }

        public void EnqueueTemplateDetached(string code, IEnumerable<string> to, IDictionary<string, object> data,
            string emailType = "notify", string fromName = null)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    var db = CreateDetachedDb();
                    if (!TryBuildTemplateMessage(code, to, data, fromName, out var message, out var error, db))
                    {
                        logger.Warn($"邮件模板不可用，通知未发送：Code={code}，原因={error}");
                        return;
                    }

                    EnqueueCore(message, emailType, db);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"邮件模板通知后台入队失败 Code={code}");
                }
            });
        }

        /// <summary>
        /// 后台任务专用连接：CopyNew + 显式系统库，避免与请求事务共用同一连接。
        /// email_log / emailTpl 固定落系统库（IMainDbEntity + [Tenant("0")]）。
        /// </summary>
        private static ISqlSugarClient CreateDetachedDb()
        {
            return BackgroundDbHelper.CreateBackgroundDb(App.SystemDbConfigId);
        }

        /// <summary>
        /// 「立即发送」入口：校验发件配置/附件、组装消息并同步发送。
        /// 参数与配置类错误直接抛 CustomException（前端可见具体原因），投递失败通过 EmailSendResult 返回。
        /// </summary>
        public async Task<EmailSendResult> SendFromDtoAsync(SendEmailDto dto)
        {
            if (dto == null) throw new CustomException(ResultCode.PARAM_ERROR, "请求参数不完整");

            var mailOptions = EmailOptionsResolver.Resolve(_options?.Value?.MailOptions, dto.FromName);
            if (mailOptions == null || string.IsNullOrWhiteSpace(mailOptions.FromEmail))
            {
                throw new CustomException(ResultCode.CUSTOM_ERROR, "请配置邮箱信息");
            }

            var message = BuildMessageFromDto(dto);
            return await SendNowCoreAsync(message, "manual").ConfigureAwait(false);
        }

        /// <summary>只落库不发送（草稿）；附件不合法直接抛 CustomException</summary>
        public EmailLog CreateDraft(SendEmailDto dto)
        {
            if (dto == null) throw new CustomException(ResultCode.PARAM_ERROR, "请求参数不完整");

            var message = BuildMessageFromDto(dto);
            return TryCreateLog(message, "manual", EmailSendStatus.Draft, EmailDispatchMode.Queue);
        }

        /// <summary>把 SendEmailDto 组装成邮件消息（附件地址在此统一校验）</summary>
        private EmailMessage BuildMessageFromDto(SendEmailDto dto)
        {
            var message = new EmailMessage
            {
                FromName = dto.FromName,
                To = EmailTextUtil.SplitList(dto.ToUser),
                Cc = EmailTextUtil.SplitList(dto.CcUser),
                Bcc = EmailTextUtil.SplitList(dto.BccUser),
                Subject = dto.Subject,
                TextBody = dto.Content,
                HtmlBody = dto.HtmlContent,
            };

            var attachments = new List<EmailAttachment>();
            var error = ResolveAttachments(EmailTextUtil.SplitList(dto.FileUrl), attachments);
            if (error != null) throw new CustomException(ResultCode.CUSTOM_ERROR, error);

            message.Attachments.AddRange(attachments);
            return message;
        }

        /// <summary>附件地址校验：只允许上传目录内的文件，返回 null 表示全部通过</summary>
        private string ResolveAttachments(IEnumerable<string> sources, List<EmailAttachment> target)
        {
            if (sources == null || target == null) return null;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var source in sources)
            {
                if (string.IsNullOrWhiteSpace(source)) continue;

                if (!_attachmentResolver.TryResolve(source, out var fullPath, out var error))
                {
                    return error;
                }

                if (seen.Add(fullPath))
                {
                    target.Add(new EmailAttachment { FilePath = fullPath });
                }
            }

            return null;
        }

        public int Resend(long[] ids)
        {
            if (ids == null || ids.Length == 0) return 0;

            var count = _emailLogService.ResetForResend(ids);
            if (count <= 0) return 0;

            foreach (var log in _emailLogService.GetListByIds(ids))
            {
                if (!_emailQueue.TryEnqueue(log.Id))
                {
                    logger.Warn($"邮件队列已满，重发记录改由后台扫描补发 LogId={log.Id}");
                    break;
                }
            }

            return count;
        }

        #region 内部方法

        /// <summary>
        /// 落库为待发送并尝试入队；落库失败时降级为后台直发（无记录，但通知不丢）。
        /// db 由调用方提供：后台任务必须传独立连接（见 EnqueueDetached）。
        /// </summary>
        private void EnqueueCore(EmailMessage message, string emailType, ISqlSugarClient db)
        {
            var log = TryCreateLog(message, emailType, EmailSendStatus.Pending, EmailDispatchMode.Queue, db);
            if (log == null)
            {
                SendInBackground(message);
                return;
            }

            if (!_emailQueue.TryEnqueue(log.Id))
            {
                logger.Warn($"邮件队列已满，改由后台扫描补发 LogId={log.Id} Subject={message.Subject}");
            }
        }

        /// <summary>
        /// 落库一条发送记录；失败返回 null（调用方决定降级策略）。
        /// db 为空时使用请求级 IEmailLogService；后台任务必须传入独立连接（见 EnqueueDetached）。
        /// </summary>
        private EmailLog TryCreateLog(EmailMessage message, string emailType, int status, int dispatchMode,
            ISqlSugarClient db = null)
        {
            try
            {
                var mailOptions = EmailOptionsResolver.Resolve(_options?.Value?.MailOptions, message.FromName);
                var log = new EmailLog
                {
                    FromName = string.IsNullOrWhiteSpace(message.FromName)
                        ? mailOptions?.FromName
                        : message.FromName.Trim(),
                    FromEmail = mailOptions?.FromEmail,
                    Subject = message.Subject,
                    ToEmails = EmailTextUtil.JoinList(message.To),
                    CcEmails = EmailTextUtil.JoinList(message.Cc),
                    BccEmails = EmailTextUtil.JoinList(message.Bcc),
                    EmailContent = string.IsNullOrWhiteSpace(message.HtmlBody) ? message.TextBody : message.HtmlBody,
                    FileUrl = EmailTextUtil.JoinList(message.Attachments?
                        .Where(x => !string.IsNullOrWhiteSpace(x.FilePath))
                        .Select(x => x.FilePath)),
                    EmailType = emailType,
                    DispatchMode = dispatchMode,
                    Status = status,
                    IsSend = status == EmailSendStatus.Success ? 1 : 0,
                    AddTime = DateTime.Now,
                    SendTime = EmailLog.NoSendTime,
                    LastAttemptTime = status == EmailSendStatus.Sending ? DateTime.Now : (DateTime?)null,
                    RetryCount = 0,
                };

                log.Id = db != null
                    ? db.Insertable(log).ExecuteReturnSnowflakeId()
                    : _emailLogService.InsertLog(log);

                return log;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"邮件发送记录落库失败 Subject={message.Subject} Type={emailType}");
                return null;
            }
        }

        private void SendInBackground(EmailMessage message)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailSender.SendAsync(message).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"邮件降级发送失败 Subject={message.Subject}");
                }
            });
        }

        /// <summary>
        /// 解析模板：优先取数据库启用中的模板，未配置时回退内置默认模板。
        /// </summary>
        private bool TryBuildTemplateMessage(string code, IEnumerable<string> to, IDictionary<string, object> data,
            string fromName, out EmailMessage message, out string error, ISqlSugarClient db = null)
        {
            message = null;
            error = null;

            if (string.IsNullOrWhiteSpace(code))
            {
                error = "模板编码不能为空";
                return false;
            }

            string subjectTemplate;
            string contentTemplate;
            bool isHtml;

            var tpl = GetTemplateByCode(code, db);
            if (tpl != null)
            {
                subjectTemplate = string.IsNullOrWhiteSpace(tpl.Subject) ? tpl.Name : tpl.Subject;
                contentTemplate = tpl.Content;
                isHtml = tpl.IsHtml ?? true;
            }
            else if (EmailTemplateDefaults.TryGet(code, out var defaultSubject, out var defaultContent))
            {
                subjectTemplate = defaultSubject;
                contentTemplate = defaultContent;
                isHtml = true;
            }
            else
            {
                error = $"模板不存在或已停用：{code}";
                return false;
            }

            var renderedContent = isHtml
                ? EmailTemplateRenderer.RenderHtml(contentTemplate, data)
                : EmailTemplateRenderer.Render(contentTemplate, data, htmlEncode: false);

            message = new EmailMessage
            {
                FromName = fromName,
                To = to?.ToList() ?? new List<string>(),
                Subject = EmailTemplateRenderer.Render(subjectTemplate, data, htmlEncode: false),
                HtmlBody = isHtml ? renderedContent : null,
                TextBody = isHtml ? null : renderedContent,
            };
            return true;
        }

        /// <summary>按编码取启用中的模板：请求线程走 IEmailTplService，后台任务走独立连接 db</summary>
        private EmailTpl GetTemplateByCode(string code, ISqlSugarClient db)
        {
            if (db == null)
            {
                return _emailTplService.GetByCode(code);
            }

            return db.Queryable<EmailTpl>()
                .Where(x => x.Code == code.Trim() && (x.Enabled == null || x.Enabled == true))
                .OrderBy(x => x.Id, OrderByType.Desc)
                .First();
        }

        #endregion
    }
}
