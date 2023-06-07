using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Extensions;
using Infrastructure.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MiniExcelLibs;
using Newtonsoft.Json;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Common;
using ZR.Model.System;
using ZR.Service.System;
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
        public CommonController(
            IOptions<OptionsSetting> options,
            IWebHostEnvironment webHostEnvironment,
            ISysFileService fileService)
        {
            WebHostEnvironment = webHostEnvironment;
            SysFileService = fileService;
            OptionsSetting = options.Value;
        }

        /// <summary>
        /// hello
        /// </summary>
        /// <returns></returns>
        [Route("/")]
        [HttpGet]
        public IActionResult Index()
        {
            return Ok("看到这里页面说明你已经成功启动了本项目:)\n\n" +
                "如果觉得项目有用，打赏作者喝杯咖啡作为奖励\n☛☛http://www.izhaorui.cn/doc/support.html\n");
        }

        /// <summary>
        /// 企业消息测试
        /// </summary>
        /// <param name="msg">要发送的消息</param>
        /// <param name="toUser">要发送的人@all所有，xxx单独发送对个人</param>
        /// <returns></returns>
        [Route("/sendMsg")]
        [HttpGet]
        [Log(Title = "企业消息测试")]
        public IActionResult SendMsg(string msg, string toUser = "")
        {
            WxNoticeHelper.SendMsg("消息测试", msg, toUser, WxNoticeHelper.MsgType.markdown);
            return SUCCESS(msg);
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="sendEmailVo">请求参数接收实体</param>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "tool:email:send")]
        [Log(Title = "发送邮件")]
        [HttpPost]
        public IActionResult SendEmail([FromBody] SendEmailDto sendEmailVo)
        {
            if (sendEmailVo == null)
            {
                return ToResponse(ApiResult.Error($"请求参数不完整"));
            }
            if (string.IsNullOrEmpty(OptionsSetting.MailOptions.FromEmail) || string.IsNullOrEmpty(OptionsSetting.MailOptions.Password))
            {
                return ToResponse(ApiResult.Error($"请配置邮箱信息"));
            }

            MailHelper mailHelper = new();

            string[] toUsers = sendEmailVo.ToUser.Split(",", StringSplitOptions.RemoveEmptyEntries);
            if (sendEmailVo.SendMe)
            {
                toUsers.Append(mailHelper.FromEmail);
            }
            string result = mailHelper.SendMail(toUsers, sendEmailVo.Subject, sendEmailVo.Content, sendEmailVo.FileUrl, sendEmailVo.HtmlContent);

            logger.Info($"发送邮件{JsonConvert.SerializeObject(sendEmailVo)}, 结果{result}");

            return SUCCESS(result);
        }

        #region 上传

        /// <summary>
        /// 存储文件
        /// </summary>
        /// <param name="uploadDto">自定义文件名</param>
        /// <param name="storeType">上传类型1、保存到本地 2、保存到阿里云</param>
        /// <returns></returns>
        [HttpPost()]
        [Verify]
        [ActionPermissionFilter(Permission = "common")]
        public async Task<IActionResult> UploadFile([FromForm] UploadDto uploadDto, StoreType storeType = StoreType.LOCAL)
        {
            IFormFile formFile = uploadDto.File;
            if (formFile == null) throw new CustomException(ResultCode.PARAM_ERROR, "上传文件不能为空");
            SysFile file = new();
            string fileExt = Path.GetExtension(formFile.FileName);//文件后缀
            double fileSize = Math.Round(formFile.Length / 1024.0, 2);//文件大小KB

            if (OptionsSetting.Upload.NotAllowedExt.Contains(fileExt))
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "上传失败，未经允许上传类型");
            }
            if (uploadDto.FileNameType == 1)
            {
                uploadDto.FileName = Path.GetFileNameWithoutExtension(formFile.FileName);
            }
            else if (uploadDto.FileNameType == 3)
            {
                uploadDto.FileName = SysFileService.HashFileName();
            }
            switch (storeType)
            {
                case StoreType.LOCAL:
                    string savePath = Path.Combine(WebHostEnvironment.WebRootPath);
                    if (uploadDto.FileDir.IsEmpty())
                    {
                        uploadDto.FileDir = OptionsSetting.Upload.LocalSavePath;
                    }
                    file = await SysFileService.SaveFileToLocal(savePath, uploadDto.FileName, uploadDto.FileDir, HttpContext.GetName(), formFile);
                    break;
                case StoreType.REMOTE:
                    break;
                case StoreType.ALIYUN:
                    int AlimaxContentLength = OptionsSetting.ALIYUN_OSS.MaxSize;
                    if (OptionsSetting.ALIYUN_OSS.REGIONID.IsEmpty())
                    {
                        return ToResponse(ResultCode.CUSTOM_ERROR, "配置文件缺失");
                    }
                    if ((fileSize / 1024) > AlimaxContentLength)
                    {
                        return ToResponse(ResultCode.CUSTOM_ERROR, "上传文件过大，不能超过 " + AlimaxContentLength + " MB");
                    }
                    file = new(formFile.FileName, uploadDto.FileName, fileExt, fileSize + "kb", uploadDto.FileDir, HttpContext.GetName())
                    {
                        StoreType = (int)StoreType.ALIYUN,
                        FileType = formFile.ContentType
                    };
                    file = await SysFileService.SaveFileToAliyun(file, formFile);

                    if (file.Id <= 0) { return ToResponse(ApiResult.Error("阿里云连接失败")); }
                    break;
                case StoreType.TENCENT:
                    break;
                case StoreType.QINIU:
                    break;
                default:
                    break;
            }
            return SUCCESS(new
            {
                url = file.AccessUrl,
                fileName = file.FileName,
                fileId = file.Id.ToString()
            });
        }

        #endregion

        /// <summary>
        /// 初始化种子数据
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        [ActionPermissionFilter(Permission = "common")]
        [Log(BusinessType = BusinessType.INSERT, Title = "初始化数据")]
        public IActionResult InitSeedData()
        {
            if (!WebHostEnvironment.IsDevelopment())
            {
                return ToResponse(ResultCode.FAIL, "导入数据失败");
            }
            var path = Path.Combine(WebHostEnvironment.WebRootPath, "data.xlsx");
            //var sheetNames = MiniExcel.GetSheetNames(path);
            SeedDataService seedDataService = new();

            var sysUser = MiniExcel.Query<SysUser>(path, sheetName: "user").ToList();
            var result1 = seedDataService.InitUserData(sysUser);

            var sysPost = MiniExcel.Query<SysPost>(path, sheetName: "post").ToList();
            var result2 = seedDataService.InitPostData(sysPost);

            var sysRole = MiniExcel.Query<SysRole>(path, sheetName: "role").ToList();
            var result3 = seedDataService.InitRoleData(sysRole);

            var sysUserRole = MiniExcel.Query<SysUserRole>(path, sheetName: "user_role").ToList();
            var result4 = seedDataService.InitUserRoleData(sysUserRole);

            var sysMenu = MiniExcel.Query<SysMenu>(path, sheetName: "menu").ToList();
            var result5 = seedDataService.InitMenuData(sysMenu);

            var sysConfig = MiniExcel.Query<SysConfig>(path, sheetName: "config").ToList();
            var result6 = seedDataService.InitConfigData(sysConfig);

            var sysRoleMenu = MiniExcel.Query<SysRoleMenu>(path, sheetName: "role_menu").ToList();
            var result7 = seedDataService.InitRoleMenuData(sysRoleMenu);

            var sysDict = MiniExcel.Query<SysDictType>(path, sheetName: "dict_type").ToList();
            var result8 = seedDataService.InitDictType(sysDict);

            var sysDictData = MiniExcel.Query<SysDictData>(path, sheetName: "dict_data").ToList();
            var result9 = seedDataService.InitDictData(sysDictData);

            var sysDept = MiniExcel.Query<SysDept>(path, sheetName: "dept").ToList();
            var result10 = seedDataService.InitDeptData(sysDept);

            var sysArticleCategory = MiniExcel.Query<ArticleCategory>(path, sheetName: "article_category").ToList();
            var result11 = seedDataService.InitArticleCategoryData(sysArticleCategory);

            var sysTask = MiniExcel.Query<SysTasks>(path, sheetName: "task").ToList();
            var result12 = seedDataService.InitTaskData(sysTask);

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(result1.Item1);
            Console.WriteLine(result2.Item1);
            Console.WriteLine(result3.Item1);
            Console.WriteLine(result4.Item1);
            Console.WriteLine(result5.Item1);
            Console.WriteLine(result6.Item1);
            Console.WriteLine(result7.Item1);
            Console.WriteLine(result8.Item1);
            Console.WriteLine(result9.Item1);
            Console.WriteLine(result10.Item1);
            Console.WriteLine(result11.Item1);
            Console.WriteLine(result12.Item1);

            return SUCCESS(new
            {
                result1 = result1.Item1,
                result2 = result2.Item1,
                result3 = result3.Item1,
                result4 = result4.Item1,
                result5 = result5.Item1,
                result6 = result6.Item1,
                result7 = result7.Item1,
                result8 = result8.Item1,
                result9 = result9.Item1,
                result10 = result10.Item1,
                result11 = result11.Item1,
                result12 = result12.Item1
            });
        }
    }

    public class UploadDto
    {
        /// <summary>
        /// 自定文件名
        /// </summary>
        public string? FileName { get; set; }
        /// <summary>
        /// 存储目录
        /// </summary>
        public string? FileDir { get; set; }
        /// <summary>
        /// 文件名生成类型 1 原文件名 2 自定义 3 自动生成
        /// </summary>
        public int FileNameType { get; set; }
        public IFormFile? File { get; set; }
    }
}
