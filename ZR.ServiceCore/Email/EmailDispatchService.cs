using Infrastructure;
using Infrastructure.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ZR.Common.Email;
using ZR.Infrastructure.Helper;
using ZR.Model.Models;
using ZR.Repository;

namespace ZR.ServiceCore.Email
{
    /// <summary>
    /// 邮件后台派发服务（HostedService）。
    ///
    /// 职责：
    /// 1) 消费内存队列，逐条真实发送并回写 email_log；
    /// 2) 启动恢复：把「待发送」记录重新入队（覆盖进程重启/队列满/队列满时未入队的场景）；
    /// 3) 定时补偿：每分钟扫描到期的失败记录做指数退避重试，重置长时间「发送中」的僵死记录。
    ///
    /// 多实例安全：发送前用「状态 待发送/失败 → 发送中」的原子更新抢占（影响行数为 0 说明已被其他实例处理），
    /// 避免两个宿主重复投递。email_log 固定落系统库，后台线程使用独立连接（CopyNew）不影响请求连接。
    /// </summary>
    public class EmailDispatchService : BackgroundService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>补偿扫描间隔</summary>
        private static readonly TimeSpan MaintainInterval = TimeSpan.FromSeconds(60);
        /// <summary>
        /// 启动恢复窗口。DispatchMode=Queue 已能排除升级前的历史数据；
        /// 只兜底最近 1 天的积压，更早的记录不再自动补发，避免停机多日后集中轰炸用户。
        /// </summary>
        private static readonly TimeSpan PendingRecoverWindow = TimeSpan.FromDays(1);
        /// <summary>「发送中」超过该时长视为进程异常退出残留</summary>
        private static readonly TimeSpan StuckSendingTimeout = TimeSpan.FromMinutes(15);
        /// <summary>单轮最多处理的补偿记录数（恢复/重试扫描）</summary>
        private const int BatchSize = 200;
        /// <summary>单批投递条数：一批共用一条 SMTP 连接，过大会拉长单次连接占用时间</summary>
        private const int SendBatchSize = 20;

        private readonly IEmailQueue _queue;
        private readonly IEmailSender _emailSender;
        private readonly IEmailAttachmentResolver _attachmentResolver;
        private readonly IOptions<OptionsSetting> _options;

        /// <summary>表结构未同步等持续性故障只完整记录一次，避免每分钟刷屏</summary>
        private bool _maintainFaultLogged;

        public EmailDispatchService(IEmailQueue queue, IEmailSender emailSender,
            IEmailAttachmentResolver attachmentResolver, IOptions<OptionsSetting> options)
        {
            _queue = queue;
            _emailSender = emailSender;
            _attachmentResolver = attachmentResolver;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.Info("邮件后台派发服务已启动");

            // 补偿循环独立运行，避免恢复扫描阻塞队列消费
            _ = Task.Run(() => MaintainLoopAsync(stoppingToken), CancellationToken.None);

            try
            {
                await foreach (var first in _queue.ReadAllAsync(stoppingToken).ConfigureAwait(false))
                {
                    // 成批消费：一次取最多 SendBatchSize 条，共用一条 SMTP 连接投递
                    var batch = new List<long>(SendBatchSize) { first };
                    while (batch.Count < SendBatchSize && _queue.TryRead(out var next))
                    {
                        batch.Add(next);
                    }

                    try
                    {
                        await ProcessBatchAsync(batch, stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"邮件后台发送异常，记录将由补偿扫描重试 Count={batch.Count}");
                    }
                    finally
                    {
                        // 无论成功/失败都移出「在途集合」，失败重试由调度器重新入队
                        foreach (var logId in batch)
                        {
                            _queue.MarkCompleted(logId);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 宿主停止：未处理的记录保持「待发送」，下次启动由恢复扫描补发
            }

            logger.Info("邮件后台派发服务已停止");
        }

        #region 发送

        private async Task ProcessBatchAsync(List<long> logIds, CancellationToken stoppingToken)
        {
            var db = CreateDb();
            var items = new List<(EmailLog Log, EmailMessage Message)>(logIds.Count);

            foreach (var logId in logIds)
            {
                // 原子抢占：只有把状态从 待发送/失败 改成 发送中 成功的实例才真正发送
                var claimed = await db.Updateable<EmailLog>()
                    .SetColumns(x => new EmailLog
                    {
                        Status = EmailSendStatus.Sending,
                        LastAttemptTime = DateTime.Now,
                    })
                    .Where(x => x.Id == logId
                        && x.DispatchMode == EmailDispatchMode.Queue
                        && (x.Status == EmailSendStatus.Pending || x.Status == EmailSendStatus.Failed))
                    .ExecuteCommandAsync().ConfigureAwait(false);

                if (claimed <= 0)
                {
                    continue;
                }

                var log = await db.Queryable<EmailLog>()
                    .Where(x => x.Id == logId)
                    .FirstAsync().ConfigureAwait(false);

                if (log == null)
                {
                    continue;
                }

                var message = BuildMessage(log, out var attachError);
                if (message == null)
                {
                    // 附件缺失/失效视为发送失败（可见、可排查），不进入批量投递
                    var failure = EmailSendResult.Fail("ATTACHMENT_NOT_FOUND", attachError);
                    await WriteResultAsync(db, log, failure, ResolveNextRetry(log, failure)).ConfigureAwait(false);
                    continue;
                }

                items.Add((log, message));
            }

            if (items.Count == 0)
            {
                return;
            }

            // 整批共用一条 SMTP 连接（SmtpEmailSender 内部按发件配置再分组）
            IReadOnlyList<EmailSendResult> results;
            try
            {
                results = await _emailSender.SendBatchAsync(items.Select(x => x.Message).ToList(), stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                results = items.Select(_ => EmailSendResult.Fail("SEND_ERROR", ex.Message)).ToList();
            }

            for (var i = 0; i < items.Count; i++)
            {
                var result = results != null && i < results.Count && results[i] != null
                    ? results[i]
                    : EmailSendResult.Fail("SEND_ERROR", "发送结果缺失");

                await WriteResultAsync(db, items[i].Log, result, ResolveNextRetry(items[i].Log, result))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>失败后按指数退避计算下次重试时间；不可重试的错误返回 null（不再进入重试扫描）</summary>
        private DateTime? ResolveNextRetry(EmailLog log, EmailSendResult result)
        {
            if (result.Success) return null;

            var maxRetry = EmailOptionsResolver.ResolveMaxRetry(_options?.Value?.MailOptions, log.FromName);
            var retryCount = (log.RetryCount ?? 0) + 1;
            return IsRetryable(result.ErrorCode) && retryCount < maxRetry
                ? DateTime.Now.AddSeconds(BackoffSeconds(retryCount))
                : (DateTime?)null;
        }

        private async Task WriteResultAsync(ISqlSugarClient db, EmailLog log, EmailSendResult result, DateTime? nextRetry)
        {
            if (result.Success)
            {
                await db.Updateable<EmailLog>()
                    .SetColumns(x => new EmailLog
                    {
                        Status = EmailSendStatus.Success,
                        IsSend = 1,
                        SendResult = result.Simulated ? "simulated" : "ok",
                        ErrorCode = "",
                        MessageId = result.MessageId,
                        NextRetryTime = null,
                        SendTime = DateTime.Now,
                    })
                    .Where(x => x.Id == log.Id)
                    .ExecuteCommandAsync().ConfigureAwait(false);

                logger.Info($"邮件发送成功 LogId={log.Id} Type={log.EmailType} To={MaskRecipients(log.ToEmails)} MessageId={result.MessageId}");
                return;
            }

            await db.Updateable<EmailLog>()
                .SetColumns(x => new EmailLog
                {
                    Status = EmailSendStatus.Failed,
                    IsSend = 0,
                    SendResult = $"[{result.ErrorCode}]{result.ErrorMsg}",
                    ErrorCode = result.ErrorCode,
                    MessageId = result.MessageId,
                    NextRetryTime = nextRetry,
                    RetryCount = x.RetryCount + 1,
                })
                .Where(x => x.Id == log.Id)
                .ExecuteCommandAsync().ConfigureAwait(false);

            if (nextRetry.HasValue)
            {
                logger.Warn($"邮件发送失败，将重试 LogId={log.Id} Type={log.EmailType} Code={result.ErrorCode} 下次重试={nextRetry:yyyy-MM-dd HH:mm:ss} Msg={result.ErrorMsg}");
            }
            else
            {
                logger.Warn($"邮件发送失败，不再重试 LogId={log.Id} Type={log.EmailType} Code={result.ErrorCode} Msg={result.ErrorMsg}");
            }
        }

        /// <summary>
        /// 由发送记录还原邮件消息。只有 FilePath 形式的附件能跨重启恢复；
        /// 任一附件不可用都返回 null 并给出原因（交由调用方标记失败），避免"附件丢了但邮件显示成功"。
        /// </summary>
        private EmailMessage BuildMessage(EmailLog log, out string error)
        {
            error = null;

            var content = log.EmailContent ?? string.Empty;
            var message = new EmailMessage
            {
                FromName = log.FromName,
                To = EmailTextUtil.SplitList(log.ToEmails),
                Cc = EmailTextUtil.SplitList(log.CcEmails),
                Bcc = EmailTextUtil.SplitList(log.BccEmails),
                Subject = log.Subject,
                // 历史记录只存正文：含标签按 HTML 发送，否则按纯文本发送
                HtmlBody = content.Contains('<') && content.Contains('>') ? content : null,
                TextBody = content.Contains('<') && content.Contains('>') ? null : content,
            };

            foreach (var file in EmailTextUtil.SplitList(log.FileUrl))
            {
                // 落库的是内部物理路径，使用「可信路径」校验（不接受 URL，但仍限制在上传根目录内）
                if (!_attachmentResolver.TryResolveStoredPath(file, out var fullPath, out var reason))
                {
                    error = $"附件不可用（{reason}）：{Path.GetFileName(file)}";
                    return null;
                }

                message.Attachments.Add(new EmailAttachment { FilePath = fullPath });
            }

            return message;
        }

        #endregion

        #region 补偿扫描

        private async Task MaintainLoopAsync(CancellationToken stoppingToken)
        {
            // 启动即恢复一次，之后每分钟一轮
            await MaintainOnceAsync(stoppingToken).ConfigureAwait(false);

            using var timer = new PeriodicTimer(MaintainInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    await MaintainOnceAsync(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // 宿主停止
            }
        }

        private async Task MaintainOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                var db = CreateDb();
                await ResetStuckAsync(db).ConfigureAwait(false);
                await RecoverPendingAsync(db).ConfigureAwait(false);
                await EnqueueRetryableAsync(db).ConfigureAwait(false);
                _maintainFaultLogged = false;
            }
            catch (Exception ex)
            {
                if (!_maintainFaultLogged)
                {
                    logger.Error(ex, "邮件补偿扫描失败。若为字段/表不存在，请执行 --initdb 或后台「数据库同步」补齐 email_log / emailTpl 表结构");
                    _maintainFaultLogged = true;
                }
            }
        }

        private async Task ResetStuckAsync(ISqlSugarClient db)
        {
            var before = DateTime.Now.Subtract(StuckSendingTimeout);

            // 队列投递：允许恢复重投（进程退出时最多重复一次，属可接受的 at-least-once 语义）
            var requeued = await db.Updateable<EmailLog>()
                .SetColumns(x => new EmailLog { Status = EmailSendStatus.Pending })
                .Where(x => x.Status == EmailSendStatus.Sending
                    && x.DispatchMode == EmailDispatchMode.Queue
                    && (x.LastAttemptTime == null || x.LastAttemptTime < before))
                .ExecuteCommandAsync().ConfigureAwait(false);

            // 同步直发：调用方正在等待结果，绝不能自动重发；超时直接标记失败
            var interrupted = await db.Updateable<EmailLog>()
                .SetColumns(x => new EmailLog
                {
                    Status = EmailSendStatus.Failed,
                    IsSend = 0,
                    ErrorCode = "SEND_INTERRUPTED",
                    SendResult = "[SEND_INTERRUPTED]发送过程中进程中断，已标记失败（不自动重发）",
                    NextRetryTime = null,
                })
                .Where(x => x.Status == EmailSendStatus.Sending
                    && (x.DispatchMode == null || x.DispatchMode == EmailDispatchMode.Direct)
                    && (x.LastAttemptTime == null || x.LastAttemptTime < before))
                .ExecuteCommandAsync().ConfigureAwait(false);

            if (requeued > 0)
            {
                logger.Warn($"邮件补偿：{requeued} 条队列投递超时，已重置为待发送");
            }
            if (interrupted > 0)
            {
                logger.Warn($"邮件补偿：{interrupted} 条同步直发中断，已标记失败（不自动重发）");
            }
        }

        private async Task RecoverPendingAsync(ISqlSugarClient db)
        {
            var createdAfter = DateTime.Now.Subtract(PendingRecoverWindow);
            // EmailType 非空用于识别「新流程写入的记录」：升级前遗留的 IsSend=0 历史数据不会被误补发；
            // DispatchMode=Queue 排除同步直发记录（后者只按发送结果收尾，不参与恢复）
            var list = await db.Queryable<EmailLog>()
                .Where(x => x.Status == EmailSendStatus.Pending
                    && x.DispatchMode == EmailDispatchMode.Queue
                    && x.EmailType != null
                    && x.AddTime >= createdAfter)
                .OrderBy(x => x.Id)
                .Take(BatchSize)
                .ToListAsync().ConfigureAwait(false);

            var enqueued = 0;
            foreach (var item in list)
            {
                // 去重：已在队列中/正在发送的记录不重复入队（TryEnqueue 返回 false）
                if (_queue.TryEnqueue(item.Id))
                {
                    enqueued++;
                }
            }

            if (enqueued > 0)
            {
                logger.Info($"邮件补偿：恢复 {enqueued} 条待发送记录（跳过在途 {list.Count - enqueued} 条）");
            }
        }

        private async Task EnqueueRetryableAsync(ISqlSugarClient db)
        {
            var now = DateTime.Now;
            var maxRetry = EmailOptionsResolver.ResolveGlobalMaxRetry(_options?.Value?.MailOptions);

            var list = await db.Queryable<EmailLog>()
                .Where(x => x.Status == EmailSendStatus.Failed
                    && x.DispatchMode == EmailDispatchMode.Queue
                    && x.RetryCount < maxRetry
                    && x.NextRetryTime != null
                    && x.NextRetryTime <= now)
                .OrderBy(x => x.Id)
                .Take(BatchSize)
                .ToListAsync().ConfigureAwait(false);

            foreach (var item in list)
            {
                // 先把状态改回待发送，避免同一轮被重复入队/被其他实例抢占后状态不一致
                var reset = await db.Updateable<EmailLog>()
                    .SetColumns(x => new EmailLog { Status = EmailSendStatus.Pending })
                    .Where(x => x.Id == item.Id && x.Status == EmailSendStatus.Failed)
                    .ExecuteCommandAsync().ConfigureAwait(false);
                if (reset > 0)
                {
                    _queue.TryEnqueue(item.Id);
                }
            }

            if (list.Count > 0)
            {
                logger.Info($"邮件补偿：{list.Count} 条失败记录进入重试");
            }
        }

        #endregion

        #region 辅助

        /// <summary>
        /// 后台线程独立连接：email_log 固定系统库（CopyNew + 显式 ConfigId），
        /// 避免与请求线程共用连接/事务上下文。
        /// </summary>
        private static ISqlSugarClient CreateDb()
        {
            return BackgroundDbHelper.CreateBackgroundDb(App.SystemDbConfigId);
        }

        /// <summary>是否值得重试：配置/地址类错误重试无意义；SMTP 4xx 为临时错误，5xx 为永久错误。</summary>
        private static bool IsRetryable(string errorCode)
        {
            if (string.IsNullOrWhiteSpace(errorCode)) return true;

            switch (errorCode)
            {
                case "NO_CONFIG":
                case "NO_RECIPIENT":
                case "INVALID_ADDRESS":
                case "INVALID_PARAM":
                case "AUTH_FAILED":
                case "ATTACHMENT_NOT_FOUND":
                case "ATTACHMENT_TOO_LARGE":
                case "TEMPLATE_NOT_FOUND":
                case "SEND_INTERRUPTED":
                    return false;
            }

            // SMTP_421 这类 4xx 临时错误可重试，5xx 永久失败不重试
            if (errorCode.StartsWith("SMTP_", StringComparison.OrdinalIgnoreCase)
                && errorCode.Length > 5 && char.IsDigit(errorCode[5]))
            {
                return errorCode[5] == '4';
            }

            return true;
        }

        /// <summary>指数退避：1 分钟起，每次翻倍，最长 30 分钟。</summary>
        private static int BackoffSeconds(int retryCount)
        {
            var seconds = 60 * Math.Pow(2, Math.Max(0, retryCount - 1));
            return (int)Math.Min(seconds, 1800);
        }

        private static string MaskRecipients(string value)
        {
            var list = EmailTextUtil.SplitList(value).Take(3).Select(MaskUtil.MaskEmail).ToList();
            return list.Count == 0 ? string.Empty : string.Join(",", list);
        }

        #endregion
    }

    /// <summary>
    /// 邮件能力宿主注册。
    /// [AppService] 负责注册 IEmailSender / IEmailQueue / IEmailSendService 等业务服务，
    /// HostedService 不在扫描范围，需由各宿主显式调用本扩展方法。
    /// </summary>
    public static class EmailServiceSetup
    {
        public static IServiceCollection AddEmailDispatch(this IServiceCollection services)
        {
            services.AddHostedService<EmailDispatchService>();
            return services;
        }
    }
}
