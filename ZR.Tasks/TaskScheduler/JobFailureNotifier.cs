using Infrastructure;
using Infrastructure.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZR.Common;
using ZR.Common.Email;
using ZR.ServiceCore.Email;

namespace ZR.Tasks.TaskScheduler
{
    /// <summary>
    /// 计划任务失败通知：按 appsettings 的 QuartzTask 节点选择渠道（企业微信 / 邮件）。
    ///
    /// 设计约束：
    /// 1) 通知失败绝不影响任务本身的执行结果 —— 所有异常在此吞掉，只记 Error 日志；
    /// 2) 必须异步 —— WxNoticeHelper 内部是同步 HTTP，在 Job 执行线程直调会拖长任务耗时；
    /// 3) 默认关闭（NotifyOnFailure=false），避免升级后突然产生大量外部消息。
    /// </summary>
    public static class JobFailureNotifier
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>通知冷却键前缀，按「任务+租户」维度</summary>
        private const string CooldownKeyPrefix = "zr:job:notify:";

        private static string CooldownKey(string taskId, string tenantId)
            => $"{CooldownKeyPrefix}{taskId}:{tenantId ?? "-"}";

        /// <summary>
        /// 发送失败通知。未开启通知或未配置渠道时直接返回。
        /// </summary>
        /// <param name="taskId">任务ID（JobKey.Name）</param>
        /// <param name="taskName">任务名称，模板已删除时传 null</param>
        /// <param name="tenantId">失败所属租户</param>
        /// <param name="message">失败原因</param>
        /// <param name="exception">原始异常，可为 null</param>
        public static void Notify(string taskId, string taskName, string tenantId, string message, Exception exception)
        {
            try
            {
                var section = App.Configuration.GetSection("QuartzTask");
                if (!section.GetValue("NotifyOnFailure", false))
                {
                    return;
                }

                // 冷却窗口：秒级任务（如每 10 秒一次）持续失败时，若每次都发通知会形成风暴。
                // 同一「任务+租户」在窗口内只通知一次；开启 Redis 时该占位跨节点共享，
                // 多实例部署也只会有一个实例发出通知。
                var cooldownMinutes = Math.Max(0, section.GetValue("NotifyCooldownMinutes", 30));
                if (cooldownMinutes > 0
                    && !CacheStore.Default.TrySet(CooldownKey(taskId, tenantId), 1, cooldownMinutes))
                {
                    logger.Debug($"计划任务失败通知处于冷却期，跳过: task={taskId}, tenant={tenantId}");
                    return;
                }

                var channels = section.GetSection("NotifyChannels").Get<string[]>();
                if (channels == null || channels.Length == 0)
                {
                    return;
                }

                var title = $"计划任务执行失败：{taskName ?? taskId}";
                var content = BuildContent(taskId, tenantId, message, exception);

                foreach (var channel in channels.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()))
                {
                    if (channel.Equals("wx", StringComparison.OrdinalIgnoreCase))
                    {
                        NotifyWx(title, content);
                    }
                    else if (channel.Equals("email", StringComparison.OrdinalIgnoreCase))
                    {
                        NotifyEmail(section, title, content);
                    }
                    else
                    {
                        logger.Warn($"未识别的计划任务通知渠道：{channel}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"计划任务失败通知发送异常: task={taskId}");
            }
        }

        /// <summary>
        /// 任务执行成功时清除失败通知冷却。
        /// 否则「失败 → 短时间恢复 → 再次失败」会被上一次的冷却窗口吞掉，导致新故障漏报。
        /// </summary>
        public static void ResetCooldown(string taskId, string tenantId)
        {
            try
            {
                CacheStore.Default.Remove(CooldownKey(taskId, tenantId));
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"清除计划任务通知冷却失败: task={taskId}");
            }
        }

        private static string BuildContent(string taskId, string tenantId, string message, Exception exception)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"任务ID：{taskId}");
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                sb.AppendLine($"租户：{tenantId}");
            }
            sb.AppendLine($"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"原因：{message}");

            if (exception != null)
            {
                var detail = exception.ToString();
                if (detail.Length > 1500)
                {
                    detail = detail[..1500];
                }
                sb.AppendLine($"异常：{detail}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 企业微信：SendMsg 是同步 HTTP，放到线程池执行，避免阻塞 Job 线程。
        /// </summary>
        private static void NotifyWx(string title, string content)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    var (code, msg) = WxNoticeHelper.SendMsg(title, content);
                    if (code == 0)
                    {
                        logger.Warn($"企业微信通知未发送：{msg}");
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "企业微信通知发送异常");
                }
            });
        }

        /// <summary>
        /// 邮件：走 IEmailSendService.EnqueueDetached（本身落库 + 后台队列，非阻塞）。
        /// 收件人取 QuartzTask:NotifyEmails，未配置时不发送并告警。
        /// </summary>
        private static void NotifyEmail(IConfigurationSection section, string title, string content)
        {
            var recipients = (section.GetValue<string>("NotifyEmails") ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (recipients.Count == 0)
            {
                logger.Warn("计划任务邮件通知已开启，但 QuartzTask:NotifyEmails 未配置收件人");
                return;
            }

            _ = Task.Run(() =>
            {
                try
                {
                    // 必须新建 scope：IEmailSendService 的依赖链含 scoped 服务，
                    // 从根容器直接解析会抛 "Cannot resolve ... from root provider"。
                    // 定时任务运行在 Quartz 线程池，没有请求作用域，只能自建。
                    var provider = InternalApp.ServiceProvider;
                    if (provider == null)
                    {
                        logger.Warn("计划任务邮件通知跳过：DI 容器未初始化");
                        return;
                    }

                    using var scope = provider.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IEmailSendService>();
                    service.EnqueueDetached(new EmailMessage
                    {
                        To = recipients,
                        Subject = title,
                        TextBody = content
                    }, "job");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "计划任务邮件通知入队失败");
                }
            });
        }
    }
}
