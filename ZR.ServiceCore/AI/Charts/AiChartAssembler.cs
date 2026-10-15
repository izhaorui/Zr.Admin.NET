using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ZR.Model.AI.Dto;

namespace ZR.ServiceCore.AI.Charts
{
    /// <summary>
    /// 从模型回复提取 zr-chart 配置、校验字段白名单，并与后端行数据组装前端 Charts。
    /// </summary>
    public static class AiChartAssembler
    {
        private static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase) { "line", "bar", "pie" };
        private static readonly Regex Fence = new(@"```zr-chart\s*([\s\S]*?)```", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static (string Reply, List<AiChartViewDto> Charts) Assemble(string reply, IList<AiChartQueryResult> queries)
        {
            reply ??= "";
            var charts = new List<AiChartViewDto>();
            if (queries == null || queries.Count == 0)
            {
                return (StripFences(reply), charts);
            }

            var specs = ExtractSpecs(reply);
            var cleaned = StripFences(reply);
            var byId = queries
                .Where(q => q != null && !string.IsNullOrWhiteSpace(q.DatasetId) && q.Rows != null && q.Rows.Count > 0)
                .GroupBy(q => q.DatasetId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

            if (specs.Count == 0)
            {
                foreach (var q in byId.Values)
                {
                    var auto = AutoSpec(q);
                    var view = Bind(auto, q);
                    if (view != null) charts.Add(view);
                }
                return (cleaned, charts);
            }

            foreach (var spec in specs)
            {
                if (spec == null || string.IsNullOrWhiteSpace(spec.DatasetId))
                {
                    continue;
                }
                if (!byId.TryGetValue(spec.DatasetId.Trim(), out var query))
                {
                    continue;
                }
                var view = Bind(spec, query);
                if (view != null) charts.Add(view);
            }

            if (charts.Count == 0)
            {
                foreach (var q in byId.Values)
                {
                    var view = Bind(AutoSpec(q), q);
                    if (view != null) charts.Add(view);
                }
            }

            return (cleaned, charts);
        }

        public static List<AiChartViewDto> FromDataJson(string dataJson)
        {
            return ParseExtra(dataJson)?.Charts ?? [];
        }

        public static List<AiChatToolCallDto> FromToolsDataJson(string dataJson)
        {
            return ParseExtra(dataJson)?.Tools ?? [];
        }

        public static string ToDataJson(List<AiChartViewDto> charts, List<AiChatToolCallDto> tools = null)
        {
            var hasCharts = charts != null && charts.Count > 0;
            var hasTools = tools != null && tools.Count > 0;
            if (!hasCharts && !hasTools)
            {
                return null;
            }
            return JsonConvert.SerializeObject(new ChartExtra
            {
                Charts = hasCharts ? charts : null,
                Tools = hasTools ? tools : null
            });
        }

        private static ChartExtra ParseExtra(string dataJson)
        {
            if (string.IsNullOrWhiteSpace(dataJson))
            {
                return null;
            }
            try
            {
                return JsonConvert.DeserializeObject<ChartExtra>(dataJson);
            }
            catch
            {
                return null;
            }
        }

        private sealed class ChartExtra
        {
            public List<AiChartViewDto> Charts { get; set; }
            public List<AiChatToolCallDto> Tools { get; set; }
        }

        private static List<AiChartSpecDto> ExtractSpecs(string reply)
        {
            var list = new List<AiChartSpecDto>();
            foreach (Match m in Fence.Matches(reply))
            {
                var spec = TryParseSpec(m.Groups[1].Value);
                if (spec != null) list.Add(spec);
            }
            return list;
        }

        private static AiChartSpecDto TryParseSpec(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }
            try
            {
                var jo = JObject.Parse(raw.Trim());
                var spec = new AiChartSpecDto
                {
                    DatasetId = jo.Value<string>("datasetId")?.Trim(),
                    Type = jo.Value<string>("type")?.Trim()?.ToLowerInvariant(),
                    Title = jo.Value<string>("title")?.Trim(),
                    XField = jo["xField"]?.Value<string>()?.Trim() ?? jo["xAxis"]?.Value<string>()?.Trim()
                };
                if (jo["series"] is JArray arr)
                {
                    foreach (var item in arr)
                    {
                        spec.Series.Add(new AiChartSeriesSpec
                        {
                            Name = item.Value<string>("name")?.Trim(),
                            Field = item.Value<string>("field")?.Trim()
                        });
                    }
                }
                return spec;
            }
            catch
            {
                return null;
            }
        }

        private static string StripFences(string reply)
        {
            var text = Fence.Replace(reply, "").Trim();
            return text;
        }

        private static AiChartSpecDto AutoSpec(AiChartQueryResult query)
        {
            var x = query.Fields.FirstOrDefault(f => f.Kind == "category")?.Field
                ?? query.Fields.FirstOrDefault()?.Field;
            var nums = query.Fields.Where(f => f.Kind == "number").ToList();
            var type = string.IsNullOrWhiteSpace(query.SuggestedType) ? "line" : query.SuggestedType.ToLowerInvariant();
            if (query.AllowedTypes != null && query.AllowedTypes.Length > 0 && !query.AllowedTypes.Contains(type))
            {
                type = query.AllowedTypes[0];
            }
            return new AiChartSpecDto
            {
                DatasetId = query.DatasetId,
                Type = type,
                Title = query.Title,
                XField = x,
                Series = nums.Select(f => new AiChartSeriesSpec { Name = f.Label, Field = f.Field }).ToList()
            };
        }

        private static AiChartViewDto Bind(AiChartSpecDto spec, AiChartQueryResult query)
        {
            if (spec == null || query == null)
            {
                return null;
            }
            var allowedFields = new HashSet<string>(query.Fields.Select(f => f.Field), StringComparer.OrdinalIgnoreCase);
            var type = (spec.Type ?? "line").ToLowerInvariant();
            if (!Types.Contains(type))
            {
                type = query.SuggestedType ?? "line";
            }
            if (query.AllowedTypes != null && query.AllowedTypes.Length > 0
                && !query.AllowedTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
            {
                type = query.AllowedTypes[0];
            }

            var xField = spec.XField;
            if (string.IsNullOrWhiteSpace(xField) || !allowedFields.Contains(xField))
            {
                xField = query.Fields.FirstOrDefault(f => f.Kind == "category")?.Field
                    ?? query.Fields.FirstOrDefault()?.Field;
            }

            var series = (spec.Series ?? [])
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Field) && allowedFields.Contains(s.Field))
                .Select(s => new AiChartSeriesSpec
                {
                    Field = s.Field,
                    Name = string.IsNullOrWhiteSpace(s.Name)
                        ? (query.Fields.FirstOrDefault(f => f.Field.Equals(s.Field, StringComparison.OrdinalIgnoreCase))?.Label ?? s.Field)
                        : s.Name
                })
                .ToList();
            if (series.Count == 0)
            {
                series = query.Fields.Where(f => f.Kind == "number")
                    .Select(f => new AiChartSeriesSpec { Name = f.Label, Field = f.Field }).ToList();
            }
            if (string.IsNullOrWhiteSpace(xField) || series.Count == 0)
            {
                return null;
            }

            return new AiChartViewDto
            {
                DatasetId = query.DatasetId,
                Type = type,
                Title = string.IsNullOrWhiteSpace(spec.Title) ? query.Title : spec.Title,
                XField = xField,
                Series = series,
                Rows = query.Rows,
                // 字段标签随图下发：前端导出 Excel 时据此生成中文表头，无需另维护映射
                Fields = query.Fields
            };
        }
    }
}
