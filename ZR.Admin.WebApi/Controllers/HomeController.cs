using Infrastructure;
using Infrastructure.Attribute;
using Microsoft.AspNetCore.Mvc;
using System;

namespace ZR.Admin.WebApi.Controllers
{
    public class HomeController : BaseController
    {
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
    }
}
