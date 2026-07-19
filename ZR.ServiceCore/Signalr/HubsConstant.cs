namespace ZR.ServiceCore.Signalr
{
    public class HubsConstant
    {
        private const string V = "receiveNotice";
        public static string ReceiveNotice = V;
        public static string OnlineNum = "onlineNum";
        public static string MoreNotice = "moreNotice";
        public static string OnlineUser = "onlineUser";
        public static string LockUser = "lockUser";
        public static string ForceUser = "forceUser";
        public static string ConnId = "connId";
        public static string ReceiveMessage = "receiveMessage";
        /// <summary>
        /// 待办提醒：登录(SignalR 连接)时推送当前用户未完成待办数，仅用于前端红点，不写消息表
        /// </summary>
        public static string TodoReminder = "todoReminder";
    }
}
