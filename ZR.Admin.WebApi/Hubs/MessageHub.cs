using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Infrastructure.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ZR.Admin.WebApi.Filters;
using ZR.Model;

namespace ZR.Admin.WebApi.Hubs
{
    [Verify]
    public class MessageHub : Hub
    {
        //创建用户集合，用于存储所有链接的用户数据
        private static readonly List<OnlineUsers> clientUsers = new();

        #region 客户端连接

        /// <summary>
        /// 客户端连接的时候调用
        /// </summary>
        /// <returns></returns>
        public override Task OnConnectedAsync()
        {
            //name 获取不到有待研究
            var name = Context.User.Identity.Name;
            var user = clientUsers.Any(u => u.ConnnectionId == Context.ConnectionId);
            //判断用户是否存在，否则添加集合
            if (!user)
            {
                clientUsers.Add(new OnlineUsers(Context.ConnectionId, Context.User.Identity.Name));
                Console.WriteLine($"{DateTime.Now}：{Context.User.Identity.Name},{Context.ConnectionId}连接服务端success，当前已连接{clientUsers.Count}个");
            }

            Clients.All.SendAsync("onlineNum", clientUsers.Count);
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

                Clients.All.SendAsync("onlineNum", clientUsers.Count);
            }
            return base.OnDisconnectedAsync(exception);
        }

        #endregion
    }
}
