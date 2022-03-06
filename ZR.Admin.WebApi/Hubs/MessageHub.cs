using Infrastructure.Constant;
using Infrastructure.Model;
using Microsoft.AspNetCore.SignalR;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Hubs
{
    public class MessageHub : Hub
    {
        //创建用户集合，用于存储所有链接的用户数据
        private static readonly List<OnlineUsers> clientUsers = new();
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private ISysNoticeService SysNoticeService;

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
            var name = Context.User?.Identity?.Name;

            var user = clientUsers.Any(u => u.ConnnectionId == Context.ConnectionId);
            //判断用户是否存在，否则添加集合
            if (!user && Context.User.Identity.IsAuthenticated)
            {
                clientUsers.Add(new OnlineUsers(Context.ConnectionId, name));
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
        public override Task OnDisconnectedAsync(Exception exception)
        {
            var user = clientUsers.Where(p => p.ConnnectionId == Context.ConnectionId).FirstOrDefault();
            //判断用户是否存在，否则添加集合
            if (user != null)
            {
                Console.WriteLine($"用户{user?.Name}离开了，当前已连接{clientUsers.Count}个");
                clientUsers.Remove(user);
                Clients.All.SendAsync(HubsConstant.OnlineNum, clientUsers.Count);
            }
            return base.OnDisconnectedAsync(exception);
        }

        #endregion
    }
}
