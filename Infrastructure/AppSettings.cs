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
            try
            {
                if (Configuration != null && sections.Any())
                {
                    Configuration.Bind(string.Join(":", sections), list);
                }
            }
            catch
            {
                return list;
            }
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

        /// <summary>
        /// 获取配置节点并转换成指定类型
        /// </summary>
        /// <typeparam name="T">节点类型</typeparam>
        /// <param name="key">节点路径</param>
        /// <returns>节点类型实例</returns>
        public static T Get<T>(string key)
        {
            return Configuration.GetSection(key).Get<T>();
        }
    }
}
