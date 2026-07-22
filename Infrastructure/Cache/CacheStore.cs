using CSRedis;
using System;
using System.Collections.Generic;
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
    }

    /// <summary>
    /// 本地内存实现（默认，单实例）。直接复用既有 CacheHelper，不改动 CacheHelper 本身。
    /// Get 走非泛型重载再强转，以绕开 CacheHelper.GetCache&lt;T&gt; 的 class 约束，使值类型也能存取。
    /// </summary>
    public class MemoryCacheStore : ICache
    {
        public T Get<T>(string key) => (T)CacheHelper.GetCache(key);
        public void Set<T>(string key, T value, int expireMinutes) => CacheHelper.SetCache(key, value, expireMinutes);
        public bool Exists(string key) => CacheHelper.Exists(key);
        public void Remove(string key) => CacheHelper.Remove(key);
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
    }

    /// <summary>
    /// 缓存门面：根据 Redis 连接是否初始化自动切换后端，调用方无需关心底层实现。
    /// 支持 Cache / Session 两类 Redis 连接（见 CacheBackend）；未配置对应 Redis 时自动回退本地内存。
    /// 新增其他后端（如 Memcached）只需实现 ICache 并在此增加分支即可。
    /// </summary>
    public static class CacheStore
    {
        private static readonly Dictionary<CacheBackend, ICache> _instances = new();

        /// <summary>
        /// 获取指定后端的缓存实例。对应 Redis 连接为 null 时回退本地内存。
        /// </summary>
        public static ICache For(CacheBackend backend)
        {
            if (_instances.TryGetValue(backend, out var cached)) return cached;

            CSRedisClient redis = backend == CacheBackend.Session ? RedisServer.Session : RedisServer.Cache;
            ICache store = redis != null ? new RedisCacheStore(redis) : new MemoryCacheStore();
            _instances[backend] = store;
            return store;
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
