using Newtonsoft.Json;
using System.ComponentModel;

namespace ZRModel
{
    /// <summary>
    /// 通用json格式实体返回类
    /// Author by zhaorui
    /// </summary>
    public class ApiResult
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        /// <summary>
        /// 如果data值为null，则忽略序列化将不会返回data字段
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object Data { get; set; }

        public ApiResult()
        {
            Code = 0;
            Msg = "fail";
        }

        public ApiResult OnSuccess()
        {
            Code = 100;
            Msg = "success";
            return this;
        }
        public ApiResult(int code, string msg, object data = null)
        {
            Code = code;
            Msg = msg;
            Data = data;
        }

        public ApiResult OnSuccess(object data = null)
        {
            Code = (int)ResultCode.SUCCESS;
            Msg = "SUCCESS";
            Data = data;
            return this;
        }

        /// <summary>
        /// 返回成功消息
        /// </summary>
        /// <param name="data">数据对象</param>
        /// <returns>成功消息</returns>
        public static ApiResult Success(object data) { return new ApiResult(100, "success", data); }

        /// <summary>
        /// 返回成功消息
        /// </summary>
        /// <param name="msg">返回内容</param>
        /// <returns>成功消息</returns>
        public static ApiResult Success(string msg) { return new ApiResult(100, msg, null); }

        /// <summary>
        /// 返回成功消息
        /// </summary>
        /// <param name="msg">返回内容</param>
        /// <param name="data">数据对象</param>
        /// <returns>成功消息</returns>
        public static ApiResult Success(string msg, object data) { return new ApiResult(100, msg, data); }

        /// <summary>
        /// 返回失败消息
        /// </summary>
        /// <param name="code"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static ApiResult Error(int code, string msg) { return new ApiResult(code, msg); }

        /// <summary>
        /// 返回失败消息
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static ApiResult Error(string msg) { return new ApiResult(200, msg); }

    }
    public enum ResultCode
    {
        [Description("success")]
        SUCCESS = 100,
        [Description("参数错误")]
        PARAM_ERROR = 101,
        [Description("自定义错误")]
        CUSTOM_ERROR = 200,
        FAIL = 0,
        [Description("程序出错啦")]
        GLOBAL_ERROR = 104,
        [Description("请先绑定手机号")]
        NOBIND_PHONENUM = 102,
        [Description("授权失败")]
        OAUTH_FAIL = 201,
        [Description("未授权")]
        DENY = 401
    }
}
