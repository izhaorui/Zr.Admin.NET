using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZR.Admin.WebApi.Filters;
using ZR.Model.Dto;
using ZR.Model.Models;
using ZR.ServiceCore.Model;

//创建时间：2023-11-20
namespace ZR.Admin.WebApi.Controllers.Email
{
    /// <summary>
    /// 邮件发送记录
    /// </summary>
    [Verify]
    [Route("system/EmailLog")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class EmailLogController : BaseController
    {
        /// <summary>
        /// 邮件发送记录接口
        /// </summary>
        private readonly IEmailLogService _EmailLogService;
        private OptionsSetting OptionsSetting;
        public EmailLogController(
            IEmailLogService EmailLogService,
            IOptions<OptionsSetting> options)
        {
            _EmailLogService = EmailLogService;
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
        /// 批量发送邮件(从记录发送)
        /// </summary>
        /// <returns></returns>
        [HttpPost("sendEmail")]
        [ActionPermissionFilter(Permission = "tool:email:send")]
        [Log(Title = "批量邮件发送", BusinessType = BusinessType.INSERT)]
        public IActionResult SendEmail([FromBody] EmailLogDto dto)
        {
            if (dto.IdArr.Length <= 0) { return ToResponse(ApiResult.Error($"发送失败Id 不能为空")); }
            int count = 0;

            foreach (var item in dto.IdArr)
            {
                var response = _EmailLogService.GetInfo(item);
                MailOptions mailOptions = OptionsSetting.MailOptions.Find(x => x.FromName == response.FromName);
                if (mailOptions == null || string.IsNullOrEmpty(mailOptions.Password))
                {
                    continue;
                }
                MailHelper mailHelper = new(mailOptions);
                string[] toUsers = response.ToEmails.Split(",", StringSplitOptions.RemoveEmptyEntries);

                string result = mailHelper.SendMail(toUsers, response.Subject, "", response.FileUrl, response.EmailContent);
                count += _EmailLogService.Update(x => x.Id == item, x => new EmailLog()
                {
                    IsSend = 1,
                    SendTime = DateTime.Now,
                    SendResult = result
                });
            }
            return SUCCESS(count);
        }

        /// <summary>
        /// 删除邮件发送记录
        /// </summary>
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
        public IActionResult SendEmail([FromBody] SendEmailDto sendEmailVo)
        {
            if (sendEmailVo == null)
            {
                return ToResponse(ApiResult.Error($"请求参数不完整"));
            }
            MailOptions mailOptions = OptionsSetting.MailOptions.Find(x => x.FromName == sendEmailVo.FromName);
            if (mailOptions == null || string.IsNullOrEmpty(mailOptions.Password))
            {
                return ToResponse(ApiResult.Error($"请配置邮箱信息"));
            }

            MailHelper mailHelper = new(mailOptions);

            string[] toUsers = sendEmailVo.ToUser.Split(",", StringSplitOptions.RemoveEmptyEntries);
            string result = string.Empty;
            if (sendEmailVo.IsSend)
            {
                result = mailHelper.SendMail(toUsers, sendEmailVo.Subject, sendEmailVo.Content, sendEmailVo.FileUrl, sendEmailVo.HtmlContent);
            }
            sendEmailVo.FromEmail = mailOptions.FromEmail;
            _EmailLogService.AddEmailLog(sendEmailVo, result);
            //logger.Info($"发送邮件{JsonConvert.SerializeObject(sendEmailVo)}, 结果{result}");

            return SUCCESS(result);
        }

    }
}