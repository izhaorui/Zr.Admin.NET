using System.Collections.Concurrent;
using System.Text.Json;
using Infrastructure;
using Infrastructure.AI;
using Infrastructure.Attribute;
using ZR.Model.AI;
// ServiceCore 全局引入了 Newtonsoft.Json，这里显式指向 System.Text.Json
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ZR.ServiceCore.AI.Governance
{
    /// <summary>
    /// AI 渠道读取实现：按环境取启用渠道，解密密钥、解析候选模型，并按优先级排序。
    /// <para>
    /// 同一 Provider 可能有多行（生产 Key / 备用 Key）：<b>全部保留</b>并按
    /// Priority 降序 → Sort 升序 → Weight 降序 → Id 升序 排好，
    /// 调用方按列表顺序取用，首条即当前生效渠道，其后的同厂商渠道用于失败换 Key。
    /// </para>
    /// <para>
    /// 60 秒进程内缓存：AI 每次请求都会取渠道，裸查库会成为热点；保存后由 <see cref="Invalidate"/> 立即失效。
    /// 表未建或库不可用时返回空集合，调用方回退文件里的 Providers。
    /// </para>
    /// </summary>
    [AppService(ServiceType = typeof(IAiProviderStore), ServiceLifetime = LifeTime.Transient)]
    public class AiProviderStore : BaseService<AiProvider>, IAiProviderStore
    {
        private static readonly ConcurrentDictionary<string, (IReadOnlyList<AiProviderSnapshot> Items, DateTime ExpireAt)> Cache = new();
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public IReadOnlyList<AiProviderSnapshot> GetEnabledProviders()
        {
            var environment = ResolveEnvironment();
            if (Cache.TryGetValue(environment, out var cached) && cached.ExpireAt > DateTime.Now)
            {
                return cached.Items;
            }

            IReadOnlyList<AiProviderSnapshot> items;
            try
            {
                // 环境过滤下推到 SQL：只取「当前环境 + * 通配」的启用行，避免把所有环境的渠道都拉进内存
                var rows = Context.Queryable<AiProvider>()
                    .Where(x => x.Status == 0 && (x.Environment == environment || x.Environment == "*"))
                    .ToList();

                items = rows
                    .OrderByDescending(x => x.Priority)
                    .ThenBy(x => x.Sort)
                    .ThenByDescending(x => x.Weight)
                    .ThenBy(x => x.Id)
                    .Select(ToSnapshot)
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Provider))
                    // 不按 Provider 去重：同一厂商的多行（生产 Key / 备用 Key）全部保留，
                    // 顺序即候选顺序，首条为当前生效渠道，其余供失败换 Key 使用。
                    .ToList();
            }
            catch (Exception ex)
            {
                // 表未建/库抖动：不写缓存，下次请求重试；调用方回退文件配置
                Logger.Warn(ex, $"读取 AI 渠道失败，本次回退文件 Providers env={environment}");
                return Array.Empty<AiProviderSnapshot>();
            }

            Cache[environment] = (items, DateTime.Now.Add(CacheTtl));
            return items;
        }

        /// <summary>行 → 运行时投影；单行解密失败只跳过该行，不影响其它渠道。</summary>
        private static AiProviderSnapshot ToSnapshot(AiProvider row)
        {
            List<string> models;
            try
            {
                models = string.IsNullOrWhiteSpace(row.ModelsJson)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(row.ModelsJson) ?? new List<string>();
            }
            catch (JsonException)
            {
                models = new List<string>();
            }

            string apiKey = null;
            if (!string.IsNullOrWhiteSpace(row.ApiKeyCipher))
            {
                try
                {
                    apiKey = AiConfigProtector.Unprotect(row.ApiKeyCipher);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, $"渠道密钥解密失败，该渠道本次不参与路由 provider={row.Provider} name={row.Name}");
                    return null;
                }
            }

            return new AiProviderSnapshot
            {
                Id = row.Id,
                Provider = row.Provider,
                Name = row.Name,
                BaseUrl = row.BaseUrl,
                ChatEndpoint = row.ChatEndpoint,
                ApiKey = apiKey,
                Model = row.Model,
                VisionModel = row.VisionModel,
                Models = models,
                TimeoutSeconds = row.TimeoutSeconds,
                Priority = row.Priority,
                Weight = row.Weight
            };
        }

        /// <summary>当前进程环境名（未初始化宿主时按 Production，与 ASP.NET Core 默认一致）。</summary>
        public static string ResolveEnvironment()
            => InternalApp.WebHostEnvironment?.EnvironmentName ?? "Production";

        /// <summary>清空本进程缓存（保存/删除/启停渠道后调用）。</summary>
        public static void Invalidate() => Cache.Clear();
    }
}
