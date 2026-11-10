using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;

namespace ZR.AiKnowledge.Providers
{
    /// <summary>
    /// 外部知识库 HTTP 公共部分：统一超时、鉴权头、错误分类与宽松解析工具。
    /// <para>
    /// HttpClient 静态复用（同一上游的短请求），超时按配置用 CancellationToken 控制，
    /// 不使用默认的整请求超时，避免与调用方取消语义打架。
    /// </para>
    /// </summary>
    internal static class ExternalKnowledgeHttp
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 请求体序列化选项：中文不转义为 \uXXXX。JSON 字符串内的中文无需转义，
        /// 不转义可减少约一半体积，也便于对接方在抓包/日志里直接看清 query。
        /// </summary>
        internal static readonly JsonSerializerOptions PayloadJson = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static readonly HttpClient Client = new(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(10)
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        /// <summary>POST JSON 并返回解析后的响应；可预期失败抛 <see cref="KnowledgeProviderException"/>。</summary>
        public static async Task<JsonDocument> PostJsonAsync(string url, string json, string apiKey,
            int timeoutSeconds, CancellationToken cancellationToken)
        {
            var timeout = Math.Clamp(timeoutSeconds <= 0 ? 15 : timeoutSeconds, 1, 120);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            }

            HttpResponseMessage response;
            try
            {
                response = await Client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new KnowledgeProviderException($"知识库请求超时 url={url} timeout={timeout}s",
                    $"知识库响应超时（{timeout}s），请稍后重试。");
            }
            catch (HttpRequestException ex)
            {
                throw new KnowledgeProviderException($"知识库请求失败 url={url} err={ex.Message}",
                    "知识库服务连接失败，请稍后重试或联系管理员。");
            }

            using (response)
            {
                string body;
                try
                {
                    body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new KnowledgeProviderException($"读取知识库响应超时 url={url}",
                        "知识库响应超时，请稍后重试。");
                }

                if (!response.IsSuccessStatusCode)
                {
                    // 只记状态码与截断后的报文，绝不记录 ApiKey
                    Logger.Warn($"知识库返回非成功状态 status={(int)response.StatusCode} url={url} body={Truncate(body, 300)}");
                    throw new KnowledgeProviderException($"知识库 HTTP {(int)response.StatusCode} url={url} body={Truncate(body, 500)}",
                        MapUserMessage((int)response.StatusCode));
                }

                try
                {
                    return JsonDocument.Parse(body);
                }
                catch (JsonException ex)
                {
                    Logger.Error(ex, $"知识库响应不是合法 JSON url={url} body={Truncate(body, 300)}");
                    throw new KnowledgeProviderException("知识库响应格式非法", "知识库返回内容无法识别，请检查接口配置。");
                }
            }
        }

        /// <summary>把 HTTP 状态码转成用户能行动的中文提示（不暴露上游细节）。</summary>
        public static string MapUserMessage(int statusCode) => statusCode switch
        {
            401 or 403 => "知识库鉴权失败，请检查访问密钥配置。",
            404 => "知识库接口地址不存在，请检查接口配置。",
            429 => "知识库调用过于频繁，请稍后再试。",
            >= 500 => "知识库服务暂时不可用，请稍后重试。",
            _ => "知识库调用失败，请稍后重试或联系管理员。"
        };

        /// <summary>从对象中按候选字段名读取字符串（数字按原文返回）；缺失返回 null。</summary>
        public static string ReadString(JsonElement element, params string[] names)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;
            foreach (var name in names)
            {
                if (!element.TryGetProperty(name, out var value)) continue;
                return value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => value.GetRawText(),
                    _ => null
                };
            }
            return null;
        }

        /// <summary>从对象中按候选字段名读取数值（兼容数字与数字字符串）；缺失或非法返回 0。</summary>
        public static double ReadDouble(JsonElement element, params string[] names)
        {
            if (element.ValueKind != JsonValueKind.Object) return 0;
            foreach (var name in names)
            {
                if (!element.TryGetProperty(name, out var value)) continue;
                if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
                if (value.ValueKind == JsonValueKind.String
                    && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }
            return 0;
        }

        /// <summary>
        /// 找到片段数组：根即数组，或根对象下 hits / results / data / documents 之一。
        /// 找不到返回空集合（视为"知识库未覆盖"，不抛异常）。
        /// </summary>
        public static List<JsonElement> FindArray(JsonElement root, params string[] names)
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.EnumerateArray().ToList();
            }
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in names)
                {
                    if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array)
                    {
                        return value.EnumerateArray().ToList();
                    }
                }
            }
            return new List<JsonElement>();
        }

        /// <summary>日志截断：避免上游异常报文刷屏。</summary>
        public static string Truncate(string text, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return text.Length <= maxLen ? text : text[..maxLen] + "…";
        }
    }

    /// <summary>
    /// 自建 HTTP 检索接口（Provider=generic）。
    /// <para>请求：<c>{"query":"...","topK":5,"minScore":0,"kbId":"","filters":{}}</c></para>
    /// <para>
    /// 响应（宽松解析，任选一种）：根数组，或 <c>{"hits":[...]}</c> / results / data / documents；
    /// 片段字段支持 content|text|chunk|page_content|answer、score|similarity|relevance、
    /// title|documentName|name、source|url|link、locator|page|position。
    /// </para>
    /// </summary>
    [AppService(ServiceType = typeof(IKnowledgeProvider), ServiceLifetime = LifeTime.Transient)]
    public class GenericHttpKnowledgeProvider : IKnowledgeProvider
    {
        public string Name => AiKnowledgeOptions.GenericProvider;

        public async Task<KnowledgeSearchResult> SearchAsync(KnowledgeQuery query, CancellationToken cancellationToken = default)
        {
            var options = AiKnowledgeSettings.Current;
            if (string.IsNullOrWhiteSpace(options.Endpoint))
            {
                throw new KnowledgeProviderException("AiKnowledge:Endpoint 未配置", "知识库地址未配置，请联系管理员。");
            }

            var question = query?.Question ?? string.Empty;
            var topK = Math.Clamp(query?.TopK ?? options.TopK, 1, 20);
            var minScore = query?.MinScore > 0 ? query.MinScore : options.MinScore;

            var body = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["query"] = question,
                ["topK"] = topK,
                ["minScore"] = minScore,
                ["kbId"] = query?.KbId ?? options.KbId ?? string.Empty,
                ["filters"] = query?.Filters ?? new Dictionary<string, string>()
            }, ExternalKnowledgeHttp.PayloadJson);

            using var doc = await ExternalKnowledgeHttp.PostJsonAsync(
                options.Endpoint.Trim(), body, options.ApiKey, options.TimeoutSeconds, cancellationToken).ConfigureAwait(false);

            var hits = new List<KnowledgeHit>();
            foreach (var item in ExternalKnowledgeHttp.FindArray(doc.RootElement, "hits", "results", "data", "documents"))
            {
                var content = ExternalKnowledgeHttp.ReadString(item, "content", "text", "chunk", "page_content", "answer");
                if (string.IsNullOrWhiteSpace(content)) continue;

                hits.Add(new KnowledgeHit
                {
                    Content = content,
                    Score = ExternalKnowledgeHttp.ReadDouble(item, "score", "similarity", "relevance"),
                    Title = ExternalKnowledgeHttp.ReadString(item, "title", "documentName", "document_name", "name"),
                    Source = ExternalKnowledgeHttp.ReadString(item, "source", "url", "link", "fileUrl", "file_url"),
                    Locator = ExternalKnowledgeHttp.ReadString(item, "locator", "page", "pageNumber", "page_number", "position")
                });
            }

            return new KnowledgeSearchResult { KbName = options.KbName, Hits = Filter(hits, minScore) };
        }

        /// <summary>按相似度降序；配置了阈值时丢弃明确低于阈值的片段（Score 缺失的片段保留）。</summary>
        private static List<KnowledgeHit> Filter(List<KnowledgeHit> hits, double minScore)
        {
            var query = hits.AsEnumerable();
            if (minScore > 0)
            {
                query = query.Where(h => h.Score <= 0 || h.Score >= minScore);
            }
            return query.OrderByDescending(h => h.Score).ToList();
        }
    }

    /// <summary>
    /// Dify 数据集检索（Provider=dify）。
    /// <para>请求：<c>POST {Endpoint}/datasets/{DatasetId}/retrieve</c>，Bearer 为数据集 API Key。</para>
    /// <para>响应：读取 <c>records[].segment.content</c> / <c>segment.document.name</c> / <c>score</c> / <c>segment.position</c>。</para>
    /// </summary>
    [AppService(ServiceType = typeof(IKnowledgeProvider), ServiceLifetime = LifeTime.Transient)]
    public class DifyKnowledgeProvider : IKnowledgeProvider
    {
        public string Name => AiKnowledgeOptions.DifyProvider;

        public async Task<KnowledgeSearchResult> SearchAsync(KnowledgeQuery query, CancellationToken cancellationToken = default)
        {
            var options = AiKnowledgeSettings.Current;
            if (string.IsNullOrWhiteSpace(options.Endpoint))
            {
                throw new KnowledgeProviderException("AiKnowledge:Endpoint 未配置", "知识库地址未配置，请联系管理员。");
            }
            if (string.IsNullOrWhiteSpace(options.DatasetId))
            {
                throw new KnowledgeProviderException("AiKnowledge:DatasetId 未配置", "知识库数据集未配置，请联系管理员。");
            }
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                throw new KnowledgeProviderException("AiKnowledge:ApiKey 未配置", "知识库访问密钥未配置，请联系管理员。");
            }

            var topK = Math.Clamp(query?.TopK ?? options.TopK, 1, 20);
            var minScore = query?.MinScore > 0 ? query.MinScore : options.MinScore;
            var url = $"{options.Endpoint.TrimEnd('/')}/datasets/{Uri.EscapeDataString(options.DatasetId.Trim())}/retrieve";

            var body = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["query"] = query?.Question ?? string.Empty,
                ["retrieval_model"] = new Dictionary<string, object>
                {
                    ["search_method"] = "hybrid_search",
                    ["reranking_enable"] = false,
                    ["top_k"] = topK,
                    ["score_threshold_enabled"] = minScore > 0,
                    ["score_threshold"] = minScore
                }
            }, ExternalKnowledgeHttp.PayloadJson);

            using var doc = await ExternalKnowledgeHttp.PostJsonAsync(
                url, body, options.ApiKey, options.TimeoutSeconds, cancellationToken).ConfigureAwait(false);

            var hits = new List<KnowledgeHit>();
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("records", out var records)
                && records.ValueKind == JsonValueKind.Array)
            {
                foreach (var record in records.EnumerateArray())
                {
                    if (!record.TryGetProperty("segment", out var segment) || segment.ValueKind != JsonValueKind.Object) continue;

                    var content = ExternalKnowledgeHttp.ReadString(segment, "content");
                    if (string.IsNullOrWhiteSpace(content)) continue;

                    var title = segment.TryGetProperty("document", out var document) && document.ValueKind == JsonValueKind.Object
                        ? ExternalKnowledgeHttp.ReadString(document, "name")
                        : null;
                    var position = ExternalKnowledgeHttp.ReadString(segment, "position");

                    hits.Add(new KnowledgeHit
                    {
                        Content = content,
                        Score = ExternalKnowledgeHttp.ReadDouble(record, "score"),
                        Title = title,
                        Source = title,
                        Locator = string.IsNullOrWhiteSpace(position) ? null : $"第{position}段"
                    });
                }
            }

            if (minScore > 0)
            {
                hits = hits.Where(h => h.Score <= 0 || h.Score >= minScore).ToList();
            }
            return new KnowledgeSearchResult
            {
                KbName = options.KbName,
                Hits = hits.OrderByDescending(h => h.Score).ToList()
            };
        }
    }
}
