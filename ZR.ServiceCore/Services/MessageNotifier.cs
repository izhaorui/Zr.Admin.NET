using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure;
using Infrastructure.Attribute;
using Microsoft.AspNetCore.SignalR;
using ZR.ServiceCore.Signalr;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 实时消息推送实现：从 MessageHub.OnlineClients 按 userId(+租户) 筛选连接并 SendAsync(ReceiveMessage)。
    /// 推送异常被吞掉并记录日志，不影响消息落库。
    /// </summary>
    [AppService(ServiceType = typeof(IMessageNotifier), ServiceLifetime = LifeTime.Scoped)]
    public class MessageNotifier : IMessageNotifier
    {
        private readonly IHubContext<MessageHub> _hubContext;

        public MessageNotifier(IHubContext<MessageHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task NotifyUserAsync(long userId, object payload)
        {
            try
            {
                var tenantId = App.GetCurrentTenantId();
                var conns = MessageHub.OnlineClients.Values
                    .Where(u => u.Userid == userId
                        && (string.IsNullOrWhiteSpace(tenantId) || string.Equals(u.TenantId, tenantId, System.StringComparison.OrdinalIgnoreCase)))
                    .Select(u => u.ConnnectionId)
                    .ToList();

                if (conns.Count == 0) return Task.CompletedTask;

                return _hubContext.Clients.Clients(conns).SendAsync(HubsConstant.ReceiveMessage, payload);
            }
            catch (System.Exception ex)
            {
                Log.WriteLine(ConsoleColor.Yellow, $"[MessageNotifier] NotifyUser Error: {ex.Message}");
                return Task.CompletedTask;
            }
        }
    }
}
