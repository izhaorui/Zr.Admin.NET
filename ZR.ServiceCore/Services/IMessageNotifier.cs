using System.Threading.Tasks;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 实时消息推送：按接收人 userId（多租户按 TenantId 隔离）推送到其全部在线 SignalR 连接。
    /// 仅服务 SysUserMsg 类消息（系统/私信/评论/点赞等），与待办提醒无关。
    /// </summary>
    public interface IMessageNotifier
    {
        /// <summary>
        /// 向指定用户推送消息负载（payload 一般为 SysUserMsgDto）
        /// </summary>
        Task NotifyUserAsync(long userId, object payload);
    }
}
