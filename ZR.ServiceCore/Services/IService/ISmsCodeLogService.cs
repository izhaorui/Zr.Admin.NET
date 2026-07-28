using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.Models;
using ZR.ServiceCore.Sms;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 短信验证码记录service接口
    /// </summary>
    public interface ISmsCodeLogService : IBaseService<SmsCodeLog>
    {
        PagedInfo<SmsCodeLogDto> GetList(SmscodeLogQueryDto parm);

        SmsCodeLog GetInfo(long Id);

        SmsCodeLog AddSmscodeLog(SmsCodeLog parm);

        /// <summary>
        /// 发送通知类短信（如订单发货通知），不生成验证码、不做验证码频控。
        /// 仅落库记录；真实短信发送待接入短信服务商（同 AddSmscodeLog 的 //TODO）。
        /// </summary>
        /// <param name="phone">接收手机号</param>
        /// <param name="content">短信内容</param>
        /// <param name="sendType">发送类型（默认 6=发货通知）</param>
        SmsCodeLog SendSmsNotice(string phone, string content, int sendType = 6);

        /// <summary>
        /// 通用短信发送（落库 + 通过 ISmsSender 统一抽象发送），支持直发内容或模板发送
        /// </summary>
        SmsSendResult SendSms(SmsMessage message);
    }
}
