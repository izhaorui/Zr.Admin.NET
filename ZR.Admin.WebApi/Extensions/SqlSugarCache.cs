using ZR.Common;
using ZR.Common.Cache;

namespace ZR.Admin.WebApi.Extensions
{
    public class SqlSugarCache : SqlSugar.ICacheService
    {
        public void Add<V>(string key, V value)
        {
            //RedisServer.Cache.Set(key, value, 3600 + RedisHelper.RandomExpired(5, 30));
            CacheHelper.SetCache(key, value);
        }

        public void Add<V>(string key, V value, int cacheDurationInSeconds)
        {
            //RedisServer.Cache.Set(key, value, cacheDurationInSeconds);
            CacheHelper.SetCaches(key, value, cacheDurationInSeconds);
        }

        public bool ContainsKey<V>(string key)
        {
            //return RedisServer.Cache.Exists(key);
            return CacheHelper.Exists(key);
        }

        public V Get<V>(string key)
        {
            //return RedisServer.Cache.Get<V>(key);
            return (V)CacheHelper.Get(key);
        }

        public IEnumerable<string> GetAllKey<V>()
        {
            return RedisServer.Cache.Keys("*");
        }

        public V GetOrCreate<V>(string cacheKey, Func<V> create, int cacheDurationInSeconds = int.MaxValue)
        {
            if (RedisServer.Cache.Exists(cacheKey))
            {
                return Get<V>(cacheKey);
            }
            else
            {
                var restul = create();

                Add(cacheKey, restul);
                return restul;
            }
        }

        public void Remove<V>(string key)
        {
            RedisServer.Cache.Del(key);
        }
    }
}