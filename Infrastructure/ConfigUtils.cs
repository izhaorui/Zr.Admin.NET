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
            Config = App.ServiceProvider.GetRequiredService<IConfiguration>();

            if (Instance == null)
                Instance = new ConfigUtils();
        }

        public static ConfigUtils Instance { get; private set; }
        #endregion

        private static IConfiguration Config { get; set; }

        /// <summary>
        /// 泛型读取配置文件
        /// 目前还不能绑定到实体类
        /// </summary>
        /// <param name="defaultValue">获取不到配置文件设定默认值</param>
        /// <param name="key">要获取的配置文件节点名称</param>
        /// <returns></returns>
        //public T GetConfig<T>(string key, T defaultValue = default)
        //{
        //    //GetValue扩展包需要安装Microsoft.Extensions.Configuration
        //    var setting = Config.GetValue(key, defaultValue);

        //    Console.WriteLine($"获取配置文件值key={key},value={setting}");
        //    return setting;
        //}

        public T GetAppConfig<T>(string key, T defaultValue = default(T))
        {
            T setting = (T)Convert.ChangeType(Config[key], typeof(T));
            var value = setting;
            if (setting == null)
                value = defaultValue;
            //Console.WriteLine($"获取配置文件值key={key},value={value}");
            return value;
        }

        /// <summary>
        /// 获取配置文件 
        /// </summary>
        /// <param name="key">eg: WeChat:Token</param>
        /// <returns></returns>
        public string GetConfig(string key)
        {
            return Config[key];
        }

        /// <summary>
        /// 获取数据库字符串连接串
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetConnectionStrings(string key)
        {
            return Config.GetConnectionString(key);

        }
    }
}
