using Infrastructure;
using Infrastructure.Attribute;
using Infrastructure.Cache;
using Newtonsoft.Json.Linq;
using NLog;
using System.Text;
using ZR.Model.AI.Dto;
using ZR.Model.System.Dto;
using ZR.ServiceCore.AI.IService;
using ZR.ServiceCore.Services;

namespace ZR.ServiceCore.AI.Charts
{
    /// <summary>
    /// 通用图表工具：列出/查询已注册数据集。模型只选 datasetId 与粒度，SQL 由各 Provider 写死。
    /// </summary>
    [AppService(ServiceType = typeof(IAiAssistantToolProvider), ServiceLifetime = LifeTime.Transient)]
    public class AiChartToolProvider : IAiAssistantToolProvider
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        /// <summary>图表聚合结果缓存分钟数：短 TTL，仅用于挡住同一轮对话内的重复查询</summary>
        private const int ChartCacheMinutes = 1;

        private readonly IReadOnlyList<IAiChartDatasetProvider> _datasets;
        private readonly ISysPermissionService _permissionService;

        public string ProviderName => "chart";

        public AiChartToolProvider(
            IEnumerable<IAiChartDatasetProvider> datasets,
            AiChartMetricExecutor metricExecutor,
            ISysPermissionService permissionService)
        {
            _permissionService = permissionService;
            _datasets = MergeDatasets(datasets, metricExecutor);
        }

        /// <summary>代码插件 + 配置目录；同一 datasetId 以插件为准并打日志。</summary>
        private IReadOnlyList<IAiChartDatasetProvider> MergeDatasets(
            IEnumerable<IAiChartDatasetProvider> datasets, AiChartMetricExecutor metricExecutor)
        {
            var list = new List<IAiChartDatasetProvider>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in datasets ?? [])
            {
                if (d == null || string.IsNullOrWhiteSpace(d.DatasetId))
                {
                    continue;
                }
                if (!seen.Add(d.DatasetId))
                {
                    _logger.Warn("重复的图表插件 datasetId={0}，已忽略后者", d.DatasetId);
                    continue;
                }
                list.Add(d);
            }
            foreach (var def in AiChartMetricCatalog.All)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.DatasetId))
                {
                    continue;
                }
                if (!seen.Add(def.DatasetId))
                {
                    _logger.Warn("配置指标 datasetId={0} 与代码插件冲突，已忽略配置项", def.DatasetId);
                    continue;
                }
                list.Add(new CatalogChartDatasetAdapter(def, metricExecutor));
            }
            return list;
        }

        public List<AiToolDef> GetToolDefs()
        {
            var catalog = string.Join("；", _datasets.Select(d =>
                $"{d.DatasetId}（{d.Title}，粒度 {string.Join("/", d.Grains)}，图 {string.Join("/", d.AllowedTypes)}"
                + (d.Dimensions is { Count: > 0 }
                    ? $"，维度 {string.Join("/", d.Dimensions.Select(x => x.Key + "(" + x.Label + ")"))}"
                    : "")
                + $"：{d.Description}）"));
            if (string.IsNullOrEmpty(catalog))
            {
                catalog = "当前未注册任何图表数据集";
            }

            return
            [
                new AiToolDef
                {
                    Name = "query_chart_dataset",
                    Label = "图表数据查询",
                    // 不设 Permission：工具本身对所有登录用户可见，具体数据集权限在 ExecuteAsync 内按 dataset 校验，
                    // 无权限时返回空清单，工具名不携带敏感语义。
                    Description = "查询后台预注册的聚合图表数据（折线/柱状/饼图）。禁止用于任意表查询或写 SQL。"
                        + "不传 datasetId 时返回当前用户有权的数据集清单。"
                        + "已注册：" + catalog
                        + "。适用于“趋势图/对比/最近N天登录量/画个折线图”等。"
                        + "数据集声明了维度（上述 catalog 含“维度”字样）时，用 dimension 参数按某维度切片，如 oper 可按 module/type/user/risk 切。"
                        + "查到数据后，最终回复须追加 ```zr-chart 配置块（只填 type/title/xField/series，不要改数据行）。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["datasetId"] = new { type = "string", description = "数据集 Id，如 login_daily；省略则只列出清单" },
                            ["dimension"] = new { type = "string", description = "统计维度 Key（仅对 catalog 中带“维度”字样的数据集有效），如 oper 可传 module/type/user/risk；不传用默认维度" },
                            ["days"] = new { type = "integer", description = "day/week 粒度的天数，默认 7，最大以数据集为准（通常 90）" },
                            ["months"] = new { type = "integer", description = "month 粒度的月数，默认 12，最大通常 24" },
                            ["grain"] = new { type = "string", description = "时间粒度 day|week|month，默认 day" },
                            ["chartType"] = new { type = "string", description = "建议图类型 line|bar|pie，须属于该数据集允许类型" }
                        },
                        required = Array.Empty<string>()
                    }
                }
            ];
        }

        public async Task<AiToolExecResult> ExecuteAsync(string toolName, string argsJson, long userId)
        {
            if (toolName != "query_chart_dataset")
            {
                return null;
            }

            var perms = AiPermissionHelper.TryLoadPerms(_permissionService, userId);
            if (perms == null)
            {
                // 权限计算失败（≠"无权限"）：提示可重试，不引导用户去申请授权
                return AiToolExecResult.Error("暂时无法校验权限，请稍后重试。");
            }

            JObject args;
            try
            {
                args = string.IsNullOrWhiteSpace(argsJson) ? new JObject() : JObject.Parse(argsJson);
            }
            catch
            {
                args = new JObject();
            }

            var datasetId = args["datasetId"]?.Value<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(datasetId))
            {
                return AiToolExecResult.Success(FormatCatalog(perms));
            }

            var provider = _datasets.FirstOrDefault(d =>
                string.Equals(d.DatasetId, datasetId, StringComparison.OrdinalIgnoreCase));
            if (provider == null)
            {
                return AiToolExecResult.Error(
                    $"未注册数据集 {datasetId}。{FormatCatalog(perms)} 请到对应模块查看，不要编写 SQL。");
            }
            if (!HasDatasetPerm(perms, provider.Permission))
            {
                // 不回显权限码，避免经模型转述把权限编码透给无权用户
                return AiToolExecResult.Error(
                    $"你没有查看「{provider.Title}」图表的权限，请联系管理员授权。");
            }

            var grain = NormalizeGrain(args["grain"]?.Value<string>(), provider);
            var (begin, end, rangeNote) = ResolveRange(args, grain, provider);
            var dimension = NormalizeDimension(args["dimension"]?.Value<string>(), provider);

            // 相同（数据集/粒度/区间/维度/用户）短时间内命中缓存，避免重复落库聚合；
            // userId 纳入 key 以尊重数据范围差异（如操作日志管理员/本人口径不同）。
            var cache = CacheStore.For(CacheBackend.Cache);
            var cacheKey = $"ai:chart:{App.GetCurrentTenantId() ?? "0"}:{provider.DatasetId}:{grain}:" +
                $"{begin:yyyyMMdd}:{end:yyyyMMdd}:{dimension ?? "-"}:{userId}";
            var result = cache.Get<AiChartQueryResult>(cacheKey);
            // 内存缓存后端返回的是同一对象引用：命中后若再改写字段会污染缓存，故标记是否仍与缓存一致
            var cacheConsistent = result != null;
            if (result == null)
            {
                result = await provider.QueryAsync(userId, begin, end, grain, dimension);
            }

            var chartType = args["chartType"]?.Value<string>()?.Trim()?.ToLowerInvariant();
            if (result == null)
            {
                return AiToolExecResult.Success(
                    $"数据集 {provider.DatasetId} 在 {begin:yyyy-MM-dd} 至 {end:yyyy-MM-dd}（{grain}）没有数据。{rangeNote}".Trim());
            }
            if (!string.IsNullOrEmpty(chartType) && provider.AllowedTypes.Contains(chartType))
            {
                result.SuggestedType = chartType;
                cacheConsistent = false;
            }

            if (result.Rows == null || result.Rows.Count == 0)
            {
                if (!cacheConsistent)
                {
                    cache.Set(cacheKey, result, ChartCacheMinutes);
                }
                return AiToolExecResult.Success(
                    $"数据集 {provider.DatasetId} 在 {result.TimeRange}（{grain}）没有数据。{rangeNote}".Trim());
            }

            if (!cacheConsistent)
            {
                cache.Set(cacheKey, result, ChartCacheMinutes);
            }

            var content = FormatQueryForModel(result, rangeNote);
            return new AiToolExecResult { Ok = true, Content = content, ChartQuery = result };
        }

        private string FormatCatalog(List<string> perms)
        {
            var allowed = _datasets.Where(d => HasDatasetPerm(perms, d.Permission)).ToList();
            if (allowed.Count == 0)
            {
                return "当前没有你有权限使用的图表数据集，请到对应模块查看或联系管理员授权。";
            }
            var sb = new StringBuilder("你可查询的图表数据集：\n");
            foreach (var d in allowed)
            {
                var dims = d.Dimensions is { Count: > 0 }
                    ? $"，维度 {string.Join("/", d.Dimensions.Select(x => x.Key + "(" + x.Label + ")"))}"
                    : "";
                sb.AppendLine(
                    $"- {d.DatasetId}：{d.Title}。{d.Description} 粒度 {string.Join("/", d.Grains)}，图 {string.Join("/", d.AllowedTypes)}，字段 {string.Join(",", d.Fields.Select(f => f.Field + "(" + f.Label + ")"))}{dims}");
            }
            sb.Append("选一个 datasetId 再次调用本工具查询数据。未列出的业务（如充值）表示尚未注册，请到对应模块查看，不要写 SQL。");
            return sb.ToString();
        }

        private static string FormatQueryForModel(AiChartQueryResult result, string rangeNote)
        {
            var fields = string.Join("，", result.Fields.Select(f => $"{f.Field}({f.Label}/{f.Kind})"));
            var types = string.Join("|", result.AllowedTypes ?? []);
            var json = JsonConvert.SerializeObject(result.Rows);
            var sb = new StringBuilder();
            sb.AppendLine($"图表数据已由后端聚合（datasetId={result.DatasetId}，{result.TimeRange}，grain={result.Grain}，{result.Rows.Count} 行）。{rangeNote}".Trim());
            sb.AppendLine($"标题建议：{result.Title}；建议图类型：{result.SuggestedType}；允许：{types}");
            sb.AppendLine($"字段：{fields}");
            sb.AppendLine("最终回复请用自然语言解读（不要编造数字），并追加唯一代码块：");
            sb.AppendLine("```zr-chart");
            var xField = result.Fields.FirstOrDefault(f => f.Kind == "category")?.Field ?? "date";
            var seriesField = result.Fields.FirstOrDefault(f => f.Kind == "number");
            var seriesName = seriesField?.Label ?? "数值";
            var seriesKey = seriesField?.Field ?? "value";
            sb.AppendLine($"{{\"datasetId\":\"{result.DatasetId}\",\"type\":\"{result.SuggestedType}\",\"title\":\"{result.Title}\",\"xField\":\"{xField}\",\"series\":[{{\"name\":\"{seriesName}\",\"field\":\"{seriesKey}\"}}]}}");
            sb.AppendLine("```");
            sb.AppendLine("series.field / xField 必须是上面的字段名；不要输出完整 ECharts option，不要修改数据行。");
            sb.AppendLine("数据行：");
            sb.Append(json);
            return sb.ToString();
        }

        private static string NormalizeGrain(string grain, IAiChartDatasetProvider provider)
        {
            grain = (grain ?? "day").Trim().ToLowerInvariant();
            if (!provider.Grains.Contains(grain))
            {
                return provider.Grains.FirstOrDefault() ?? "day";
            }
            return grain;
        }

        private static string NormalizeDimension(string dimension, IAiChartDatasetProvider provider)
        {
            if (string.IsNullOrWhiteSpace(dimension) || provider.Dimensions is not { Count: > 0 })
            {
                return null;
            }
            var value = dimension.Trim();
            // 大小写不敏感的 Key 命中则规范回原 Key；其余（含中文别名）透传给数据集归一化
            var hit = provider.Dimensions.FirstOrDefault(x =>
                string.Equals(x.Key, value, StringComparison.OrdinalIgnoreCase));
            return hit?.Key ?? value;
        }

        private static (DateTime begin, DateTime end, string note) ResolveRange(JObject args, string grain, IAiChartDatasetProvider provider)
        {
            var end = DateTime.Today;
            string note = "";
            DateTime begin;
            if (grain == "month")
            {
                var months = 12;
                if (args["months"] != null)
                {
                    months = args["months"].Value<int>();
                }
                else if (args["days"] != null)
                {
                    months = Math.Max(1, (args["days"].Value<int>() + 29) / 30);
                }
                var max = Math.Max(1, provider.MaxMonths);
                if (months < 1) months = 1;
                if (months > max)
                {
                    note = $"月跨度已截断为 {max} 个月。";
                    months = max;
                }
                begin = end.AddMonths(1 - months);
            }
            else
            {
                var days = args["days"]?.Value<int>() ?? 7;
                var max = Math.Max(1, provider.MaxDays);
                if (days < 1) days = 1;
                if (days > max)
                {
                    note = $"天数已截断为 {max} 天。";
                    days = max;
                }
                begin = end.AddDays(1 - days);
            }
            return (begin, end, note);
        }

        private static bool HasDatasetPerm(List<string> perms, string permission)
        {
            // 数据集未声明权限（或声明为 common）表示登录即可查看；其余走统一的管理员/精确匹配口径
            if (string.IsNullOrWhiteSpace(permission) || permission == "common")
            {
                return true;
            }
            return AiPermissionHelper.HasPerm(perms, permission);
        }
    }
}
