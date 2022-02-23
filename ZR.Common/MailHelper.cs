using Infrastructure;
using MailKit.Net.Smtp;
using MimeKit;
using MimeKit.Text;
using System;
using System.Collections.Generic;
using System.IO;

namespace ZR.Common
{
    public class MailHelper
    {
        /// <summary>
        /// 发送人邮箱
        /// </summary>
        public string FromEmail { get; set; } = "";
        /// <summary>
        /// 发送人密码
        /// </summary>
        public string FromPwd { get; set; } = "";
        /// <summary>
        /// 发送协议
        /// </summary>
        public string Smtp { get; set; } = "smtp.qq.com";
        /// <summary>
        /// 协议端口
        /// </summary>
        public int Port { get; set; } = 587;
        /// <summary>
        /// 是否使用SSL协议
        /// </summary>
        public bool UseSsl { get; set; } = false;
        public string mailSign = @"邮件来自C# 程序发送";
        private readonly MailOptions mailOptions = new();

        public MailHelper()
        {
            AppSettings.Bind("MailOptions", mailOptions);
            FromEmail = mailOptions.From;
            Smtp = mailOptions.Smtp;
            FromPwd = mailOptions.Password;
            Port = mailOptions.Port;
        }
        public MailHelper(string fromEmail, string smtp, int port, string fromPwd)
        {
            FromEmail = fromEmail;
            Smtp = smtp;
            FromPwd = fromPwd;
            Port = port;
        }

        public MailHelper(string fromEmail, string fromPwd)
        {
            FromEmail = fromEmail;
            FromPwd = fromPwd;
        }

        /// <summary>
        /// 发送一个人
        /// </summary>
        /// <param name="toAddress"></param>
        /// <param name="subject"></param>
        /// <param name="text"></param>
        /// <param name="path"></param>
        public void SendMail(string toAddress, string subject, string text, string path = "", string html = "")
        {
            IEnumerable<MailboxAddress> mailboxes = new List<MailboxAddress>() {
                new MailboxAddress(toAddress, toAddress)
            };

            SendMail(mailboxes, subject, text, path, html);
        }

        /// <summary>
        /// 发送多个邮箱
        /// </summary>
        /// <param name="toAddress"></param>
        /// <param name="subject"></param>
        /// <param name="text"></param>
        /// <param name="path"></param>
        public void SendMail(string[] toAddress, string subject, string text, string path = "", string html = "")
        {
            IList<MailboxAddress> mailboxes = new List<MailboxAddress>() { };
            foreach (var item in toAddress)
            {
                mailboxes.Add(new MailboxAddress(item, item));
            }

            SendMail(mailboxes, subject, text, path, html);
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="toAddress"></param>
        /// <param name="subject">主题</param>
        /// <param name="text"></param>
        /// <param name="path">附件url地址</param>
        /// <param name="html">网页HTML内容</param>
        private void SendMail(IEnumerable<MailboxAddress> toAddress, string subject, string text, string path = "", string html = "")
        {
            MimeMessage message = new MimeMessage();
            //发件人
            message.From.Add(new MailboxAddress(FromEmail, FromEmail));
            //收件人
            message.To.AddRange(toAddress);
            message.Subject = subject;
            //message.Date = DateTime.Now;

            //创建附件Multipart
            Multipart multipart = new Multipart("mixed");
            var alternative = new MultipartAlternative();
            //html内容
            if (!string.IsNullOrEmpty(html))
            {
                var Html = new TextPart(TextFormat.Html)
                {
                    Text = html
                };
                alternative.Add(Html);
            }
            //文本内容
            if (!string.IsNullOrEmpty(text))
            {
                var plain = new TextPart(TextFormat.Plain)
                {
                    Text = text + "\r\n\n\n" + mailSign
                };
                alternative.Add(plain);
            }

            //附件
            if (!string.IsNullOrEmpty(path))
            {
                string[] files = path.Split(",");
                foreach (var file in files)
                {
                    MimePart attachment = new()
                    {
                        Content = new MimeContent(File.OpenRead(file), ContentEncoding.Default),
                        //读取文件，只能用绝对路径
                        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                        ContentTransferEncoding = ContentEncoding.Base64,
                        //文件名字
                        FileName = Path.GetFileName(path)
                    };
                    alternative.Add(attachment);
                }
            }
            multipart.Add(alternative);
            //赋值邮件内容
            message.Body = multipart;

            //开始发送
            using var client = new SmtpClient();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            //Smtp服务器
            //client.Connect("smtp.qq.com", 587, false);
            client.Connect(Smtp, Port, true);
            //登录，发送
            //特别说明，对于服务器端的中文相应，Exception中有编码问题，显示乱码了
            client.Authenticate(FromEmail, FromPwd);

            client.Send(message);
            //断开
            client.Disconnect(true);
            Console.WriteLine($"发送邮件成功{DateTime.Now}");
        }
    }
}