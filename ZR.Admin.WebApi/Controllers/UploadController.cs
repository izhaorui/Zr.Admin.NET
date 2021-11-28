using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using ZR.Admin.WebApi.Filters;

namespace ZR.Admin.WebApi.Controllers
{
    [Route("[controller]/[action]")]
    public class UploadController : BaseController
    {
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private OptionsSetting OptionsSetting;
        private IWebHostEnvironment WebHostEnvironment;
        public UploadController(IOptions<OptionsSetting> optionsSetting, IWebHostEnvironment webHostEnvironment)
        {
            OptionsSetting = optionsSetting.Value;
            WebHostEnvironment = webHostEnvironment;
        }
        /// <summary>
        /// 存储文件
        /// </summary>
        /// <param name="formFile"></param>
        /// <returns></returns>
        [HttpPost]
        [Verify]
        [ActionPermissionFilter(Permission = "system")]
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
            return ToResponse(ResultCode.SUCCESS, new { accessPath, fullPath = finalFilePath });
        }
    }
}
