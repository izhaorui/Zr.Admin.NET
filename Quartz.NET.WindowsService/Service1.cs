using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Quartz.NET.WindowsService
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Task.Run(() =>
            {
                Start();
            });
        }
        private void Start()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\log\\";
            //默认1分钟调用一次
            int interval = 1;
            string message = "";
            string url = "";
            while (true)
            {
                try
                {
                    ConfigurationManager.RefreshSection("appSettings");
                    url = ConfigurationManager.AppSettings["url"];
                    interval = Convert.ToInt32(ConfigurationManager.AppSettings["interval"]);

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        Stream responseStream = response.GetResponseStream();
                        StreamReader streamReader = new StreamReader(responseStream, Encoding.UTF8);
                        message = streamReader.ReadToEnd();
                    }
                }
                catch (Exception ex)
                {
                    message= url+ ",interval:["+ interval + "],"+ ex.Message;
                }
                finally
                {
                    try
                    {
                        message = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "__" + message + "\r\n";
                        WriteFile(path, DateTime.Now.ToString("yyyy-MM-dd") + ".txt", message, true);
                    }
                    catch (Exception ex)
                    {
                    }
                    Thread.Sleep(new TimeSpan(0,interval,0));
                }
            }
        }

        private static void WriteFile(string path, string fileName, string content, bool appendToLast = false)
        {
            if (!Directory.Exists(path))//如果不存在就创建file文件夹
            {
                Directory.CreateDirectory(path);
            }

            using (FileStream stream = File.Open(path + fileName, FileMode.OpenOrCreate, FileAccess.Write))
            {
                byte[] by = Encoding.Default.GetBytes(content);
                if (appendToLast)
                {
                    stream.Position = stream.Length;
                }
                else
                {
                    stream.SetLength(0);
                }
                stream.Write(by, 0, by.Length);
            }
        }

        protected override void OnStop()
        {
        }
    }
}
