using IPTools.Core;
using Microsoft.AspNetCore.SignalR;
using System.Web;
using UAParser;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Hubs
{
    /// <summary>
    /// msghub
    /// </summary>
    public class MessageHub : Hub
    {
        //创建用户集合，用于存储所有链接的用户数据
        public static readonly List<OnlineUsers> onlineClients = new();
        public static List<OnlineUsers> users = new();
        //private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly ISysNoticeService SysNoticeService;

        public MessageHub(ISysNoticeService noticeService)
        {
            SysNoticeService = noticeService;
        }

        private ApiResult SendNotice()
        {
            var result = SysNoticeService.GetSysNotices();

            return new ApiResult(200, "success", result);
        }

        #region 客户端连接

        /// <summary>
        /// 客户端连接的时候调用
        /// </summary>
        /// <returns></returns>
        public override Task OnConnectedAsync()
        {
            var name = HttpContextExtension.GetName(App.HttpContext);
            var ip = HttpContextExtension.GetClientUserIp(App.HttpContext);
            var ip_info = IpTool.Search(ip);

            ClientInfo clientInfo = HttpContextExtension.GetClientInfo(App.HttpContext);
            string device = clientInfo.ToString();
            string qs = HttpContextExtension.GetQueryString(App.HttpContext);
            string from = HttpUtility.ParseQueryString(qs).Get("from") ?? "web";

            long userid = HttpContextExtension.GetUId(App.HttpContext);
            string uuid = device + userid + ip;
            var user = onlineClients.Any(u => u.ConnnectionId == Context.ConnectionId);
            var user2 = onlineClients.Any(u => u.Uuid == uuid);

            //判断用户是否存在，否则添加集合!user2 && !user && 
            if (!user2 && !user && Context.User.Identity.IsAuthenticated)
            {
                OnlineUsers onlineUser = new(Context.ConnectionId, name, userid, ip, device)
                {
                    Location = ip_info.City,
                    Uuid = uuid,
                    Platform = from
                };
                onlineClients.Add(onlineUser);
                Log.WriteLine(msg: $"{DateTime.Now}：{name},{Context.ConnectionId}连接服务端success，当前已连接{onlineClients.Count}个");
                //Clients.All.SendAsync("welcome", $"欢迎您：{name},当前时间：{DateTime.Now}");
                Clients.Caller.SendAsync(HubsConstant.MoreNotice, SendNotice());
                Clients.Caller.SendAsync(HubsConstant.ConnId, onlineUser.ConnnectionId);
            }
            OnlineUsers? userInfo = GetUserById(userid);
            if (userInfo == null)
            {
                userInfo = new OnlineUsers() { Userid = userid, Name = name, LoginTime = DateTime.Now };
                users.Add(userInfo);
            }
            else
            {
                if (userInfo.LoginTime <= Convert.ToDateTime(DateTime.Now.ToShortDateString()))
                {
                    userInfo.LoginTime = DateTime.Now;
                    userInfo.TodayOnlineTime = 0;
                }
                var clientUser = onlineClients.Find(x => x.Userid == userid);
                userInfo.TodayOnlineTime += Math.Round(clientUser?.OnlineTime ?? 0, 2);
            }
            //给当前所有登录当前账号的用户下发登录时长
            var connIds = onlineClients.Where(f => f.Userid == userid).ToList();
            userInfo.ClientNum = connIds.Count;

            Clients.Clients(connIds.Select(f => f.ConnnectionId)).SendAsync("onlineInfo", userInfo);

            Log.WriteLine(ConsoleColor.Blue, msg: $"用户{name}已连接，今日已在线{userInfo?.TodayOnlineTime}分钟，当前已连接{onlineClients.Count}个");
            //给所有用户更新在线人数
            Clients.All.SendAsync(HubsConstant.OnlineNum, new
            {
                num = onlineClients.Count,
                onlineClients
            });
            return base.OnConnectedAsync();
        }

        /// <summary>
        /// 连接终止时调用。
        /// </summary>
        /// <returns></returns>
        public override Task OnDisconnectedAsync(Exception? exception)
        {
            var user = onlineClients.Where(p => p.ConnnectionId == Context.ConnectionId).FirstOrDefault();
            if (user != null)
            {
                onlineClients.Remove(user);
                //给所有用户更新在线人数
                Clients.All.SendAsync(HubsConstant.OnlineNum, new
                {
                    num = onlineClients.Count,
                    onlineClients,
                    leaveUser = user
                });

                //累计用户时长
                OnlineUsers? userInfo = GetUserById(user.Userid);
                if (userInfo != null)
                {
                    userInfo.TodayOnlineTime += user?.OnlineTime ?? 0;
                }
                Log.WriteLine(ConsoleColor.Red, msg: $"用户{user?.Name}离开了,已在线{userInfo?.TodayOnlineTime}分，当前已连接{onlineClients.Count}个");
            }
            return base.OnDisconnectedAsync(exception);
        }

        #endregion

        /// <summary>
        /// 发送信息
        /// </summary>
        /// <param name="toConnectId">对方链接id</param>
        /// <param name="toUserId"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [HubMethodName("sendMessage")]
        public async Task SendMessage(string toConnectId, long toUserId, string message)
        {
            var userName = HttpContextExtension.GetName(App.HttpContext);
            long userid = HttpContextExtension.GetUId(App.HttpContext);
            var toUserList = onlineClients.Where(p => p.Userid == toUserId);
            var toUserInfo = toUserList.FirstOrDefault();
            IList<string> sendToUser = toUserList.Select(x => x.ConnnectionId).ToList();
            sendToUser.Add(GetConnectId());
            if (toUserInfo != null)
            {
                await Clients.Clients(sendToUser)
                    .SendAsync("receiveChat", new
                    {
                        msgType = 0,//文本
                        chatid = Guid.NewGuid().ToString(),
                        userName,
                        userid,
                        toUserName = toUserInfo.Name,
                        toUserid = toUserInfo.Userid,
                        message,
                        chatTime = DateTime.Now
                    });
            }
            else
            {
                //TODO 存储离线消息
                Console.WriteLine($"{toUserId}不在线");
            }

            Console.WriteLine($"用户{userName}对{toConnectId}-{toUserId}说：{message}");
        }

        private OnlineUsers GetUserByConnId(string connId)
        {
            return onlineClients.Where(p => p.ConnnectionId == connId).FirstOrDefault();
        }
        private static OnlineUsers? GetUserById(long userid)
        {
            return users.Where(f => f.Userid == userid).FirstOrDefault();
        }

        /// <summary>
        /// 移动端使用获取链接id
        /// </summary>
        /// <returns></returns>
        [HubMethodName("getConnId")]
        public string GetConnectId()
        {
            return Context.ConnectionId;
        }

        /// <summary>
        /// 退出其他设备登录
        /// </summary>
        /// <returns></returns>
        [HubMethodName("logOut")]
        public async Task LogOut()
        {
            var singleLogin = AppSettings.Get<bool>("singleLogin");
            long userid = HttpContextExtension.GetUId(App.HttpContext);
            if (singleLogin)
            {
                var onlineUsers = onlineClients.Where(p => p.ConnnectionId != Context.ConnectionId && p.Userid == userid);
                await Clients.Clients(onlineUsers.Select(x => x.ConnnectionId))
                    .SendAsync("logOut");
            }
        }
    }
}
