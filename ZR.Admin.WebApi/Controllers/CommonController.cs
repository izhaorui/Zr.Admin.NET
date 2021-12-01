using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using ZR.Admin.WebApi.Filters;
using ZR.Common;

namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 公共模块
    /// </summary>
    [Route("[controller]/[action]")]
    public class CommonController : BaseController
    {
        private OptionsSetting OptionsSetting;
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public CommonController(IOptions<OptionsSetting> options)
        {
            OptionsSetting = options.Value;
        }

        /// <summary>
        /// 心跳
        /// </summary>
        /// <returns></returns>
        [HttpGet, Route("health/index")]
        public IActionResult Health()
        {
            return SUCCESS(true);
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="sendEmailVo">请求参数接收实体</param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "tool:email:send")]
        [Log(Title = "发送邮件", IsSaveRequestData = false)]
        public IActionResult SendEmail([FromBody] SendEmailDto sendEmailVo)
        {
            if (sendEmailVo == null || string.IsNullOrEmpty(sendEmailVo.Subject) || string.IsNullOrEmpty(sendEmailVo.ToUser))
            {
                return ToResponse(ApiResult.Error($"请求参数不完整"));
            }
            if (string.IsNullOrEmpty(OptionsSetting.MailOptions.From) || string.IsNullOrEmpty(OptionsSetting.MailOptions.Password))
            {
                return ToResponse(ApiResult.Error($"请配置邮箱信息"));
            }
            MailHelper mailHelper = new MailHelper(OptionsSetting.MailOptions.From, OptionsSetting.MailOptions.Smtp, OptionsSetting.MailOptions.Port, OptionsSetting.MailOptions.Password);

            mailHelper.SendMail(sendEmailVo.ToUser, sendEmailVo.Subject, sendEmailVo.Content, sendEmailVo.FileUrl, sendEmailVo.HtmlContent);

            logger.Info($"发送邮件{JsonConvert.SerializeObject(sendEmailVo)}");

            return SUCCESS(true);
        }
    }
}
