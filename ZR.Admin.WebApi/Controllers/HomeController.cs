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
    public class HomeController : BaseController
    {
        private OptionsSetting OptionsSetting;
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public HomeController(IOptions<OptionsSetting> options)
        {
            OptionsSetting = options.Value;
        }

        /// <summary>
        /// 心跳
        /// </summary>
        /// <returns></returns>
        [HttpGet, Route("health/index")]
        [Log()]
        public IActionResult Health()
        {
            return SUCCESS(true);
        }

        /// <summary>
        /// 加密
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public IActionResult Encrypt(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                throw new Exception("content不能为空");
            }
            string key = ConfigUtils.Instance.GetConfig("DbKey");
            string encryptTxt = NETCore.Encrypt.EncryptProvider.DESEncrypt(content, key);
            return Ok(new { content, encryptTxt });
        }

        /// <summary>
        /// 解密
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public IActionResult Decrypt(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                throw new Exception("content不能为空");
            }
            string key = ConfigUtils.Instance.GetConfig("DbKey");
            string encryptTxt = NETCore.Encrypt.EncryptProvider.DESDecrypt(content, key);
            return Ok(new { content, encryptTxt });
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="sendEmailVo">请求参数接收实体</param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "tool:email:send")]
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
