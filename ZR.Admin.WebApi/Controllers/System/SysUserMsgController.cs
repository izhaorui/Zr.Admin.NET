using Microsoft.AspNetCore.Mvc;
using ZR.Admin.WebApi.Filters;
using ZR.Model;
using ZR.Model.Dto;

//创建时间：2024-05-08
namespace ZR.Admin.WebApi.Controllers.System
{
    /// <summary>
    /// 用户系统消息
    /// </summary>
    [Verify]
    [Route("SysUserMsg")]
    public class SysUserMsgController : BaseController
    {
        /// <summary>
        /// 用户系统消息接口
        /// </summary>
        private readonly ISysUserMsgService _SysUserMsgService;

        public SysUserMsgController(ISysUserMsgService SysUserMsgService)
        {
            _SysUserMsgService = SysUserMsgService;
        }

        /// <summary>
        /// 查询用户系统消息列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "usermsg:list")]
        public IActionResult QuerySysUserMsg([FromQuery] SysUserMsgQueryDto parm)
        {
            var response = _SysUserMsgService.GetList(parm);
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询我的系统消息
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("mylist")]
        public IActionResult QueryMySysUserMsg([FromQuery] SysUserMsgQueryDto parm)
        {
            parm.UserId = HttpContext.GetUId();
            var response = _SysUserMsgService.GetList(parm);
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询我的未读消息数
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("myMsgNum")]
        public IActionResult QueryMyUnReadMsg([FromQuery] SysUserMsgQueryDto parm)
        {
            parm.UserId = HttpContext.GetUId();
            var response = _SysUserMsgService
                .Queryable()
                .Where(f => f.IsRead == 0 && f.UserId == parm.UserId)
                .WithCache(60 * 10)
                .ToList();
            var data = from a in response
                       group a by new { a.MsgType } into grp
                       select new
                       {
                           msgType = grp.Key.MsgType,
                           num = grp.Count(),
                           d = 3
                       };
            var lastSysMsgInfo = _SysUserMsgService
                .Queryable()
                .Where(f => f.MsgType == UserMsgType.SYSTEM)
                .OrderByDescending(x => x.MsgId)
                .First();

            return SUCCESS(new { data, lastSysMsgInfo });
        }

        /// <summary>
        /// 查询用户系统消息详情
        /// </summary>
        /// <param name="MsgId"></param>
        /// <returns></returns>
        [HttpGet("{MsgId}")]
        [ActionPermissionFilter(Permission = "usermsg:query")]
        public IActionResult GetSysUserMsg(long MsgId)
        {
            var response = _SysUserMsgService.GetInfo(MsgId);

            var info = response.Adapt<SysUserMsgDto>();
            return SUCCESS(info);
        }

        /// <summary>
        /// 添加用户系统消息
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ActionPermissionFilter(Permission = "usermsg:add")]
        [Log(Title = "用户系统消息", BusinessType = BusinessType.INSERT)]
        public IActionResult AddSysUserMsg([FromBody] SysUserMsgDto parm)
        {
            var modal = parm.Adapt<SysUserMsg>().ToCreate(HttpContext);

            var response = _SysUserMsgService.AddSysUserMsg(modal);

            return SUCCESS(response);
        }

        /// <summary>
        /// 删除用户系统消息
        /// </summary>
        /// <returns></returns>
        [HttpDelete("delete/{ids}")]
        [ActionPermissionFilter(Permission = "usermsg:delete")]
        [Log(Title = "用户系统消息", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteSysUserMsg([FromRoute] string ids)
        {
            var idArr = Tools.SplitAndConvert<long>(ids);

            return ToResponse(_SysUserMsgService.Delete(idArr));
        }

        /// <summary>
        /// 已读消息
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="msgType"></param>
        /// <returns></returns>
        [HttpPost("read/{msgId}/{msgType}")]
        [ActionPermissionFilter(Permission = "common")]
        public IActionResult ReadMsg([FromRoute] long msgId, [FromRoute] UserMsgType msgType)
        {
            var userId = HttpContext.GetUId();
            var response = _SysUserMsgService.ReadMsg(userId, msgId, msgType);

            return SUCCESS(response);
        }

        /// <summary>
        /// 导出用户系统消息
        /// </summary>
        /// <returns></returns>
        [Log(Title = "用户系统消息", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "usermsg:export")]
        public IActionResult Export([FromQuery] SysUserMsgQueryDto parm)
        {
            parm.PageNum = 1;
            parm.PageSize = 100000;
            var list = _SysUserMsgService.ExportList(parm).Result;
            if (list == null || list.Count <= 0)
            {
                return ToResponse(ResultCode.FAIL, "没有要导出的数据");
            }
            var result = ExportExcelMini(list, "用户系统消息", "用户系统消息");
            return ExportExcel(result.Item2, result.Item1);
        }

        /// <summary>
        /// 清空用户系统消息
        /// </summary>
        /// <returns></returns>
        [Log(Title = "用户系统消息", BusinessType = BusinessType.CLEAN)]
        [ActionPermissionFilter(Permission = "usermsg:delete")]
        [HttpDelete("clean")]
        public IActionResult Clear()
        {
            if (!HttpContext.IsAdmin())
            {
                return ToResponse(ResultCode.FAIL, "操作失败");
            }
            return SUCCESS(_SysUserMsgService.TruncateSysUserMsg());
        }

    }
}