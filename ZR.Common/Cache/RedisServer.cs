using CSRedis;
using Infrastructure;

namespace ZR.Common.Cache
{
    public class RedisServer
    {
        public static CSRedisClient Cache;
        public static CSRedisClient Session;

        public static void Initalize()
        {
            Cache = new CSRedisClient(ConfigUtils.Instance.GetConfig("RedisServer:Cache"));
            Session = new CSRedisClient(ConfigUtils.Instance.GetConfig("RedisServer:Session"));
        }
    }
}
