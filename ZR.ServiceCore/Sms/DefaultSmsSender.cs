using Infrastructure.Attribute;
using Infrastructure.Model;
using Microsoft.Extensions.Options;

namespace ZR.ServiceCore.Sms
{
    /// <summary>
    /// 默认短信发送实现。
    /// - SmsOptions.Enabled = false（默认）：模拟发送，仅写日志，返回成功（Simulated=true），不影响业务流程；
    /// - Enabled = true：按 Provider 分发到对应服务商实现。
    /// 对接真实服务商时，只需安装对应 SDK 并补全 SendByAliyun / SendByTencentCloud，
    /// 或另写一个 ISmsSender 实现类（[AppService(ServiceType = typeof(ISmsSender))]）替换本类注册。
    /// </summary>
    [AppService(ServiceType = typeof(ISmsSender), ServiceLifetime = LifeTime.Singleton)]
    public class DefaultSmsSender : ISmsSender
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly OptionsSetting optionsSetting;

        public DefaultSmsSender(IOptions<OptionsSetting> options)
        {
            optionsSetting = options?.Value;
        }

        public SmsSendResult Send(SmsMessage message)
        {
            return SendAsync(message).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task<SmsSendResult> SendAsync(SmsMessage message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.PhoneNum))
            {
                return Task.FromResult(SmsSendResult.Fail("INVALID_PARAM", "手机号不能为空"));
            }
            if (string.IsNullOrWhiteSpace(message.Content) && string.IsNullOrWhiteSpace(message.TemplateCode))
            {
                return Task.FromResult(SmsSendResult.Fail("INVALID_PARAM", "短信内容与模板编号至少填一项"));
            }

            var opt = optionsSetting?.SmsOptions;
            if (opt == null || !opt.Enabled)
            {
                // 模拟发送：未启用短信服务时仅记录日志，方便开发调试
                logger.Info($"【模拟短信】手机号={message.PhoneNum}，模板={message.TemplateCode}，内容={message.Content}");
                return Task.FromResult(SmsSendResult.Ok(bizId: null, simulated: true));
            }

            message.SignName ??= opt.SignName;

            return (opt.Provider?.ToLowerInvariant()) switch
            {
                "aliyun" => SendByAliyunAsync(opt, message),
                "tencentcloud" or "tencent" => SendByTencentCloudAsync(opt, message),
                _ => Task.FromResult(SmsSendResult.Fail("PROVIDER_NOT_SUPPORTED",
                    $"未知短信服务商 {opt.Provider}，请检查 SmsOptions.Provider 配置或在 DefaultSmsSender 中扩展实现")),
            };
        }

        /// <summary>
        /// 阿里云短信（待接入）。
        /// 对接步骤：NuGet 安装 AlibabaCloud.SDK.Dysmsapi20170525，
        /// 使用 opt.AccessKeyId / AccessKeySecret / Endpoint，调用 SendSmsAsync，
        /// 模板参数 message.TemplateParams 序列化为 JSON 传 TemplateParam。
        /// </summary>
        private Task<SmsSendResult> SendByAliyunAsync(SmsOptions opt, SmsMessage message)
        {
            logger.Warn($"阿里云短信尚未接入，发送被跳过：{message.PhoneNum}");
            return Task.FromResult(SmsSendResult.Fail("NOT_IMPLEMENTED", "阿里云短信尚未接入，请在 DefaultSmsSender.SendByAliyunAsync 中实现"));
        }

        /// <summary>
        /// 腾讯云短信（待接入）。
        /// 对接步骤：NuGet 安装 TencentCloudSDK.Sms，
        /// 使用 opt.AccessKeyId(SecretId) / AccessKeySecret(SecretKey) / SdkAppId / Endpoint(地域)，
        /// TemplateId=message.TemplateCode，TemplateParamSet=message.TemplateParams.Values。
        /// </summary>
        private Task<SmsSendResult> SendByTencentCloudAsync(SmsOptions opt, SmsMessage message)
        {
            logger.Warn($"腾讯云短信尚未接入，发送被跳过：{message.PhoneNum}");
            return Task.FromResult(SmsSendResult.Fail("NOT_IMPLEMENTED", "腾讯云短信尚未接入，请在 DefaultSmsSender.SendByTencentCloudAsync 中实现"));
        }
    }
}
