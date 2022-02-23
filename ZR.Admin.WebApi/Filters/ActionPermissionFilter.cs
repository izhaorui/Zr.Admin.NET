using Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZR.Model.System;

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
        public string Permission { get; set; }
        private bool HasPermi { get; set; }
        private bool HasRole { get; set; }

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
            LoginUser info = Framework.JwtUtil.GetLoginUser(context.HttpContext);

            if (info != null && info?.UserId > 0)
            {
                List<string> perms = info.Permissions;
                List<string> rolePerms = info.RoleIds;
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

                bool isDemoMode = AppSettings.GetAppConfig("DemoMode", false);

                //演示公开环境屏蔽权限
                string[] denyPerms = new string[] { "update", "add", "remove", "add", "edit", "delete", "import", "run", "start", "stop", "clear", "send", "export", "upload", "common" };
                if (isDemoMode && denyPerms.Any(f => Permission.ToLower().Contains(f)))
                {
                    context.Result = new JsonResult(new { code = ResultCode.FORBIDDEN, msg = "演示模式 , 不允许操作" });
                }
                if (!HasPermi && !Permission.Equals("common"))
                {
                    logger.Info($"用户{info.UserName}没有权限访问{context.HttpContext.Request.Path}，当前权限[{Permission}]");
                    context.Result = new JsonResult(new { code = ResultCode.FORBIDDEN, msg = "你没有权限访问" });
                }
            }

            return base.OnActionExecutionAsync(context, next);
        }
    }
}
