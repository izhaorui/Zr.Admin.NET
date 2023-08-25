namespace ZR.Admin.WebApi.Hubs
{
    public class OnlineUsers
    {
        /// <summary>
        /// 客户端连接Id
        /// </summary>
        public string ConnnectionId { get; set; }
        /// <summary>
        /// 用户id
        /// </summary>
        public long? Userid { get; set; }
        public string Name { get; set; }
        public DateTime LoginTime { get; set; }
        public string UserIP { get; set; }
        /// <summary>
        /// 登录地点
        /// </summary>
        public string? Location { get; set; }

        /// <summary>
        /// 判断用户唯一
        /// </summary>
        public string? Uuid{ get; set; }
        /// <summary>
        /// 浏览器
        /// </summary>
        public string Browser { get; set; }
        /// <summary>
        /// 平台
        /// </summary>
        public string Platform { get; set; } = string.Empty;

        public OnlineUsers(string clientid, string name, long? userid, string userip, string browser)
        {
            ConnnectionId = clientid;
            Name = name;
            LoginTime = DateTime.Now;
            Userid = userid;
            UserIP = userip;
            Browser = browser;
        }
    }
}
