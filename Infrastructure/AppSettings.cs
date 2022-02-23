using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure
{
    public class AppSettings
    {
        static IConfiguration Configuration { get; set; }

        public AppSettings(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// 封装要操作的字符
        /// </summary>
        /// <param name="sections">节点配置</param>
        /// <returns></returns>
        public static string App(params string[] sections)
        {
            try
            {
                if (sections.Any())
                {
                    return Configuration[string.Join(":", sections)];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return "";
        }

        /// <summary>
        /// 递归获取配置信息数组
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sections"></param>
        /// <returns></returns>
        public static List<T> App<T>(params string[] sections)
        {
            List<T> list = new();
            // 引用 Microsoft.Extensions.Configuration.Binder 包
            Configuration.Bind(string.Join(":", sections), list);
            return list;
        }
        public static T Bind<T>(string key, T t)
        {
            Configuration.Bind(key, t);

            return t;
        }

        public static T GetAppConfig<T>(string key, T defaultValue = default)
        {
            T setting = (T)Convert.ChangeType(Configuration[key], typeof(T));
            var value = setting;
            if (setting == null)
                value = defaultValue;
            return value;
        }

        /// <summary>
        /// 获取配置文件 
        /// </summary>
        /// <param name="key">eg: WeChat:Token</param>
        /// <returns></returns>
        public static string GetConfig(string key)
        {
            return Configuration[key];
        }
    }
}
