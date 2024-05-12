using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Filters;
using ZR.Model.Dto;
using ZR.ServiceCore.Monitor.IMonitorService;

//创建时间：2024-03-27
namespace ZR.Admin.WebApi.Controllers
{
    /// <summary>
    /// 用户在线时长
    /// </summary>
    [Verify]
    [ApiExplorerSettings(GroupName = "sys")]
    [Route("monitor/UserOnlineLog")]
    public class UserOnlineLogController : BaseController
    {
        /// <summary>
        /// 用户在线时长接口
        /// </summary>
        private readonly IUserOnlineLogService _UserOnlineLogService;

        public UserOnlineLogController(IUserOnlineLogService UserOnlineLogService)
        {
            _UserOnlineLogService = UserOnlineLogService;
        }

        /// <summary>
        /// 查询用户在线时长列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        //[ActionPermissionFilter(Permission = "useronlinelog:list")]
        public IActionResult QueryUserOnlineLog([FromQuery] UserOnlineLogQueryDto parm)
        {
            var response = _UserOnlineLogService.GetList(parm);
            return SUCCESS(response);
        }

        /// <summary>
        /// 删除用户在线时长
        /// </summary>
        /// <returns></returns>
        [HttpDelete("delete/{ids}")]
        [ActionPermissionFilter(Permission = "useronlinelog:delete")]
        [Log(Title = "用户在线时长", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteUserOnlineLog([FromRoute]string ids)
        {
            var idArr = Tools.SplitAndConvert<long>(ids);

            return ToResponse(_UserOnlineLogService.Delete(idArr));
        }

        /// <summary>
        /// 导出用户在线时长
        /// </summary>
        /// <returns></returns>
        [Log(Title = "用户在线时长", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "useronlinelog:export")]
        public IActionResult Export([FromQuery] UserOnlineLogQueryDto parm)
        {
            parm.PageNum = 1;
            parm.PageSize = 100000;
            var list = _UserOnlineLogService.ExportList(parm).Result;
            if (list == null || list.Count <= 0)
            {
                return ToResponse(ResultCode.FAIL, "没有要导出的数据");
            }
            var result = ExportExcelMini(list, "用户在线时长", "用户在线时长");
            return ExportExcel(result.Item2, result.Item1);
        }

    }
}