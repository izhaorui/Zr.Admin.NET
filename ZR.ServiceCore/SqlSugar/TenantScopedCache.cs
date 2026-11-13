using Infrastructure;

namespace ZR.ServiceCore.SqlSugar
{
    /// <summary>
    /// SqlSugar 查询缓存装饰器 —— 给缓存 key 追加「库 ConfigId + 当前租户」作用域。
    /// </summary>
    /// <remarks>
    /// SqlSugar 的 <c>WithCache</c> 缓存 key 只由查询 SQL 生成，不带连接/租户标识：多租户下不同租户库、
    /// 主库共享表按 TenantId 过滤的查询，SQL 文本完全相同，key 会撞在一起导致 A 租户读到 B 租户的数据。
    /// 由 <c>SqlsugarSetup</c> 为每个连接各包一层，key 统一带「库:租户」后缀区分。
    ///
    /// 后缀拼在末尾而不是开头：SqlSugar 原生 key 的 <c>SqlSugarDataCache</c> 前缀保持不变，
    /// <c>IsAutoRemoveDataCache</c> 按表名 <c>Contains</c> 清理、Redis 的 <c>KEYS SqlSugarDataCache.*</c> 匹配都不受影响。
    ///
    /// 注意：本装饰器只解决库/租户维度。被数据权限（角色/部门）过滤的查询，同租户内不同角色仍会共用 key。
    /// </remarks>
    public class TenantScopedCache : ICacheService
    {
        private readonly ICacheService _inner;
        private readonly string _configId;

        /// <param name="configId">连接配置 Id（本项目多租户下租户库的 ConfigId 即租户 Id）</param>
        /// <param name="inner">实际缓存实现（内存 / Redis）</param>
        public TenantScopedCache(string configId, ICacheService inner)
        {
            _configId = configId ?? string.Empty;
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public void Add<V>(string key, V value)
        {
            _inner.Add(Scope(key), value);
        }

        public void Add<V>(string key, V value, int cacheDurationInSeconds)
        {
            _inner.Add(Scope(key), value, cacheDurationInSeconds);
        }

        public bool ContainsKey<V>(string key)
        {
            return _inner.ContainsKey<V>(Scope(key));
        }

        public V Get<V>(string key)
        {
            return _inner.Get<V>(Scope(key));
        }

        /// <summary>
        /// 原样返回底层全部 key（含其它租户/其它连接的作用域），
        /// 使 SqlSugar 按表名清理缓存时能跨租户一起失效，不会留下别个租户的过期数据。
        /// </summary>
        public IEnumerable<string> GetAllKey<V>()
        {
            return _inner.GetAllKey<V>();
        }

        public V GetOrCreate<V>(string cacheKey, Func<V> create, int cacheDurationInSeconds = int.MaxValue)
        {
            return _inner.GetOrCreate(Scope(cacheKey), create, cacheDurationInSeconds);
        }

        public void Remove<V>(string key)
        {
            _inner.Remove<V>(Scope(key));
        }

        /// <summary>
        /// 追加「库:租户」后缀，与 <c>CacheService.BuildTenantKey</c> 同一口径取租户
        /// （非多租户时 App.GetCurrentTenantId() 回落系统库 ConfigId）；已带后缀的原样返回。
        /// </summary>
        private string Scope(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;

            var suffix = $":{_configId}:{App.GetCurrentTenantId()}";
            return key.EndsWith(suffix, StringComparison.Ordinal) ? key : key + suffix;
        }
    }
}