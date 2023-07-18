namespace ZR.Common.Model
{
    public class WxTokenResult
    {
        /// <summary>
        /// 0、正常
        /// </summary>
        public int errcode { get; set; }
        public string errmsg { get; set; }
        public string access_token { get; set; }
        public string ticket { get; set; }
    }
}
