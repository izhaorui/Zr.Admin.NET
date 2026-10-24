using Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZR.Model.Dto;
using ZR.Model.Models;
using ZR.ServiceCore.Email;

//创建时间：2023-11-20
namespace ZR.Admin.WebApi.Controllers.Email
{
    /// <summary>
    /// 邮件发送记录
    /// </summary>
    [Route("system/EmailLog")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class EmailLogController : BaseController
    {
        /// <summary>
        /// 邮件发送记录接口
        /// </summary>
        private readonly IEmailLogService _EmailLogService;
        private readonly IEmailSendService _emailSendService;
        private readonly OptionsSetting OptionsSetting;

        public EmailLogController(
            IEmailLogService EmailLogService,
            IEmailSendService emailSendService,
            IOptions<OptionsSetting> options)
        {
            _EmailLogService = EmailLogService;
            _emailSendService = emailSendService;
            OptionsSetting = options.Value;
        }

        /// <summary>
        /// 查询邮件发送记录列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "emaillog:list")]
        public IActionResult QueryEmailLog([FromQuery] EmailLogQueryDto parm)
        {
            var response = _EmailLogService.GetList(parm);
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询邮件发送记录详情
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpGet("{Id}")]
        [ActionPermissionFilter(Permission = "emaillog:query")]
        public IActionResult GetEmailLog(long Id)
        {
            var response = _EmailLogService.GetInfo(Id);

            var info = response.Adapt<EmailLogDto>();
            return SUCCESS(info);
        }

        /// <summary>
        /// 批量重发邮件（从发送记录）
        /// </summary>
        /// <returns></returns>
        [HttpPost("sendEmail")]
        [ActionPermissionFilter(Permission = "tool:email:send")]
        [Log(Title = "批量邮件发送", BusinessType = BusinessType.INSERT)]
        public IActionResult SendEmail([FromBody] EmailLogDto dto)
        {
            if (dto?.IdArr == null || dto.IdArr.Length <= 0)
            {
                return ToResponse(ApiResult.Error($"发送失败Id 不能为空"));
            }

            // 记录重置为待发送并入队，真实投递由后台完成（避免批量同步发信长时间占用请求线程）
            var count = _emailSendService.Resend(dto.IdArr);
            return SUCCESS(count);
        }

        /// <summary>
        /// 删除邮件发送记录
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        [HttpDelete("{ids}")]
        [ActionPermissionFilter(Permission = "emaillog:delete")]
        [Log(Title = "邮件发送记录", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteEmailLog(string ids)
        {
            long[] idsArr = Tools.SpitLongArrary(ids);
            if (idsArr.Length <= 0) { return ToResponse(ApiResult.Error($"删除失败Id 不能为空")); }

            var response = _EmailLogService.Delete(idsArr);

            return ToResponse(response);
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="sendEmailVo">请求参数接收实体</param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "tool:email:send")]
        [Log(Title = "发送邮件")]
        [HttpPost("/common/SendEmail")]
        public async Task<IActionResult> SendEmail([FromBody] SendEmailDto sendEmailVo)
        {
            if (sendEmailVo == null)
            {
                return ToResponse(ApiResult.Error($"请求参数不完整"));
            }

            // IsSend=false 只留存记录（草稿），不投递；配置/附件校验在服务层完成
            if (!sendEmailVo.IsSend)
            {
                var draft = _emailSendService.CreateDraft(sendEmailVo);
                return SUCCESS(draft == null ? "fail" : "ok");
            }

            // 交互式发送保持同步返回结果；失败不自动重试，避免用户重复点击导致重复投递
            var result = await _emailSendService.SendFromDtoAsync(sendEmailVo);
            return SUCCESS(result.Success ? "ok" : "fail");
        }

        /// <summary>
        /// 获取可用发件邮箱列表（发送页下拉用，不返回密码）
        /// </summary>
        [HttpGet("/common/SendEmail/options")]
        [ActionPermissionFilter(Permission = "tool:email:send")]
        public IActionResult GetSendEmailOptions()
        {
            var list = (OptionsSetting.MailOptions ?? new List<MailOptions>())
                .Where(x => !string.IsNullOrWhiteSpace(x.FromEmail))
                .Select(x => new { x.FromName, x.FromEmail })
                .ToList();

            return SUCCESS(list);
        }

    }
}
