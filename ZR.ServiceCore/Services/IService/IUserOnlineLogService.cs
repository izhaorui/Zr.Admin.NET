using ZR.Model;
using ZR.Model.Dto;
using ZR.Model.Models;
using ZR.ServiceCore.Signalr;

namespace ZR.ServiceCore.Monitor.IMonitorService
{
    /// <summary>
    /// 用户在线时长service接口
    /// </summary>
    public interface IUserOnlineLogService : IBaseService<UserOnlineLog>
    {
        PagedInfo<UserOnlineLogDto> GetList(UserOnlineLogQueryDto parm);

        Task<UserOnlineLog> AddUserOnlineLog(UserOnlineLog parm, OnlineUsers onlineUsers);

        PagedInfo<UserOnlineLogDto> ExportList(UserOnlineLogQueryDto parm);
    }
}
