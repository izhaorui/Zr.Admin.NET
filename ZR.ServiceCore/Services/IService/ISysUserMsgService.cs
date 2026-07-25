using ZR.Model;
using ZR.Model.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 用户系统消息service接口
    /// </summary>
    public interface ISysUserMsgService : IBaseService<SysUserMsg>
    {
        PagedInfo<SysUserMsgDto> GetList(SysUserMsgQueryDto parm);

        SysUserMsg GetInfo(long MsgId);
        int ReadMsg(long userId, long msgId, UserMsgType msgType);

        SysUserMsg AddSysUserMsg(SysUserMsg parm);
        SysUserMsg AddSysUserMsg(long userId, string content, UserMsgType msgType);
        /// <summary>
        /// 添加系统消息（显式指定租户ID），供后台任务等无租户上下文场景使用，确保消息正确落入目标租户。
        /// </summary>
        SysUserMsg AddSysUserMsg(long userId, string content, UserMsgType msgType, string tenantId);
        bool TruncateSysUserMsg();


        PagedInfo<SysUserMsgDto> ExportList(SysUserMsgQueryDto parm);

        ///// <summary>
        ///// 未读消息数
        ///// </summary>
        //int UnreadCount(long userId);

        /// <summary>
        /// 删除消息（软删除，置 IsDelete=1）
        /// </summary>
        int DeleteByIds(long[] ids, long userId);

        /// <summary>
        /// 当前用户全部消息标记为已读
        /// </summary>
        int ReadAll(long userId);
    }
}
