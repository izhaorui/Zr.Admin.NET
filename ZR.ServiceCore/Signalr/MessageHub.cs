using Infrastructure;
using Infrastructure.Model;
using Mapster;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Web;
using ZR.Infrastructure.IPTools;
using ZR.Model.Dto;
using ZR.Model.Models;
using ZR.ServiceCore.Monitor.IMonitorService;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.Signalr
{
    public class MessageHub : Hub
    {
        public static readonly ConcurrentDictionary<string, OnlineUsers> OnlineClients = new();
        private static readonly ConcurrentDictionary<long, OnlineUsers> Users = new();

        private readonly ISysNoticeService _sysNoticeService;
        private readonly ISysUserService _userService;
        private readonly IUserOnlineLogService _userOnlineLogService;

        public MessageHub(ISysNoticeService noticeService, ISysUserService userService, IUserOnlineLogService userOnlineLogService)
        {
            _sysNoticeService = noticeService;
            _userService = userService;
            _userOnlineLogService = userOnlineLogService;
        }
        private ApiResult SendNotice()
        {
            var result = _sysNoticeService.GetSysNotices();

            return new ApiResult(200, "success", result);
        }
        public override async Task OnConnectedAsync()
        {
            try
            {
                var context = App.HttpContext;
                var name = HttpContextExtension.GetName(context);
                var ip = HttpContextExtension.GetClientUserIp(context);
                var device = HttpContextExtension.GetClientInfo(context).ToString();
                var qs = HttpUtility.ParseQueryString(HttpContextExtension.GetQueryString(context));
                var from = qs.Get("from") ?? "web";
                var clientId = qs.Get("clientId");
                long userId = HttpContextExtension.GetUId(context);
                string uuid = $"{device}{userId}{ip}";

                if (!Context.User.Identity.IsAuthenticated || OnlineClients.ContainsKey(Context.ConnectionId))
                    return;

                var ipInfo = IpTool.Search(ip);
                var onlineUser = new OnlineUsers(Context.ConnectionId, name, userId, ip, device)
                {
                    Location = ipInfo?.City,
                    Uuid = uuid,
                    Platform = from,
                    ClientId = clientId ?? Context.ConnectionId
                };

                OnlineClients[Context.ConnectionId] = onlineUser;
                var userInfo = Users.GetOrAdd(userId, _ => new OnlineUsers { Userid = userId, Name = name, LoginTime = DateTime.Now });
                UpdateUserOnlineTime(userInfo);

                await Clients.Caller.SendAsync(HubsConstant.MoreNotice, SendNotice());
                await Clients.All.SendAsync(HubsConstant.OnlineNum, new { num = OnlineClients.Count, OnlineClients });
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Red, $"OnConnectedAsync Error: {ex.Message}");
            }
        }

        private void UpdateUserOnlineTime(OnlineUsers userInfo)
        {
            if (userInfo.LoginTime <= DateTime.Today)
            {
                userInfo.LoginTime = DateTime.Now;
                userInfo.TodayOnlineTime = 0;
            }
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (OnlineClients.TryRemove(Context.ConnectionId, out var user))
            {
                if (Users.TryGetValue(user.Userid, out var userInfo))
                {
                    userInfo.TodayOnlineTime += user.OnlineTime;
                    await _userOnlineLogService.AddUserOnlineLog(new UserOnlineLog { TodayOnlineTime = Math.Round(userInfo.TodayOnlineTime, 2) }, user);
                }
                await Clients.All.SendAsync(HubsConstant.OnlineNum, new { num = OnlineClients.Count, OnlineClients, leaveUser = user });
            }
        }

        [HubMethodName("sendMessage")]
        public async Task SendMessage(long toUserId, string message)
        {
            try
            {
                var userName = HttpContextExtension.GetName(App.HttpContext);
                long userId = HttpContextExtension.GetUId(App.HttpContext);
                var fromUser = await _userService.GetByIdAsync(userId);
                var toUserConnections = OnlineClients.Values.Where(u => u.Userid == toUserId).Select(u => u.ConnnectionId).ToList();
                toUserConnections.Add(Context.ConnectionId);

                ChatMessageDto messageDto = new()
                {
                    MsgType = 0,
                    StoredKey = $"{userId}-{toUserId}",
                    UserId = userId,
                    ChatId = Guid.NewGuid().ToString(),
                    ToUserId = toUserId,
                    Message = message,
                    Online = toUserConnections.Count > 1 ? 1 : 0,
                    ChatTime = DateTimeHelper.GetUnixTimeSeconds(DateTime.Now),
                    FromUser = fromUser.Adapt<ChatUserDto>()
                };

                if (messageDto.Online == 0)
                {
                    await StoreOfflineMessage(messageDto);
                }
                else
                {
                    await Clients.Clients(toUserConnections).SendAsync("receiveChat", messageDto);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Red, $"SendMessage Error: {ex.Message}");
            }
        }

        private Task StoreOfflineMessage(ChatMessageDto message)
        {
            Console.WriteLine($"Storing offline message for {message.ToUserId}");
            return Task.CompletedTask;
        }

        [HubMethodName("getConnId")]
        public string GetConnectId() => Context.ConnectionId;

        [HubMethodName("logOut")]
        public async Task LogOut()
        {
            var singleLogin = AppSettings.Get<bool>("singleLogin");
            long userId = HttpContextExtension.GetUId(App.HttpContext);
            if (singleLogin)
            {
                var connections = OnlineClients.Values.Where(u => u.Userid == userId && u.ConnnectionId != Context.ConnectionId).Select(u => u.ConnnectionId).ToList();
                await Clients.Clients(connections).SendAsync("logOut");
            }
        }
    }
}
