using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using ZR.ServiceCore.Sms;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 短信发送
    /// </summary>
    [Route("system/sms")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysSmsController : BaseController
    {
        private readonly ISmsCodeLogService smsCodeLogService;

        public SysSmsController(ISmsCodeLogService smsCodeLogService)
        {
            this.smsCodeLogService = smsCodeLogService;
        }

        /// <summary>
        /// 发送短信（支持直发内容或模板发送，落库短信记录）。
        /// 未配置短信服务商（SmsOptions.Enabled=false）时为模拟发送，仅记录日志。
        /// </summary>
        /// <param name="dto">发送参数</param>
        /// <returns>发送结果（含 Simulated 是否模拟、BizId 回执ID）</returns>
        [HttpPost("send")]
        [ActionPermissionFilter(Permission = "system:sms:send")]
        [Log(Title = "发送短信", BusinessType = BusinessType.OTHER)]
        public IActionResult Send([FromBody] SmsSendDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.PhoneNum))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "手机号不能为空");
            }
            if (!Regex.IsMatch(dto.PhoneNum, @"^1\d{10}$"))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "手机号格式不正确");
            }
            if (string.IsNullOrWhiteSpace(dto.Content) && string.IsNullOrWhiteSpace(dto.TemplateCode))
            {
                return ToResponse(ResultCode.PARAM_ERROR, "短信内容与模板编号至少填一项");
            }

            var result = smsCodeLogService.SendSms(new SmsMessage
            {
                PhoneNum = dto.PhoneNum,
                Content = dto.Content,
                TemplateCode = dto.TemplateCode,
                TemplateParams = dto.TemplateParams,
                SendType = dto.SendType
            });

            if (!result.Success)
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, $"短信发送失败：[{result.ErrorCode}] {result.ErrorMsg}");
            }
            return SUCCESS(result);
        }
    }
}
