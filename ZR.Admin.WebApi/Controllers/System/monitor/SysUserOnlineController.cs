using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ZR.Model;
using ZR.Model.System.Dto;
using ZR.ServiceCore.Signalr;

namespace ZR.Admin.WebApi.Controllers.monitor
{
    /// <summary>
    /// 在线用户
    /// </summary>
    [Route("monitor/online")]
    [ApiExplorerSettings(GroupName = "sys")]
    public class SysUserOnlineController : BaseController
    {
        private readonly IHubContext<MessageHub> HubContext;

        public SysUserOnlineController(IHubContext<MessageHub> hubContext)
        {
            HubContext = hubContext;
        }

        /// <summary>
        /// 获取在线用户列表（多租户模式下按当前租户过滤）
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        public IActionResult Index([FromQuery] PagerInfo parm)
        {
            var query = MessageHub.OnlineClients.Values.AsEnumerable();

            if (App.IsTenantEnabled())
            {
                var currentTenantId = App.GetCurrentTenantId();
                query = query.Where(u => string.Equals(u.TenantId, currentTenantId, StringComparison.OrdinalIgnoreCase));
            }

            var filtered = query.OrderByDescending(f => f.LoginTime).ToList();

            var result = filtered
                .Skip((parm.PageNum - 1) * parm.PageSize)
                .Take(parm.PageSize);

            return SUCCESS(new { result, totalNum = filtered.Count });
        }

        /// <summary>
        /// 单个强退
        /// </summary>
        /// <returns></returns>
        [HttpDelete("force")]
        [Log(Title = "强退", BusinessType = BusinessType.FORCE)]
        [ActionPermissionFilter(Permission = "monitor:online:forceLogout")]
        public async Task<IActionResult> Force([FromBody] LockUserDto dto)
        {
            if (dto == null) { return ToResponse(ResultCode.PARAM_ERROR); }
            
            await HubContext.Clients.Client(dto.ConnnectionId)
                .SendAsync(HubsConstant.ForceUser, new { dto.Reason, dto.Time });
            
            //var expirTime = DateTimeHelper.GetUnixTimeSeconds(DateTime.Now.AddMinutes(dto.Time));
            ////PC 端采用设备 + 用户名的方式进行封锁
            //CacheService.SetLockUser(dto.ClientId + dto.Name, expirTime, dto.Time);
            return SUCCESS(1);
        }

        /// <summary>
        /// 批量强退
        /// </summary>
        /// <returns></returns>
        [HttpDelete("batchForce")]
        [Log(Title = "强退", BusinessType = BusinessType.FORCE)]
        [ActionPermissionFilter(Permission = "monitor:online:batchLogout")]
        public async Task<IActionResult> BatchforceLogout([FromBody] LockUserDto dto)
        {
            if (dto == null) { return ToResponse(ResultCode.PARAM_ERROR); }

            await HubContext.Clients.All.SendAsync(HubsConstant.ForceUser, new { dto.Reason });

            return SUCCESS(1);
        }
    }
}
