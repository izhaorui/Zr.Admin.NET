﻿using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Common;
using ZR.Model.System;
using ZR.Service.System.IService;

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

        private IWebHostEnvironment WebHostEnvironment;
        private ISysFileService SysFileService;
        public CommonController(IOptions<OptionsSetting> options, IWebHostEnvironment webHostEnvironment, ISysFileService fileService)
        {
            WebHostEnvironment = webHostEnvironment;
            SysFileService = fileService;
            OptionsSetting = options.Value;
        }

        /// <summary>
        /// 心跳
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Health()
        {
            return SUCCESS(true);
        }

        [Route("/")]
        [HttpGet]
        public IActionResult Index()
        {
            return Content("Hello看到这里页面说明你已经成功启动了本项目，加油吧 少年。");
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="sendEmailVo">请求参数接收实体</param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "tool:email:send")]
        [Log(Title = "发送邮件", IsSaveRequestData = false)]
        [HttpPost]
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

        #region 上传

        /// <summary>
        /// 存储文件
        /// </summary>
        /// <param name="formFile"></param>
        /// <param name="fileDir">存储目录</param>
        /// <returns></returns>
        [HttpPost()]
        [Verify]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult UploadFile([FromForm(Name = "file")] IFormFile formFile, string fileDir = "uploads")
        {
            if (formFile == null) throw new CustomException(ResultCode.PARAM_ERROR, "上传文件不能为空");
            string fileExt = Path.GetExtension(formFile.FileName);
            string fileName = FileUtil.HashFileName(Guid.NewGuid().ToString()).ToLower() + fileExt;
            string filePath = FileUtil.GetdirPath(fileDir);
            string finalFilePath = Path.Combine(WebHostEnvironment.WebRootPath, filePath, fileName);
            finalFilePath = finalFilePath.Replace("\\", "/").Replace("//", "/");
            double fileSize = formFile.Length / 1024;

            if (!Directory.Exists(Path.GetDirectoryName(finalFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(finalFilePath));
            }

            using (var stream = new FileStream(finalFilePath, FileMode.Create))
            {
                formFile.CopyTo(stream);
            }

            string accessPath = $"{OptionsSetting.Upload.UploadUrl}/{filePath.Replace("\\", " /")}{fileName}";
            SysFile file = new()
            {
                AccessUrl = accessPath,
                Create_by = HttpContext.GetName(),
                FileExt = fileExt,
                FileName = fileName,
                FileSize = fileSize + "kb",
                StoreType = 1,
                FileUrl = finalFilePath,
                Create_time = DateTime.Now
            };
            long fileId = SysFileService.InsertFile(file);
            return ToResponse(ResultCode.SUCCESS, new
            {
                url = accessPath,
                fileName,
                fileId
            });
        }

        /// <summary>
        /// 存储文件到阿里云
        /// </summary>
        /// <param name="formFile"></param>
        /// <param name="fileDir">上传文件夹路径</param>
        /// <returns></returns>
        [HttpPost]
        [Verify]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult UploadFileAliyun([FromForm(Name = "file")] IFormFile formFile, string fileDir = "")
        {
            if (formFile == null) throw new CustomException(ResultCode.PARAM_ERROR, "上传文件不能为空");
            string fileExt = Path.GetExtension(formFile.FileName);
            string[] AllowedFileExtensions = new string[] { ".jpg", ".gif", ".png", ".jpeg", ".webp", ".svga", ".xls" };
            int MaxContentLength = 1024 * 1024 * 5;
            double fileSize = formFile.Length / 1024;
            if (!AllowedFileExtensions.Contains(fileExt))
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "上传失败，未经允许上传类型");
            }

            if (formFile.Length > MaxContentLength)
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "上传文件过大，不能超过 " + (MaxContentLength / 1024).ToString() + " MB");
            }
            (bool, string, string) result = SysFileService.SaveFile(fileDir, formFile);
            long fileId = SysFileService.InsertFile(new SysFile()
            {
                AccessUrl = result.Item2,
                Create_by = HttpContext.GetName(),
                FileExt = fileExt,
                FileName = result.Item3,
                FileSize = fileSize + "kb",
                StoreType = 2,
                StorePath = fileDir
            });
            return ToResponse(ResultCode.SUCCESS, new
            {
                url = result.Item2,
                fileName = result.Item3,
                fileId
            });
        }
        #endregion
    }
}
