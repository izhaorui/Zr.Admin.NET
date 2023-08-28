using Infrastructure.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Data;
using ZR.Admin.WebApi.Framework;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Service.System;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Filters
{
    /// <summary>
    /// API授权判断
    /// </summary>
    public class ActionPermissionFilter : ActionFilterAttribute//, IAsyncActionFilter
    {
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 权限字符串，例如 system:user:view
        /// </summary>
        public string Permission { get; set; } = string.Empty;
        /// <summary>
        /// 角色字符串，例如 common,admin
        /// </summary>
        public string RolePermi { get; set; } = string.Empty;
        private bool HasPermi { get; set; }
        public ActionPermissionFilter() { }
        public ActionPermissionFilter(string permission)
        {
            Permission = permission;
            HasPermi = !string.IsNullOrEmpty(Permission);
        }

        /// <summary>
        /// 执行Action前校验是否有权限访问
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public override Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            LoginUser info = JwtUtil.GetLoginUser(context.HttpContext);

            if (info != null && info?.UserId > 0)
            {
                long userId = info.UserId;
                List<string> perms = CacheService.GetUserPerms(GlobalConstant.UserPermKEY + userId);
                List<string> rolePerms = info.RoleIds;

                if (perms == null)
                {
                    var sysPermissionService = App.GetService<ISysPermissionService>();
                    perms = sysPermissionService.GetMenuPermission(new SysUser() { UserId = userId });

                    CacheService.SetUserPerms(GlobalConstant.UserPermKEY + userId, perms);
                }

                if (perms.Exists(f => f.Equals(GlobalConstant.AdminPerm)))
                {
                    HasPermi = true;
                }
                else if (rolePerms.Exists(f => f.Equals(GlobalConstant.AdminRole)))
                {
                    HasPermi = true;
                }
                else if (!string.IsNullOrEmpty(Permission))
                {
                    HasPermi = perms.Exists(f => f.ToLower() == Permission.ToLower());
                }
                if (!HasPermi && !string.IsNullOrEmpty(RolePermi))
                {
                    HasPermi = info.RoleIds.Contains(RolePermi);
                }
                bool isDemoMode = AppSettings.GetAppConfig("DemoMode", false);
                var url = context.HttpContext.Request.Path;
                //演示公开环境屏蔽权限
                string[] denyPerms = new string[] { "update", "add", "remove", "add", "edit", "delete", "import", "run", "start", "stop", "clear", "send", "export", "upload", "common", "gencode", "reset" };
                if (isDemoMode && denyPerms.Any(f => Permission.ToLower().Contains(f)))
                {
                    context.Result = new JsonResult(new { code = ResultCode.FORBIDDEN, msg = "演示模式 , 不允许操作" });
                }
                if (!HasPermi && !Permission.Equals("common"))
                {
                    logger.Info($"用户{info.UserName}没有权限访问{url}，当前权限[{Permission}]");
                    JsonResult result = new(new ApiResult()
                    {
                        Code = (int)ResultCode.FORBIDDEN,
                        Msg = $"你当前没有权限[{Permission}]访问,请联系管理员",
                        Data = url
                    })
                    {
                        ContentType = "application/json",
                    };
                    context.Result = result;
                }
            }

            return base.OnActionExecutionAsync(context, next);
        }
    }
}
