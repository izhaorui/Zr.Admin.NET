using ZR.Common.Cache;

namespace ZR.ServiceCore.SqlSugar
{
    /// <summary>
    /// 数据库Redis缓存
    /// </summary>
    public class SqlSugarRedisCache : ICacheService
    {
        public void Add<V>(string key, V value)
        {
            RedisServer.Cache.Set(key, value);
        }

        public void Add<V>(string key, V value, int cacheDurationInSeconds)
        {
            RedisServer.Cache.Set(key, value, cacheDurationInSeconds);
        }

        public bool ContainsKey<V>(string key)
        {
            return RedisServer.Cache.Exists(key);
        }

        public V Get<V>(string key)
        {
            return RedisServer.Cache.Get<V>(key);
        }

        public IEnumerable<string> GetAllKey<V>()
        {
            return RedisServer.Cache.Keys("SqlSugarDataCache.*");
        }

        public V GetOrCreate<V>(string cacheKey, Func<V> create, int cacheDurationInSeconds = int.MaxValue)
        {
            if (ContainsKey<V>(cacheKey))
            {
                var result = Get<V>(cacheKey);
                if (result == null)
                {
                    return create();
                }
                else
                {
                    return result;
                }
            }
            else
            {
                var restul = create();

                Add(cacheKey, restul, cacheDurationInSeconds);
                return restul;
            }
        }

        public void Remove<V>(string key)
        {
            RedisServer.Cache.Del(key);
        }
    }
}
