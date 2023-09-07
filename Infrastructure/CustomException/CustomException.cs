using System;

namespace Infrastructure
{
    public class CustomException : Exception
    {
        public int Code { get; set; }
        /// <summary>
        /// 前端提示语
        /// </summary>
        public string Msg { get; set; }
        /// <summary>
        /// 记录到日志的详细内容
        /// </summary>
        public string LogMsg { get; set; }
        /// <summary>
        /// 是否通知
        /// </summary>
        public bool Notice { get; set; } = true;

        public CustomException(string msg) : base(msg)
        {
        }
        public CustomException(int code, string msg) : base(msg)
        {
            Code = code;
            Msg = msg;
        }

        public CustomException(ResultCode resultCode, string msg, bool notice = true) : base(msg)
        {
            Code = (int)resultCode;
            Notice = notice;
        }

        /// <summary>
        /// 自定义异常
        /// </summary>
        /// <param name="resultCode"></param>
        /// <param name="msg"></param>
        /// <param name="errorMsg">用于记录详细日志到输出介质</param>
        public CustomException(ResultCode resultCode, string msg, object errorMsg) : base(msg)
        {
            Code = (int)resultCode;
            LogMsg = errorMsg.ToString();
        }
    }
}