using CSRedis;
using System;
using System.Collections.Concurrent;
using ZR.Common;       // 复用 CacheHelper 作为本地内存实现

namespace Infrastructure.Cache
{
    /// <summary>
    /// Redis 连接分类，对应 appsettings 中 RedisServer:Cache / RedisServer:Session 两个连接串。
    /// </summary>
    public enum CacheBackend
    {
        /// <summary>
        /// 业务缓存库（默认）。对应 RedisServer:Cache
        /// </summary>
        Cache,
        /// <summary>
        /// 会话库。对应 RedisServer:Session，用于单设备登录等会话态数据
        /// </summary>
        Session
    }

    /// <summary>
    /// 可插拔缓存抽象（供“新功能”使用，老的 CacheHelper 保持不变）。
    /// 后端自动选择：对应 Redis 连接已初始化（RedisServer:open=1）时使用 Redis（跨节点共享），
    /// 否则回退本地内存（CacheHelper）。
    /// </summary>
    public interface ICache
    {
        T Get<T>(string key);
        void Set<T>(string key, T value, int expireMinutes);
        bool Exists(string key);
        void Remove(string key);

        /// <summary>
        /// 原子自增并返回自增后的值（键不存在时按 0 起算），首次自增时设置过期时间。
        /// Redis 后端为跨节点原子操作；内存后端以进程内锁保证串行（单实例语义）。
        /// 供限流计数等需要跨实例原子性的场景使用。
        /// </summary>
        long Increment(string key, int expireMinutes);
    }

    /// <summary>
    /// 本地内存实现（默认，单实例）。直接复用既有 CacheHelper，不改动 CacheHelper 本身。
    /// Get 走非泛型重载再强转，以绕开 CacheHelper.GetCache&lt;T&gt; 的 class 约束，使值类型也能存取。
    /// </summary>
    public class MemoryCacheStore : ICache
    {
        // 内存后端无原生原子自增，以进程内锁保证「读-改-写」串行（单实例语义）
        private static readonly object IncrementLock = new();

        public T Get<T>(string key) => (T)CacheHelper.GetCache(key);
        public void Set<T>(string key, T value, int expireMinutes) => CacheHelper.SetCache(key, value, expireMinutes);
        public bool Exists(string key) => CacheHelper.Exists(key);
        public void Remove(string key) => CacheHelper.Remove(key);

        public long Increment(string key, int expireMinutes)
        {
            lock (IncrementLock)
            {
                var current = CacheHelper.GetCache(key);
                var next = (current is long value ? value : 0L) + 1;
                CacheHelper.SetCache(key, next, expireMinutes);
                return next;
            }
        }
    }

    /// <summary>
    /// Redis 实现（多实例共享，跨节点生效）。基于 CSRedisClient，默认 JSON 序列化。
    /// </summary>
    public class RedisCacheStore : ICache
    {
        private readonly CSRedisClient _redis;
        public RedisCacheStore(CSRedisClient redis) => _redis = redis;

        public T Get<T>(string key) => _redis.Get<T>(key);
        public void Set<T>(string key, T value, int expireMinutes)
            => _redis.Set(key, value, (int)TimeSpan.FromMinutes(expireMinutes).TotalSeconds);
        public bool Exists(string key) => _redis.Exists(key);
        public void Remove(string key) => _redis.Del(key);

        public long Increment(string key, int expireMinutes)
        {
            var value = _redis.IncrBy(key, 1);
            if (value == 1)
            {
                // 仅在首次自增时设过期，避免每次续期导致键永不过期
                _redis.Expire(key, (int)TimeSpan.FromMinutes(expireMinutes).TotalSeconds);
            }
            return value;
        }
    }

    /// <summary>
    /// 缓存门面：根据 Redis 连接是否初始化自动切换后端，调用方无需关心底层实现。
    /// 支持 Cache / Session 两类 Redis 连接（见 CacheBackend）；未配置对应 Redis 时自动回退本地内存。
    /// 新增其他后端（如 Memcached）只需实现 ICache 并在此增加分支即可。
    /// </summary>
    public static class CacheStore
    {
        // 必须线程安全：For() 会在每个请求（GetUserSession/SetUserSession/RemoveUserSession）中被并发调用，
        // 普通 Dictionary 并发读+写会抛 "Operations that change non-concurrent collections must have exclusive access"。
        private static readonly ConcurrentDictionary<CacheBackend, ICache> _instances = new();

        /// <summary>
        /// 获取指定后端的缓存实例。对应 Redis 连接为 null 时回退本地内存。
        /// </summary>
        public static ICache For(CacheBackend backend)
        {
            return _instances.GetOrAdd(backend, static b =>
            {
                CSRedisClient redis = b == CacheBackend.Session ? RedisServer.Session : RedisServer.Cache;
                return redis != null ? new RedisCacheStore(redis) : (ICache)new MemoryCacheStore();
            });
        }

        /// <summary>
        /// 默认业务缓存后端（RedisServer:Cache），等价于 For(CacheBackend.Cache)
        /// </summary>
        public static ICache Default => For(CacheBackend.Cache);

        // 便捷泛型方法（默认走 Cache 库）
        public static T Get<T>(string key) => Default.Get<T>(key);
        public static void Set<T>(string key, T value, int expireMinutes) => Default.Set<T>(key, value, expireMinutes);
        public static bool Exists(string key) => Default.Exists(key);
        public static void Remove(string key) => Default.Remove(key);
    }
}
