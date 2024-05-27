namespace ZR.Model.System.Dto
{
    public class SysLogininfoQueryDto : PagerInfo
    {
        public string Status { get; set; }
        public long? UserId { get; set; }
        public string Ipaddr { get; set; } = string.Empty; 
        public string UserName { get; set; }
        public DateTime? BeginTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    public class SysLogininfoDto : SysBase
    {
        public int PageNum { get; set; }
        /// <summary>
        /// IP 地址
        /// </summary>
        public string Ipaddr { get; set; }
        /// <summary>
        /// 登录状态 0成功 1失败
        /// </summary>
        public string Status { get; set; }
        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }

    }
}
