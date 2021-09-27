using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using ZR.Common;

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
        public IActionResult SaveFile([FromForm(Name = "file")] IFormFile formFile)
        {
            if (formFile == null) throw new CustomException(ResultCode.PARAM_ERROR, "上传图片不能为空");
            string fileExt = Path.GetExtension(formFile.FileName);
            string savePath = Path.Combine(WebHostEnvironment.WebRootPath, FileUtil.GetdirPath("uploads"));

            if (!Directory.Exists(savePath)) { Directory.CreateDirectory(savePath); }

            string fileName = FileUtil.HashFileName(Guid.NewGuid().ToString()).ToLower() + fileExt;
            string finalFilePath = Path.Combine(savePath, fileName);

            using (var stream = new FileStream(finalFilePath, FileMode.Create))
            {
                formFile.CopyToAsync(stream);
            }

            string accessPath = $"{OptionsSetting.Upload.UploadUrl}/{finalFilePath.Replace("wwwroot", "").Replace("\\", "/")}";
            return ToResponse(ToJson(1, accessPath));
        }
    }
}
