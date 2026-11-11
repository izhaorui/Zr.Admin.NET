using System.Collections.Concurrent;
using Infrastructure;
using Infrastructure.AI;
using Infrastructure.Attribute;
using ZR.Model.AI;

namespace ZR.ServiceCore.AI.Governance
{
    /// <summary>
    /// AI 配置的数据库读取实现：按“环境精确匹配 → * 通配”取一行，解密后返回；两者都没有则不取任何行。
    /// <para>
    /// 生效行规则（<see cref="PickEffectiveRow"/>）与候选条件（<see cref="QueryEnabledRows"/>，只认
    /// Status=0）由管理端 AiConfigService 复用，避免“后台看到的行”与“线上真正读的行”不是同一行。
    /// 规则里没有“随便取一行”的兜底，共享库不会跨环境串读/串写。
    /// </para>
    /// <para>
    /// 带 60 秒进程内缓存：AI 每次请求都会取配置，裸查库会成为热点。语义明确如下：
    /// <list type="bullet">
    /// <item>缓存按“环境|段名”存放，<b>查询成功但无记录也会缓存 null</b>（避免每次请求都查空）；</item>
    /// <item><see cref="Invalidate"/> 只清<b>本实例</b>，多实例部署下其它节点最长 60 秒仍读旧值——</item>
    /// <item>该 60 秒收敛延迟是按“可接受”设计的；若业务要求改完立即全局生效，请改为单实例写入
    /// 或引入 Redis 发布失效消息（当前未实现），否则不要把 TTL 调大。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 读取失败（表未建/库抖动/密文无法解密）不写缓存、不向调用链抛异常，而是通过
    /// <see cref="TryGetSectionJson"/> 的返回值告知调用方，由配置源策略决定回退文件还是失败关闭。
    /// </para>
    /// </summary>
    [AppService(ServiceType = typeof(IAiConfigStore), ServiceLifetime = LifeTime.Transient)]
    public class AiConfigStore : BaseService<AiConfig>, IAiConfigStore
    {
        /// <summary>读取缓存：key = 环境|段名。刻意保留短 TTL（见类注释），避免引入 Redis 失效广播的复杂度。</summary>
        private static readonly ConcurrentDictionary<string, (string Json, DateTime ExpireAt)> Cache = new();

        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public string GetSectionJson(string section)
            => TryGetSectionJson(section, out var json, out _) ? json : null;

        /// <summary>
        /// 读取段配置。<paramref name="error"/> 非空表示“读取过程失败”（表未建/库抖动/密文损坏），
        /// 与“读取成功但没有该段配置”（返回 true 且 json 为 null）区别开，
        /// 供 <c>AiOptionsResolver</c> 在配置源=db 时做失败关闭。
        /// </summary>
        public bool TryGetSectionJson(string section, out string json, out string error)
        {
            json = null;
            error = null;
            if (string.IsNullOrWhiteSpace(section)) return true;

            var environment = ResolveEnvironment();
            var cacheKey = $"{environment}|{section}";
            if (Cache.TryGetValue(cacheKey, out var cached) && cached.ExpireAt > DateTime.Now)
            {
                json = cached.Json;
                return true;
            }

            try
            {
                var row = PickEffectiveRow(QueryEnabledRows(Context, section), environment);
                json = row == null ? null : AiConfigProtector.Unprotect(row.ConfigJson);
            }
            catch (Exception ex)
            {
                // 表未建/库抖动：不写缓存（下次请求重试），何时回退文件交给调用方按配置源决定
                error = ex.Message;
                Logger.Warn(ex, $"读取库中 AI 配置失败，本次不缓存 section={section} env={environment}");
                return false;
            }

            Cache[cacheKey] = (json, DateTime.Now.Add(CacheTtl));
            return true;
        }

        /// <summary>
        /// 候选行：只取启用行（Status=0）。运行时与管理端必须用同一个条件，
        /// 否则某行被停用后会出现“后台展示/编辑的是 A 行，线上生效的是 B 行”。
        /// </summary>
        internal static List<AiConfig> QueryEnabledRows(ISqlSugarClient db, string section)
            => db.Queryable<AiConfig>().Where(x => x.Section == section && x.Status == 0).ToList();

        /// <summary>
        /// 生效行规则：环境精确匹配（忽略大小写）→ * 通用；都没有则返回 null（视为“当前环境无配置”）。
        /// 运行时读取与管理端回显/保存/回退共用这一条规则。
        /// <para>
        /// 刻意<b>不</b>再退回“任意一行”：共享库里往往同时存在多个环境的专属配置，
        /// 一旦 Production 没有自己的行、也没有 * 行就抓到 Development 那行，
        /// 会把低环境的地址/密钥带上线；管理端保存或回退时同样会改到/删掉别的环境的记录。
        /// 无匹配一律返回 null，由调用方按配置源策略回退文件或失败关闭。
        /// </para>
        /// </summary>
        internal static AiConfig PickEffectiveRow(IEnumerable<AiConfig> rows, string environment)
        {
            if (rows == null) return null;

            var list = rows as IList<AiConfig> ?? rows.ToList();
            return list.FirstOrDefault(x => string.Equals(x.Environment, environment, StringComparison.OrdinalIgnoreCase))
                   ?? list.FirstOrDefault(x => x.Environment == "*");
        }

        /// <summary>当前进程环境名（未初始化宿主时按 Production，与 ASP.NET Core 默认一致）。</summary>
        public static string ResolveEnvironment()
            => InternalApp.WebHostEnvironment?.EnvironmentName ?? "Production";

        /// <summary>清空本进程缓存（保存/回退配置后调用）。</summary>
        public static void Invalidate() => Cache.Clear();
    }
}
