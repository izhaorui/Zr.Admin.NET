using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

/// <summary>
/// 需要引用包
/// Microsoft.Extensions.Configuration;
/// Microsoft.Extensions.DependencyInjection;
namespace Infrastructure
{
    public class ConfigUtils
    {
        #region 单例访问

        static ConfigUtils()
        {
            Configuration = App.ServiceProvider.GetRequiredService<IConfiguration>();

            if (Instance == null)
                Instance = new ConfigUtils();
        }

        public static ConfigUtils Instance { get; private set; }
        #endregion
        private static IConfiguration Configuration { get; set; }

        public T GetAppConfig<T>(string key, T defaultValue = default(T))
        {
            T setting = (T)Convert.ChangeType(Configuration[key], typeof(T));
            var value = setting;
            if (setting == null)
                value = defaultValue;
            return value;
        }
        public T Bind<T>(string key, T t)
        {
            Configuration.Bind(key, t);
            
            return t;
        }
        /// <summary>
        /// 获取配置文件 
        /// </summary>
        /// <param name="key">eg: WeChat:Token</param>
        /// <returns></returns>
        public string GetConfig(string key)
        {
            return Configuration[key];
        }

        /// <summary>
        /// 获取数据库字符串连接串
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetConnectionString(string key)
        {
            return Configuration.GetConnectionString(key);
        }
    }
}
