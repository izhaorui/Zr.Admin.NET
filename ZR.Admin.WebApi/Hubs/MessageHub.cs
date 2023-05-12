using Infrastructure;
using Infrastructure.Constant;
using Infrastructure.Model;
using IPTools.Core;
using Microsoft.AspNetCore.SignalR;
using UAParser;
using ZR.Admin.WebApi.Extensions;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Hubs
{
    /// <summary>
    /// msghub
    /// </summary>
    public class MessageHub : Hub
    {
        //创建用户集合，用于存储所有链接的用户数据
        public static readonly List<OnlineUsers> clientUsers = new();
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
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

            var userid = HttpContextExtension.GetUId(App.HttpContext);
            string uuid = device + userid + ip;
            var user = clientUsers.Any(u => u.ConnnectionId == Context.ConnectionId);
            var user2 = clientUsers.Any(u => u.Uuid == uuid);

            //判断用户是否存在，否则添加集合!user2 && !user && 
            if (Context.User.Identity.IsAuthenticated)
            {
                OnlineUsers users = new(Context.ConnectionId, name, userid, ip, device)
                {
                    Location = ip_info.City,
                    Uuid = uuid
                };
                clientUsers.Add(users);
                Console.WriteLine($"{DateTime.Now}：{name},{Context.ConnectionId}连接服务端success，当前已连接{clientUsers.Count}个");
                //Clients.All.SendAsync("welcome", $"欢迎您：{name},当前时间：{DateTime.Now}");
                Clients.All.SendAsync(HubsConstant.MoreNotice, SendNotice());
            }

            Clients.All.SendAsync(HubsConstant.OnlineNum, clientUsers.Count);
            return base.OnConnectedAsync();
        }

        /// <summary>
        /// 连接终止时调用。
        /// </summary>
        /// <returns></returns>
        public override Task OnDisconnectedAsync(Exception? exception)
        {
            var user = clientUsers.Where(p => p.ConnnectionId == Context.ConnectionId).FirstOrDefault();
            //判断用户是否存在，否则添加集合
            if (user != null)
            {
                clientUsers.Remove(user);
                Clients.All.SendAsync(HubsConstant.OnlineNum, clientUsers.Count);

                Console.WriteLine($"用户{user?.Name}离开了，当前已连接{clientUsers.Count}个");
            }
            return base.OnDisconnectedAsync(exception);
        }

        #endregion

        /// <summary>
        /// 注册信息
        /// </summary>
        /// <param name="connectId"></param>
        /// <param name="userName"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        [HubMethodName("SendMessage")]
        public async Task SendMessage(string connectId, string userName, string message)
        {
            Console.WriteLine($"{connectId},message={message}");

            await Clients.Client(connectId).SendAsync("receiveChat", new { userName, message });
        }
    }
}
