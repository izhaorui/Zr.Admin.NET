using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZR.Admin.WebApi.Extensions;
using ZR.Admin.WebApi.Filters;
using ZR.Admin.WebApi.Framework;
using Infrastructure.Model;
using Infrastructure;
using Infrastructure.Attribute;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Service.System.IService;
using Hei.Captcha;
using ZR.Common;

namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 登录
    /// </summary>
    public class SysLoginController : BaseController
    {
        static readonly NLog.Logger logger = NLog.LogManager.GetLogger("LoginController");
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly ISysUserService sysUserService;
        private readonly ISysMenuService sysMenuService;
        private readonly ISysLoginService sysLoginService;
        private readonly ISysPermissionService permissionService;
        private readonly SecurityCodeHelper SecurityCodeHelper;

        public SysLoginController(
            IHttpContextAccessor contextAccessor,
            ISysMenuService sysMenuService,
            ISysUserService sysUserService,
            ISysLoginService sysLoginService,
            ISysPermissionService permissionService,
            SecurityCodeHelper captcha)
        {
            httpContextAccessor = contextAccessor;
            SecurityCodeHelper = captcha;
            this.sysMenuService = sysMenuService;
            this.sysUserService = sysUserService;
            this.sysLoginService = sysLoginService;
            this.permissionService = permissionService;
        }


        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="loginBody">登录对象</param>
        /// <returns></returns>
        [Route("login")]
        [HttpPost]
        [Log(Title = "登录")]
        public IActionResult Login([FromBody] LoginBodyDto loginBody)
        {
            if (loginBody == null) { throw new CustomException("请求参数错误"); }
            loginBody.LoginIP = HttpContextExtension.GetClientUserIp(HttpContext);
            if (CacheHelper.Get(loginBody.Uuid) is string str && !str.ToLower().Equals(loginBody.Code.ToLower()))
            {
                throw new CustomException(ResultCode.CAPTCHA_ERROR, "验证码错误");
            }

            var user = sysLoginService.Login(loginBody, AsyncFactory.RecordLogInfo(httpContextAccessor.HttpContext, "0", "login"));
            #region 存入cookie Action校验权限使用
            //角色集合 eg: admin,yunying,common
            List<string> roles = permissionService.GetRolePermission(user);
            //权限集合 eg *:*:*,system:user:list
            List<string> permissions = permissionService.GetMenuPermission(user);
            #endregion
            LoginUser loginUser = new LoginUser(user.UserId, user.UserName, roles, permissions);

            return SUCCESS(JwtUtil.GenerateJwtToken(HttpContext.WriteCookies(loginUser)));
        }

        /// <summary>
        /// 注销
        /// </summary>
        /// <returns></returns>
        [Log(Title = "注销")]
        [HttpPost("logout")]
        public IActionResult LogOut()
        {
            Task.Run(async () =>
            {
                //注销登录的用户，相当于ASP.NET中的FormsAuthentication.SignOut  
                await HttpContext.SignOutAsync();
            }).Wait();

            return SUCCESS(1);
        }

        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <returns></returns>
        [Verify]
        [HttpGet("getInfo")]
        public IActionResult GetUserInfo()
        {
            long userid = HttpContext.GetUId();
            var user = sysUserService.SelectUserById(userid);

            //前端校验按钮权限使用
            //角色集合 eg: admin,yunying,common
            List<string> roles = permissionService.GetRolePermission(user);
            //权限集合 eg *:*:*,system:user:list
            List<string> permissions = permissionService.GetMenuPermission(user);

            return SUCCESS(new { user, roles, permissions });
        }

        /// <summary>
        /// 获取路由信息
        /// </summary>
        /// <returns></returns>
        [Verify]
        [HttpGet("getRouters")]
        public IActionResult GetRouters()
        {
            long uid = HttpContext.GetUId();
            var menus = sysMenuService.SelectMenuTreeByUserId(uid);

            return ToResponse(ToJson(1, sysMenuService.BuildMenus(menus)));
        }

        /// <summary>
        /// 生成图片验证码
        /// </summary>
        /// <returns></returns>
        [HttpGet("captchaImage")]
        public ApiResult CaptchaImage()
        {
            string uuid = Guid.NewGuid().ToString().Replace("-", "");
            var code = SecurityCodeHelper.GetRandomEnDigitalText(4);
            var imgByte = SecurityCodeHelper.GetEnDigitalCodeByte(code);
            string base64Str = Convert.ToBase64String(imgByte);
            CacheHelper.SetCache(uuid, code);
            var obj = new { uuid, img = base64Str };// File(stream, "image/png")

            return ToJson(1, obj);
        }
    }
}
