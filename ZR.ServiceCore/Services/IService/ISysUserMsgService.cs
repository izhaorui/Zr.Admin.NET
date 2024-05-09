using ZR.Infrastructure.Enums;
using ZR.Model;
using ZR.ServiceCore.Model.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 用户系统消息service接口
    /// </summary>
    public interface ISysUserMsgService : IBaseService<SysUserMsg>
    {
        PagedInfo<SysUserMsgDto> GetList(SysUserMsgQueryDto parm);

        SysUserMsg GetInfo(long MsgId);
        int ReadMsg(long userId, long msgId);

        SysUserMsg AddSysUserMsg(SysUserMsg parm);
        SysUserMsg AddSysUserMsg(long userId, string content, UserMsgType msgType);
        bool TruncateSysUserMsg();


        PagedInfo<SysUserMsgDto> ExportList(SysUserMsgQueryDto parm);
    }
}
