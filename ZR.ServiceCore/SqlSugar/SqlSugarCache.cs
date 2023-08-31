using ZR.Common;

namespace ZR.ServiceCore.SqlSugar
{
    public class SqlSugarCache : ICacheService
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
            //return RedisServer.Cache.Keys("*");
            return CacheHelper.GetCacheKeys();
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
            //RedisServer.Cache.Del(key);
            CacheHelper.Remove(key);
        }
    }
}