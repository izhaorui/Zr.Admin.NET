using System;

namespace Infrastructure.Attribute
{
    /// <summary>
    /// 参考地址：https://www.cnblogs.com/kelelipeng/p/10643556.html
    /// 标记服务
    /// 如何使用？
    /// 1、如果服务是本身 直接在类上使用[AppService]
    /// 2、如果服务是接口 在类上使用 [AppService(ServiceType = typeof(实现接口))]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class AppServiceAttribute : System.Attribute
    {
        /// <summary>
        /// 服务声明周期
        /// 不给默认值的话注册的是AddSingleton
        /// </summary>
        public LifeTime ServiceLifetime { get; set; } = LifeTime.Scoped;
        /// <summary>
        /// 指定服务类型
        /// </summary>
        public Type ServiceType { get; set; }
        /// <summary>
        /// 是否可以从第一个接口获取服务类型
        /// </summary>
        public bool InterfaceServiceType { get; set; }
    }

    public enum LifeTime
    {
        Transient, Scoped, Singleton
    }
}
