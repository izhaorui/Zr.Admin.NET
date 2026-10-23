using Come.CollectiveOAuth.Cache;
using Infrastructure.Cache;

namespace ZR.ServiceCore.OAuth
{
    /// <summary>
    /// state 缓存：把授权库的 IAuthStateCache 套到项目现有 ICache 上。
    ///
    /// 为什么必须换掉默认实现：上游 DefaultAuthStateCache 用 IMemoryCache，state 只存在单个进程里。
    /// 项目一旦多节点部署（RedisServer:Cache 已启用），授权页落在 A 节点、回调落在 B 节点，
    /// 必然报「state 非法」，现象是“偶尔登录失败”，极难排查。
    ///
    /// 一次性语义：上游 login() 的流程是 containsKey → remove，两步之间不是原子的，
    /// 并发两个回调可以同时通过。这里额外用 ICache.TrySet（Redis = SET NX）抢占一个 used 标记，
    /// 只有抢到的请求能继续，其余直接拒绝。
    /// </summary>
    public sealed class OAuthStateCache : IAuthStateCache
    {
        private const string StatePrefix = "oauth:state:";
        private const string UsedPrefix = "oauth:state:used:";

        /// <summary>
        /// 上下文单独一个前缀：上游 authorize() 会用 state 值覆盖 "oauth:state:{state}"，
        /// 若把租户塞进同一个键，回调时读到的是 state 本身而不是租户。
        /// </summary>
        private const string ContextPrefix = "oauth:state:ctx:";

        private static readonly OAuthStateCache Instance = new();

        private readonly ICache _cache = CacheStore.Default;

        public OAuthStateCache(int ttlMinutes = 10)
        {
            TtlMinutes = ttlMinutes > 0 ? ttlMinutes : 10;
        }

        /// <summary>进程内单例：authorize 与 login 必须共用同一个缓存实例</summary>
        public static OAuthStateCache Default => Instance;

        public int TtlMinutes { get; }

        /// <summary>
        /// 生成并登记一个 state。App 端没有授权页跳转，由服务端预发 state 后再带着 code 回调。
        /// </summary>
        public string Issue()
        {
            string state = Guid.NewGuid().ToString("N");
            cache(state, "1");
            return state;
        }

        /// <summary>
        /// 生成 state 并绑定上下文（租户、设备标识）。
        ///
        /// 租户为什么走 state 而不走 URL 参数：URL 是用户可见、可改的，
        /// 若回调时从 query 读 tenantId，改一下就能拿别家租户的账号进系统。
        /// </summary>
        public string IssueWithContext(OAuthStateContext context)
        {
            string state = Guid.NewGuid().ToString("N");
            cache(state, "1");
            SetContext(state, context);
            return state;
        }

        /// <summary>绑定上下文（Web 授权时 state 由业务层预先生成，需单独登记）</summary>
        public void SetContext(string state, OAuthStateContext context)
        {
            if (string.IsNullOrWhiteSpace(state) || context == null)
            {
                return;
            }

            _cache.Set(ContextPrefix + state, JsonConvert.SerializeObject(context), TtlMinutes);
        }

        /// <summary>读取上下文。state 被消费后仍可读，用于记录"是哪个租户的哪次授权"</summary>
        public OAuthStateContext GetContext(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
            {
                return null;
            }

            string json = _cache.Get<string>(ContextPrefix + state);
            return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<OAuthStateContext>(json);
        }

        public void cache(string key, string value)
        {
            cache(key, value, TtlMinutes * 60_000L);
        }

        public void cache(string key, string value, long timeout)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            // 上游约定的 timeout 单位是毫秒，ICache 用的是分钟，向上取整避免秒级失效
            int minutes = timeout > 0 ? Math.Max(1, (int)Math.Ceiling(timeout / 60_000d)) : TtlMinutes;
            _cache.Set(StatePrefix + key, value, minutes);
        }

        public string get(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? null : _cache.Get<string>(StatePrefix + key);
        }

        public bool containsKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && _cache.Exists(StatePrefix + key);
        }

        public void remove(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                _cache.Remove(StatePrefix + key);
            }
        }

        /// <summary>
        /// 原子占用 state。返回 true 表示本次请求抢到，可以继续换取 token。
        /// 注意：只写 used 标记、不删原始 state，删的动作交给上游 login() 的 remove，
        /// 否则上游的 checkState 会先失败。
        /// </summary>
        public bool TryConsume(string state)
        {
            if (string.IsNullOrWhiteSpace(state) || !containsKey(state))
            {
                return false;
            }

            return _cache.TrySet(UsedPrefix + state, "1", TtlMinutes);
        }
    }

    /// <summary>
    /// 授权场景。必须区分：绑定场景的 state 若能被拿去走登录接口，
    /// 攻击者可以诱导已登录用户完成授权后把 code+state 交给自己的会话使用（授权码注入）。
    /// </summary>
    public static class OAuthScene
    {
        /// <summary>登录：未登录状态发起，成功后签发 token</summary>
        public const string Login = "login";

        /// <summary>绑定：已登录用户发起，只建立绑定关系，不签发 token</summary>
        public const string Bind = "bind";
    }

    /// <summary>
    /// 一次授权流程的上下文：发起时写入、回调时读回。
    /// 这些值都不能由回调请求体携带，否则等于把租户/设备交给调用方随意指定。
    /// </summary>
    public sealed class OAuthStateContext
    {
        /// <summary>渠道标识（大写）</summary>
        public string AuthSource { get; set; }

        /// <summary>租户：决定读哪个库、用哪套应用配置</summary>
        public string TenantId { get; set; }

        /// <summary>设备标识（前端指纹），用于 2FA 可信设备判定</summary>
        public string ClientId { get; set; }

        /// <summary>发起时的场景："login" 登录、"bind" 绑定</summary>
        public string Scene { get; set; }

        /// <summary>绑定场景下的用户ID（登录场景为空）</summary>
        public long BindUserId { get; set; }
    }
}