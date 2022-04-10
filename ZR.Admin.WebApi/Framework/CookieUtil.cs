using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ZR.Admin.WebApi.Framework
{
    public class CookieUtil
    {
        public static void WhiteCookie(HttpContext context, List<Claim> claims)
        {
            //2.创建声明主题 指定认证方式 这里使用cookie
            var claimsIdentity = new ClaimsIdentity(claims, "Login");

            Task.Run(async () =>
            {
                await context.SignInAsync(
                JwtBearerDefaults.AuthenticationScheme,//这里要注意的是HttpContext.SignInAsync(AuthenticationType,…) 所设置的Scheme一定要与前面的配置一样，这样对应的登录授权才会生效。
            new ClaimsPrincipal(claimsIdentity),
            new AuthenticationProperties()
            {
                IsPersistent = true,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.Now.AddDays(1),//有效时间
            });
            }).Wait();
        }
    }
}
