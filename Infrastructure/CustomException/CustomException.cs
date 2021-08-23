using System;

namespace Infrastructure
{
    public class CustomException : Exception
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public string LogMsg { get; set; }
        
        public CustomException(string msg) : base(msg)
        {
        }
        public CustomException(int code, string msg) : base(msg)
        {
            Code = code;
            Msg = msg;
        }

        public CustomException(ResultCode resultCode, string msg) : base(msg)
        {
            Code = (int)resultCode;
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