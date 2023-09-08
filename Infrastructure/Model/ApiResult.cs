using Infrastructure.Constant;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Infrastructure.Model
{
    public class ApiResult : Dictionary<string, object>
    {
        /** 状态码 */
        public static readonly string CODE_TAG = "code";

        /** 返回内容 */
        public static readonly string MSG_TAG = "msg";

        /** 数据对象 */
        public static readonly string DATA_TAG = "data";
        //public int Code { get; set; }
        //public string Msg { get; set; }
        /// <summary>
        /// 如果data值为null，则忽略序列化将不会返回data字段
        /// </summary>
        //[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        //public object Data { get; set; }

        /// <summary>
        /// 初始化一个新创建的APIResult对象，使其表示一个空消息
        /// </summary>
        public ApiResult()
        {
        }

        /// <summary>
        /// 初始化一个新创建的 ApiResult 对象
        /// </summary>
        /// <param name="code"></param>
        /// <param name="msg"></param>
        public ApiResult(int code, string msg)
        {
            Add(CODE_TAG, code);
            Add(MSG_TAG, msg);
        }

        /// <summary>
        /// 初始化一个新创建的 ApiResult 对象
        /// </summary>
        /// <param name="code"></param>
        /// <param name="msg"></param>
        /// <param name="data"></param>
        public ApiResult(int code, string msg, object data)
        {
            Add(CODE_TAG, code);
            Add(MSG_TAG, msg);
            if (data != null)
            {
                Add(DATA_TAG, data);
            }
        }
        /// <summary>
        /// 返回成功消息
        /// </summary>
        /// < returns > 成功消息 </ returns >
        public static ApiResult Success() { return new ApiResult(HttpStatus.SUCCESS, "success"); }

        /// <summary>
        /// 返回成功消息
        /// </summary>
        /// <param name="data"></param>
        /// <returns> 成功消息 </returns >
        public static ApiResult Success(object data) { return new ApiResult(HttpStatus.SUCCESS, "success", data); }

        /// <summary>
        /// 返回成功消息
        /// </summary>
        /// <param name="msg">返回内容</param>
        /// <returns>成功消息</returns>
        public static ApiResult Success(string msg) { return new ApiResult(HttpStatus.SUCCESS, msg, null); }

        /// <summary>
        /// 返回成功消息
        /// </summary>
        /// <param name="msg">返回内容</param>
        /// <param name="data">数据对象</param>
        /// <returns>成功消息</returns>
        public static ApiResult Success(string msg, object data) { return new ApiResult(HttpStatus.SUCCESS, msg, data); }

        public static ApiResult Error(ResultCode code, string msg = "")
        {
            return Error((int)code, msg);
        }

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
        public static ApiResult Error(string msg) { return new ApiResult((int)ResultCode.CUSTOM_ERROR, msg); }


        /// <summary>
        /// 是否为成功消息
        /// </summary>
        /// <returns></returns>
        public bool IsSuccess()
        {
            return HttpStatus.SUCCESS == (int)this[CODE_TAG];
        }

        /// <summary>
        /// 方便链式调用
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public ApiResult Put(string key, object value)
        {
            Add(key, value);
            return this;
        }
    }

    public class ApiResult<T> : ApiResult
    {
        public T Result { get; set; }
    }
}
