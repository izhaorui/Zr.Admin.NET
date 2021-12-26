using IPTools.Core;
using Microsoft.AspNetCore.Http;
using UAParser;
using ZR.Admin.WebApi.Extensions;
using ZR.Model.System;

namespace ZR.Admin.WebApi.Framework
{
    public class AsyncFactory
    {
        /// <summary>
        /// 记录用户登陆信息
        /// </summary>
        /// <param name="context"></param>
        /// <param name="status"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static SysLogininfor RecordLogInfo(HttpContext context, string status, string message)
        {
            ClientInfo clientInfo = context.GetClientInfo();
            SysLogininfor sysLogininfor = new SysLogininfor();
            sysLogininfor.browser = clientInfo.Device.Family;
            sysLogininfor.os = clientInfo.OS.ToString();
            sysLogininfor.ipaddr = context.GetClientUserIp();
            sysLogininfor.msg = message;
            sysLogininfor.userName = context.GetName();
            sysLogininfor.status = status;
            var ip_info = IpTool.Search(sysLogininfor.ipaddr);
            sysLogininfor.loginLocation = ip_info.Province + "-" + ip_info.City;

            return sysLogininfor;
        }
    }
}
