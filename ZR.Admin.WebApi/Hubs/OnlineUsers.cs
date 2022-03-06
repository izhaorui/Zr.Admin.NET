using System;
using System.Collections.Generic;
using System.Text;

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
        public int Userid { get; set; }
        public string Name { get; set; }
        public DateTime LoginTime { get; set; }

        public OnlineUsers(string clientid, string name)
        {
            ConnnectionId = clientid;
            Name = name;
            LoginTime = DateTime.Now;
        }
    }
}
