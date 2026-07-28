namespace ZR.ServiceCore.Sms
{
    /// <summary>
    /// 短信发送统一抽象。
    /// 后续对接短信服务商（阿里云/腾讯云等）时：
    /// 1. 在 appsettings.json 的 SmsOptions 中配置 Enabled=true、Provider 及密钥；
    /// 2. 在 DefaultSmsSender 对应 Provider 分支中实现真实发送（或新写一个实现类替换注册）。
    /// 业务侧只依赖本接口，无需改动调用代码。
    /// </summary>
    public interface ISmsSender
    {
        /// <summary>发送短信（异步）</summary>
        Task<SmsSendResult> SendAsync(SmsMessage message);

        /// <summary>发送短信（同步便捷方法）</summary>
        SmsSendResult Send(SmsMessage message);
    }

    /// <summary>
    /// 短信消息。支持两种模式：
    /// 1. 直发内容：填 Content（适合验证码/通知直发，部分服务商不支持自由内容）；
    /// 2. 模板发送：填 TemplateCode + TemplateParams（国内服务商主流方式）。
    /// </summary>
    public class SmsMessage
    {
        /// <summary>接收手机号（单个）</summary>
        public string PhoneNum { get; set; }

        /// <summary>短信内容（直发模式）</summary>
        public string Content { get; set; }

        /// <summary>模板编号/模板ID（模板模式，如阿里云 SMS_xxx、腾讯云模板ID）</summary>
        public string TemplateCode { get; set; }

        /// <summary>模板参数（模板模式）</summary>
        public Dictionary<string, string> TemplateParams { get; set; }

        /// <summary>短信签名，为空时使用 SmsOptions.SignName</summary>
        public string SignName { get; set; }

        /// <summary>发送类型 1登录 2注册 3找回密码 6通知</summary>
        public int SendType { get; set; } = 6;
    }

    /// <summary>短信发送状态（对应 SmsCodeLog.SendStatus）</summary>
    public static class SmsSendStatus
    {
        /// <summary>待发送/发送中</summary>
        public const int Pending = 0;
        /// <summary>发送成功</summary>
        public const int Success = 1;
        /// <summary>发送失败</summary>
        public const int Failed = 2;
    }

    /// <summary>短信发送结果</summary>
    public class SmsSendResult
    {
        /// <summary>是否发送成功</summary>
        public bool Success { get; set; }

        /// <summary>是否为模拟发送（短信服务未启用时为 true，仅记录日志不真实发送）</summary>
        public bool Simulated { get; set; }

        /// <summary>服务商返回的回执ID/BizId，便于查询发送状态</summary>
        public string BizId { get; set; }

        /// <summary>错误码</summary>
        public string ErrorCode { get; set; }

        /// <summary>错误描述</summary>
        public string ErrorMsg { get; set; }

        public static SmsSendResult Ok(string bizId = null, bool simulated = false)
            => new() { Success = true, BizId = bizId, Simulated = simulated };

        public static SmsSendResult Fail(string code, string msg)
            => new() { Success = false, ErrorCode = code, ErrorMsg = msg };
    }

    /// <summary>发送短信接口入参（HTTP API 用）</summary>
    public class SmsSendDto
    {
        /// <summary>接收手机号</summary>
        public string PhoneNum { get; set; }

        /// <summary>短信内容（与模板二选一）</summary>
        public string Content { get; set; }

        /// <summary>模板编号（与内容二选一）</summary>
        public string TemplateCode { get; set; }

        /// <summary>模板参数</summary>
        public Dictionary<string, string> TemplateParams { get; set; }

        /// <summary>发送类型 1登录 2注册 3找回密码 6通知，默认6</summary>
        public int SendType { get; set; } = 6;
    }
}
