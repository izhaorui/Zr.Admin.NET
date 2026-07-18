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
        #region 静态字段

        /// <summary>
        /// 在线连接客户端 Key=ConnectionId
        /// </summary>
        public static readonly ConcurrentDictionary<string, OnlineUsers> OnlineClients = new();

        /// <summary>
        /// 用户信息缓存 Key=UserId，用于统计今日在线时长
        /// </summary>
        private static readonly ConcurrentDictionary<long, OnlineUsers> Users = new();

        #endregion

        #region 依赖注入

        private readonly ISysNoticeService _sysNoticeService;
        private readonly ISysUserService _userService;
        private readonly IUserOnlineLogService _userOnlineLogService;

        public MessageHub(ISysNoticeService noticeService, ISysUserService userService, IUserOnlineLogService userOnlineLogService)
        {
            _sysNoticeService = noticeService;
            _userService = userService;
            _userOnlineLogService = userOnlineLogService;
        }

        #endregion

        #region 连接生命周期

        /// <summary>
        /// 客户端连接
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            try
            {
                var context = App.HttpContext;

                // 未认证直接忽略
                if (context?.User?.Identity?.IsAuthenticated != true) return;
                // 已存在相同连接忽略
                if (OnlineClients.ContainsKey(Context.ConnectionId)) return;

                var name = HttpContextExtension.GetName(context);
                var ip = HttpContextExtension.GetClientUserIp(context);
                var device = HttpContextExtension.GetClientInfo(context).ToString();
                var tenantId = App.GetCurrentTenantId();
                var qs = HttpUtility.ParseQueryString(HttpContextExtension.GetQueryString(context));
                var from = qs.Get("from") ?? "web";
                var clientId = qs.Get("clientId");
                long userId = HttpContextExtension.GetUId(context);
                string uuid = $"{device}{userId}{ip}";

                var ipInfo = IpTool.Search(ip);
                var onlineUser = new OnlineUsers(Context.ConnectionId, name, userId, ip, device)
                {
                    Location = ipInfo?.City,
                    Uuid = uuid,
                    Platform = from,
                    ClientId = clientId ?? Context.ConnectionId,
                    TenantId = tenantId
                };

                OnlineClients[Context.ConnectionId] = onlineUser;

                // 多租户模式下将连接加入租户分组，广播仅推送到同租户客户端
                if (App.IsTenantEnabled() && !string.IsNullOrWhiteSpace(tenantId))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, GetTenantGroup(tenantId));
                }

                // 更新用户今日在线缓存
                var userCache = Users.GetOrAdd(userId, _ => new OnlineUsers { Userid = userId, Name = name, LoginTime = DateTime.Now });
                ResetDailyOnlineTimeIfNeeded(userCache);

                // 推送通知 & 在线人数
                await Clients.Caller.SendAsync(HubsConstant.MoreNotice, BuildNoticeResult());
                await BroadcastOnlineUsersAsync(tenantId);
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Red, $"[OnConnectedAsync] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 客户端断开
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            try
            {
                if (!OnlineClients.TryRemove(Context.ConnectionId, out var user)) return;

                if (Users.TryGetValue(user.Userid, out var userCache))
                {
                    userCache.TodayOnlineTime += user.OnlineTime;
                    await _userOnlineLogService.AddUserOnlineLog(
                        new UserOnlineLog { TodayOnlineTime = Math.Round(userCache.TodayOnlineTime, 2) },
                        user);
                }

                var tenantId = user.TenantId;
                await BroadcastOnlineUsersAsync(tenantId, user);
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Red, $"[OnDisconnectedAsync] Error: {ex.Message}");
            }
        }

        #endregion

        #region Hub 方法

        /// <summary>
        /// 发送聊天消息
        /// </summary>
        [HubMethodName("sendMessage")]
        public async Task SendMessage(long toUserId, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message)) return;

                var context = App.HttpContext;
                long userId = HttpContextExtension.GetUId(context);
                var fromUser = await _userService.GetByIdAsync(userId);
                var tenantId = App.GetCurrentTenantId();

                // 目标用户的所有连接 + 发送者自己（多租户模式下限制同租户）
                var toConnections = OnlineClients.Values
                    .Where(u => u.Userid == toUserId && (string.IsNullOrWhiteSpace(tenantId) || string.Equals(u.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)))
                    .Select(u => u.ConnnectionId)
                    .ToList();
                toConnections.Add(Context.ConnectionId);

                var messageDto = new ChatMessageDto
                {
                    MsgType = 0,
                    StoredKey = $"{userId}-{toUserId}",
                    UserId = userId,
                    ChatId = Guid.NewGuid().ToString(),
                    ToUserId = toUserId,
                    Message = message,
                    Online = toConnections.Count > 1 ? 1 : 0,
                    ChatTime = DateTimeHelper.GetUnixTimeSeconds(DateTime.Now),
                    FromUser = fromUser.Adapt<ChatUserDto>()
                };

                if (messageDto.Online == 0)
                {
                    await StoreOfflineMessageAsync(messageDto);
                }
                else
                {
                    await Clients.Clients(toConnections).SendAsync("receiveChat", messageDto);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(ConsoleColor.Red, $"[SendMessage] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前连接 ID
        /// </summary>
        [HubMethodName("getConnId")]
        public string GetConnectId() => Context.ConnectionId;

        /// <summary>
        /// 单点登录踢出其他设备
        /// </summary>
        [HubMethodName("logOut")]
        public async Task LogOut()
        {
            var singleLogin = AppSettings.Get<bool>("singleLogin");
            if (!singleLogin) return;

            long userId = HttpContextExtension.GetUId(App.HttpContext);
            var tenantId = App.GetCurrentTenantId();
            var otherConnections = OnlineClients.Values
                .Where(u => u.Userid == userId && u.ConnnectionId != Context.ConnectionId
                    && (string.IsNullOrWhiteSpace(tenantId) || string.Equals(u.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)))
                .Select(u => u.ConnnectionId)
                .ToList();

            if (otherConnections.Count > 0)
            {
                await Clients.Clients(otherConnections).SendAsync("logOut");
            }
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 构建通知返回结果
        /// </summary>
        private ApiResult BuildNoticeResult()
        {
            var result = _sysNoticeService.GetSysNotices();
            return new ApiResult(200, "success", result);
        }

        /// <summary>
        /// 获取租户 SignalR 分组名
        /// </summary>
        private static string GetTenantGroup(string tenantId)
        {
            return string.IsNullOrWhiteSpace(tenantId) ? "all" : $"tenant_{tenantId}";
        }

        /// <summary>
        /// 过滤指定租户的在线客户端
        /// </summary>
        private static IEnumerable<OnlineUsers> FilterOnlineClientsByTenant(string tenantId)
        {
            if (!App.IsTenantEnabled() || string.IsNullOrWhiteSpace(tenantId))
                return OnlineClients.Values;

            return OnlineClients.Values
                .Where(u => string.Equals(u.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 广播在线人数（按租户隔离，仅推送给同租户客户端）
        /// </summary>
        private async Task BroadcastOnlineUsersAsync(string tenantId, OnlineUsers leaveUser = null)
        {
            var filteredClients = FilterOnlineClientsByTenant(tenantId).ToList();
            var payload = new
            {
                num = filteredClients.Count,
                OnlineClients = filteredClients,
                leaveUser
            };

            if (App.IsTenantEnabled())
            {
                var group = GetTenantGroup(tenantId);
                await Clients.Group(group).SendAsync(HubsConstant.OnlineNum, payload);
            }
            else
            {
                await Clients.All.SendAsync(HubsConstant.OnlineNum, payload);
            }
        }

        /// <summary>
        /// 跨天重置今日在线时长
        /// </summary>
        private static void ResetDailyOnlineTimeIfNeeded(OnlineUsers userCache)
        {
            if (userCache.LoginTime.Date < DateTime.Today)
            {
                userCache.LoginTime = DateTime.Now;
                userCache.TodayOnlineTime = 0;
            }
        }

        /// <summary>
        /// 存储离线消息（待扩展：持久化到数据库）
        /// </summary>
        private static Task StoreOfflineMessageAsync(ChatMessageDto message)
        {
            Log.WriteLine(ConsoleColor.Yellow, $"[OfflineMsg] ToUser={message.ToUserId}, Msg={message.Message}");
            // TODO: 持久化离线消息到数据库
            return Task.CompletedTask;
        }

        #endregion
    }
}
