using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using ZR.Admin.WebApi.Filters;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Controllers
{
    [Route("[controller]/[action]")]
    public class UploadController : BaseController
    {
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private OptionsSetting OptionsSetting;
        private IWebHostEnvironment WebHostEnvironment;
        private ISysFileService SysFileService;
        public UploadController(IOptions<OptionsSetting> optionsSetting, IWebHostEnvironment webHostEnvironment, ISysFileService fileService)
        {
            OptionsSetting = optionsSetting.Value;
            WebHostEnvironment = webHostEnvironment;
            SysFileService = fileService;
        }
        /// <summary>
        /// 存储文件
        /// </summary>
        /// <param name="formFile"></param>
        /// <returns></returns>
        [HttpPost]
        //[Verify]
        //[ActionPermissionFilter(Permission = "system")]
        public IActionResult SaveFile([FromForm(Name = "file")] IFormFile formFile)
        {
            if (formFile == null) throw new CustomException(ResultCode.PARAM_ERROR, "上传图片不能为空");
            string fileExt = Path.GetExtension(formFile.FileName);
            string fileName = FileUtil.HashFileName(Guid.NewGuid().ToString()).ToLower() + fileExt;
            string finalFilePath = Path.Combine(WebHostEnvironment.WebRootPath, FileUtil.GetdirPath("uploads"), fileName);
            finalFilePath = finalFilePath.Replace("\\", "/").Replace("//", "/");

            if (!Directory.Exists(Path.GetDirectoryName(finalFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(finalFilePath));
            }

            using (var stream = new FileStream(finalFilePath, FileMode.Create))
            {
                formFile.CopyToAsync(stream);
            }

            string accessPath = $"{OptionsSetting.Upload.UploadUrl}/{FileUtil.GetdirPath("uploads").Replace("\\", " /")}{fileName}";
            return ToResponse(ResultCode.SUCCESS, accessPath);
        }

        /// <summary>
        /// 存储文件到阿里云
        /// </summary>
        /// <param name="formFile"></param>
        /// <returns></returns>
        [HttpPost]
        //[Verify]
        //[ActionPermissionFilter(Permission = "system")]
        public IActionResult SaveFileAliyun([FromForm(Name = "file")] IFormFile formFile)
        {
            if (formFile == null) throw new CustomException(ResultCode.PARAM_ERROR, "上传文件不能为空");
            string fileExt = Path.GetExtension(formFile.FileName);
            string[] AllowedFileExtensions = new string[] { ".jpg", ".gif", ".png", ".jpeg", ".webp", ".svga", ".xls" };
            int MaxContentLength = 1024 * 1024 * 4;

            if (!AllowedFileExtensions.Contains(fileExt))
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "上传失败，未经允许上传类型");
            }

            if (formFile.Length > MaxContentLength)
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "上传文件过大，不能超过 " + (MaxContentLength / 1024).ToString() + " MB");
            }
            (bool, string) result = SysFileService.SaveFile("", formFile);

            return ToResponse(ResultCode.SUCCESS, result.Item2);
        }
    }
}
