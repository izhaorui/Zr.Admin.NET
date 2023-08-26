using Infrastructure.Constant;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ZR.Admin.WebApi.Filters;
using ZR.Admin.WebApi.Hubs;
using ZR.Model;
using ZR.Model.System.Dto;
using ZR.Service.System;

namespace ZR.Admin.WebApi.Controllers.monitor
{
    [Verify]
    [Route("monitor/online")]
    [Tags("在线用户SysUserOnline")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysUserOnlineController : BaseController
    {
        private readonly IHubContext<MessageHub> HubContext;

        public SysUserOnlineController(IHubContext<MessageHub> hubContext)
        {
            HubContext = hubContext;
        }

        /// <summary>
        /// 获取在线用户列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        public IActionResult Index([FromQuery] PagerInfo parm)
        {
            var result = MessageHub.clientUsers
                .OrderByDescending(f => f.LoginTime)
                .Skip(parm.PageNum - 1).Take(parm.PageSize);

            return SUCCESS(new { result, totalNum = MessageHub.clientUsers.Count });
        }

        /// <summary>
        /// 强退
        /// </summary>
        /// <returns></returns>
        [HttpDelete("lock")]
        [Log(Title = "强退", BusinessType = BusinessType.FORCE)]
        public async Task<IActionResult> Lock([FromBody] LockUserDto dto)
        {
            if (dto == null) { return ToResponse(ResultCode.PARAM_ERROR); }
            
            await HubContext.Clients.Client(dto.ConnnectionId).SendAsync(HubsConstant.LockUser, new { dto.Reason, dto.Time });
            
            var expirTime = DateTimeHelper.GetUnixTimeSeconds(DateTime.Now.AddMinutes(dto.Time));
            //PC 端采用设备 + 用户名的方式进行封锁
            CacheService.SetLockUser(dto.ClientId + dto.Name, expirTime, dto.Time);
            return SUCCESS(new { expirTime });
        }
    }
}
