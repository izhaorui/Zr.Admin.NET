using NLog;
using ZR.ServiceCore.Sms;

namespace ZR.Workflow.Service.Engine
{
    /// <summary>
    /// 工作流短信通知。后台发送并兜底异常，短信失败不影响主流程。
    /// </summary>
    public class WfSmsNotifier
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly ISmsSender _smsSender;

        public WfSmsNotifier(ISmsSender smsSender)
        {
            _smsSender = smsSender;
        }

        /// <summary>
        /// 后台发送，异常在 ContinueWith 中记录日志，不会成为未观察异常
        /// </summary>
        public void Enqueue(long userId, string phone, string templateCode, string content)
        {
            Task.Run(() => SendAsync(userId, phone, templateCode, content))
                .ContinueWith(t => logger.Warn(t.Exception, $"工作流短信通知后台任务异常 userId={userId}"), TaskContinuationOptions.OnlyOnFaulted);
        }

        public Task SendAsync(long userId, string phone, string templateCode, string content)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(templateCode))
                {
                    logger.Warn("工作流短信通知跳过：未配置 SmsOptions:Templates:workflow 模板编号");
                    return Task.CompletedTask;
                }

                var result = _smsSender.Send(new SmsMessage
                {
                    PhoneNum = phone,
                    TemplateCode = templateCode,
                    TemplateParams = new Dictionary<string, string> { { "content", content } },
                    SendType = 6
                });

                if (result.Success) logger.Info($"工作流短信通知已发送 userId={userId} Simulated={result.Simulated}");
                else logger.Warn($"工作流短信通知发送失败 userId={userId} ErrorCode={result.ErrorCode} ErrorMsg={result.ErrorMsg}");
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"工作流短信通知异常 userId={userId}");
            }
            return Task.CompletedTask;
        }
    }
}
