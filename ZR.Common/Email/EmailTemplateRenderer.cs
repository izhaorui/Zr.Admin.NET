using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace ZR.Common.Email
{
    /// <summary>
    /// 邮件模板渲染：把 emailTpl.Content / Subject 中的 {{key}} 占位符替换为业务数据。
    /// 约定：
    /// - 占位符格式 {{key}}，key 区分大小写，支持字母/数字/下划线/点/短横线；
    /// - HTML 正文渲染时对值做 HTML 编码，避免业务数据（用户昵称、流程标题等）注入标签；
    /// - 未提供的占位符原样保留，便于发现漏传参数（而不是静默变成空串）。
    /// </summary>
    public static class EmailTemplateRenderer
    {
        private static readonly Regex PlaceholderRegex = new Regex(
            @"\{\{\s*([A-Za-z0-9_\-\.]+)\s*\}\}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>渲染模板。htmlEncode=true 时对替换值做 HTML 编码（用于 HTML 正文），主题建议传 false。</summary>
        public static string Render(string template, IDictionary<string, object> data, bool htmlEncode = true)
        {
            if (string.IsNullOrEmpty(template) || data == null || data.Count == 0)
            {
                return template ?? string.Empty;
            }

            return PlaceholderRegex.Replace(template, match =>
            {
                var key = match.Groups[1].Value;
                if (!data.TryGetValue(key, out var value) || value == null)
                {
                    return match.Value;
                }

                var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                return htmlEncode ? WebUtility.HtmlEncode(text) : text;
            });
        }

        /// <summary>渲染 HTML 正文：编码后把换行转换为 &lt;br/&gt;，便于纯文本内容直接入模板。</summary>
        public static string RenderHtml(string template, IDictionary<string, object> data)
        {
            var html = Render(template, data, htmlEncode: true);
            return html.Replace("\r\n", "<br/>").Replace("\n", "<br/>");
        }
    }

    /// <summary>
    /// 内置默认模板：数据库 emailTpl 未配置对应 Code 时兜底，保证通知类邮件始终可发。
    /// 业务方仍可在后台「邮件模板」里按 Code 覆盖主题与正文。
    /// </summary>
    public static class EmailTemplateDefaults
    {
        /// <summary>工作流待办/审批通知</summary>
        public const string WorkflowNotify = "workflow.notify";
        /// <summary>安全提醒（新设备登录、双因子连续失败、恢复码使用等）</summary>
        public const string SecurityAlert = "security.alert";

        /// <summary>取内置模板；未定义返回 false。</summary>
        public static bool TryGet(string code, out string subject, out string content)
        {
            switch (code)
            {
                case WorkflowNotify:
                    subject = "【待办通知】{{subject}}";
                    content = "<p>您好：</p><p>{{content}}</p>"
                        + "<p style=\"color:#999;font-size:12px\">本邮件由系统自动发送，请勿直接回复。</p>";
                    return true;

                case SecurityAlert:
                    subject = "【安全提醒】{{subject}}";
                    content = "<p>您好：</p><p>{{content}}</p>"
                        + "<p style=\"color:#999;font-size:12px\">如非本人操作，请立即修改密码或联系管理员。</p>";
                    return true;

                default:
                    subject = null;
                    content = null;
                    return false;
            }
        }
    }
}
