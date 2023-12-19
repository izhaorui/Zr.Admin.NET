using Infrastructure;
using System.Collections.Generic;
using System.Text.Json;
using ZR.Common.Model;

namespace ZR.Common
{
    public class WxNoticeHelper
    {
        //CorpID  企业ID 
        //AGENTID 应用的ID
        //Secret 应用的ID对应的密钥
        private static readonly string AGENTID = AppSettings.App(new string[] { "WxCorp", "AgentID" });
        private static readonly string CORPID = AppSettings.App(new string[] { "WxCorp", "CorpID" });
        private static readonly string CORPSECRET = AppSettings.App(new string[] { "WxCorp", "CorpSecret" });
        private static readonly string SEND_USER = AppSettings.App(new string[] { "WxCorp", "SendUser" });
        private static readonly string SendUrl = "https://qyapi.weixin.qq.com/cgi-bin/message/send";
        private static readonly string GetTokenUrl = "https://qyapi.weixin.qq.com/cgi-bin/gettoken";

        /// <summary>
        /// 消息类型
        /// </summary>
        public enum MsgType { markdown, text, textcard, interactive_taskcard }

        /// <summary>
        /// 发送消息公共模板方法
        /// </summary>
        /// <param name="toUser">微信微信好友id，默认@all发给所有关注该应用的用户</param>
        /// <param name="title">标题</param>
        /// <param name="content">内容</param>
        /// <param name="msgType">消息类型</param>
        /// <returns></returns>
        public static (int, string) SendMsg(string title, string content, string toUser = "", MsgType msgType = MsgType.text)
        {
            if (string.IsNullOrEmpty(toUser))
            {
                toUser = SEND_USER;
            }
            if (string.IsNullOrEmpty(title))
            {
                return (0, "title不能为空");
            }
            if (string.IsNullOrEmpty(CORPID))
            {
                System.Console.WriteLine("如需微信接收异常消息，请完成企业微信配置");
                return (0, "请完成企业微信通知配置");
            }
            WxTokenResult tokenResult = GetAccessToken();

            if (tokenResult == null || tokenResult.errcode != 0)
            {
                return (0, tokenResult?.errmsg);
            }

            Dictionary<string, object> dic = null;
            switch (msgType)
            {
                case MsgType.markdown:
                    dic = GetMarkdown(title, content, toUser);
                    break;
                case MsgType.text:
                    dic = GetText(title, content, toUser);
                    break;
                case MsgType.textcard:
                    break;
                case MsgType.interactive_taskcard:
                    break;
                default:
                    dic = GetText(title, content, toUser);
                    break;
            }
            string postData = JsonSerializer.Serialize(dic);
            string msgUrl = $"{SendUrl}?access_token={tokenResult.access_token}";

            //返回结果
            //{"errcode":0,"errmsg":"ok","invaliduser":""}
            string msgResult = HttpHelper.HttpPost(msgUrl, postData, "contentType/json");
            WxTokenResult getTokenResult = JsonSerializer.Deserialize<WxTokenResult>(msgResult);
            System.Console.WriteLine(msgResult);
            return (getTokenResult?.errcode == 0 ? 100 : 0, getTokenResult?.errmsg);
        }
        public static (int, string) SendMsg(string title, string content, string toUser)
        {
            return SendMsg(title, content, toUser, MsgType.markdown);
        }

        /// <summary>
        /// 获取访问token
        /// </summary>
        /// <returns>           
        /// {"errcode":0,"errmsg":"ok","access_token":"iCbcfE1OjfRhV0_io-CzqTNC0lnrudeW3oF5rhJKfmINaxLClLa1FoqAY_wEXtodYh_DTnrtAwZfzeb-NRXvwiOoqUTHx3i6QKLYcfBtF8y-xd5mvaeaf3e9mvTAPhmX0lkm1cLTwRLmoa1IwzgQ-QZEZcuIcntWdEMGseVYok3BwCGpC87bt6nNdgnekZdFVRp1uuaxoctDGlXpoQlQsA","expires_in":7200}
        /// </returns>
        private static WxTokenResult GetAccessToken()
        {
            string getTokenUrl = $"{GetTokenUrl}?corpid={CORPID}&corpsecret={CORPSECRET}";
            string getTokenResult = HttpHelper.HttpGet(getTokenUrl);
            System.Console.WriteLine(getTokenResult);
            WxTokenResult tokenResult = JsonSerializer.Deserialize<WxTokenResult>(getTokenResult);
            return tokenResult;
        }

        /// <summary>
        /// 发送text
        /// </summary>
        /// <param name="title"></param>
        /// <param name="content"></param>
        /// <param name="toUser"></param>
        /// <returns></returns>
        private static Dictionary<string, object> GetText(string title, string content, string toUser = "")
        {
            Dictionary<string, object> dic = new()
            {
                    { "msgtype", "text" },
                    { "touser", toUser },
                    { "agentid", AGENTID },
                    { "text", new Dictionary<string, string>
                    {
                        { "content",$"{title}\n\n{content}"
                    }
                }}
                };
            return dic;
        }

        /// <summary>
        /// 发送markdown
        /// </summary>
        /// <param name="title">要发送的标题</param>
        /// <param name="content">发送的内容</param>
        /// <param name="toUser">指定接收消息的成员，成员ID列表（多个接收者用‘|’分隔，最多支持1000个）。 特殊情况：指定为”@all”，则向该企业应用的全部成员发送</param>
        /// <returns></returns>
        private static Dictionary<string, object> GetMarkdown(string title, string content, string toUser = "")
        {
            Dictionary<string, object> dic = new()
            {
                { "touser", toUser },
                { "msgtype", "markdown" },
                { "agentid", AGENTID },
                { "enable_duplicate_check", 1 },
                {
                    "markdown",
                    new Dictionary<string, string>
                {
                    { "content", $"**{title}**\n\n{content}" }
                }
                }
            };
            return dic;
        }
    }
}
