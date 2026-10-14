using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Infrastructure.AI
{
    /// <summary>
    /// AI 模块统一日志出口：集中"宿主优先、控制台兜底"的解析逻辑，供 <see cref="AiLlmClient"/>、
    /// <see cref="AiLlmClientRetryWrapper"/>、<see cref="PromptLoader"/> 等复用。
    /// <para>替代各处自行 <c>LoggerFactory.Create(b =&gt; b.AddConsole())</c> 的写法——后者在每次调用都会新建日志工厂
    /// （资源泄漏 + 额外开销），且不经宿主 NLog，日志不会落到 <c>logs/all.log</c>。</para>
    /// <para>解析规则与 <see cref="AiLlmClient"/> 原实现一致：优先取宿主 DI 的 <see cref="ILoggerFactory"/>
    /// （Web 宿主已 <c>UseNLog()</c>，日志会经 NLog 落到 logs/all.log 与控制台）；取不到时回退进程内单例的控制台工厂。
    /// 宿主工厂首次成功解析后缓存；宿主未就绪期间不缓存，后续调用会再次尝试解析，保证启动早期也能随后切到宿主。</para>
    /// </summary>
    internal static class AiLog
    {
        /// <summary>控制台兜底工厂：进程内单例，仅创建一次。</summary>
        private static readonly ILoggerFactory ConsoleFactory =
            LoggerFactory.Create(builder => builder.AddConsole());

        /// <summary>已按类别缓存的宿主工厂 Logger（仅宿主工厂就绪后填充）。</summary>
        private static readonly ConcurrentDictionary<string, ILogger> HostLoggers = new();

        /// <summary>已按类别缓存的宿主工厂泛型 Logger（仅宿主工厂就绪后填充）。</summary>
        private static readonly ConcurrentDictionary<string, ILogger> HostTypedLoggers = new();

        private static ILoggerFactory _hostFactory;

        /// <summary>按类别名取日志出口（宿主优先、控制台兜底）。</summary>
        public static ILogger GetLogger(string category)
        {
            category ??= "Infrastructure.AI";
            var host = ResolveHostFactory();
            // 兜底 logger 不缓存：宿主未就绪时每次现场取（工厂内部已缓存实例，开销可忽略），
            // 这样宿主就绪后能自动切换到宿主出口，而不会被早先缓存的兜底实例占位。
            return host != null
                ? HostLoggers.GetOrAdd(category, host.CreateLogger)
                : ConsoleFactory.CreateLogger(category);
        }

        /// <summary>按泛型类别取日志出口（宿主优先、控制台兜底），用于需要 <see cref="ILogger{T}"/> 的构造点。</summary>
        public static ILogger<T> GetLogger<T>()
        {
            var host = ResolveHostFactory();
            if (host == null)
            {
                return ConsoleFactory.CreateLogger<T>();
            }

            var key = typeof(T).FullName ?? typeof(T).Name;
            return (ILogger<T>)HostTypedLoggers.GetOrAdd(key, _ => host.CreateLogger<T>());
        }

        /// <summary>
        /// 解析宿主 <see cref="ILoggerFactory"/>：成功解析后缓存，未就绪时返回 null（不缓存，留待下次重试）。
        /// </summary>
        private static ILoggerFactory ResolveHostFactory()
        {
            var factory = _hostFactory;
            if (factory != null) return factory;

            try
            {
                if (App.ServiceProvider != null)
                {
                    factory = App.GetService<ILoggerFactory>();
                    _hostFactory = factory;
                }
            }
            catch
            {
                // 宿主未就绪（启动早期 / 非 Web 宿主工具）：回退控制台，不因日志影响调用
            }

            return factory;
        }
    }
}
