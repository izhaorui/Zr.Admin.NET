using System.Diagnostics;
using System.Text.Json;
using Infrastructure;
using Infrastructure.AI;
using Infrastructure.Attribute;
using Infrastructure.Model;
using ZR.Model;
using ZR.Model.AI;
using ZR.Model.AI.Dto;
using ZR.ServiceCore.AI.IService;
// ServiceCore 全局引入了 Newtonsoft.Json，这里显式指向 System.Text.Json
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ZR.ServiceCore.AI.Governance
{
    /// <summary>
    /// AI 渠道管理实现：CRUD、启停与连通性测试。
    /// <para>
    /// 密钥链路：入参新值 → DataProtection 加密入库；入参为掩码（****）或空 → 保留库中原密文；
    /// 任何返回前端的字段都只给掩码。
    /// </para>
    /// <para>
    /// 环境隔离：列表按当前环境过滤，所有<b>按 Id</b> 的操作（详情/保存/删除/启停/取已存密钥测试）
    /// 也必须带同样的环境条件（本环境 + * 通用行），否则共享库里当前环境的操作者可以改到、
    /// 删掉或测试别的环境的渠道。
    /// </para>
    /// </summary>
    [AppService(ServiceType = typeof(IAiProviderService), ServiceLifetime = LifeTime.Transient)]
    public class AiProviderService : BaseService<AiProvider>, IAiProviderService
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public PagedInfo<AiProviderListDto> GetList(AiProviderQueryDto query)
        {
            query ??= new AiProviderQueryDto();
            var environment = AiProviderStore.ResolveEnvironment();

            var expression = Expressionable.Create<AiProvider>();
            expression.And(x => x.Environment == environment || x.Environment == "*");
            expression.AndIF(!string.IsNullOrWhiteSpace(query.Keyword),
                x => x.Name.Contains(query.Keyword) || x.Provider.Contains(query.Keyword));
            expression.AndIF(!string.IsNullOrWhiteSpace(query.Provider), x => x.Provider == query.Provider);
            expression.AndIF(query.Status.HasValue, x => x.Status == query.Status.Value);

            var pageNum = Math.Max(1, query.PageNum);
            var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 200);
            var total = 0;
            var rows = Context.Queryable<AiProvider>()
                .Where(expression.ToExpression())
                .OrderByDescending(x => x.Priority)
                .OrderBy(x => x.Sort)
                .OrderBy(x => x.Id)
                .ToPageList(pageNum, pageSize, ref total);

            return new PagedInfo<AiProviderListDto>
            {
                PageIndex = pageNum,
                PageSize = pageSize,
                TotalNum = total,
                Result = rows.Select(ToDto).ToList()
            };
        }

        public AiProviderListDto Get(long id)
        {
            var entity = LoadOwned(id);
            if (entity == null) throw new CustomException("渠道不存在或不属于当前环境");
            return ToDto(entity);
        }

        public long Save(AiProviderSaveDto input)
        {
            if (input == null) throw new CustomException("参数不能为空");

            var provider = (input.Provider ?? string.Empty).Trim().ToLowerInvariant();
            var name = (input.Name ?? string.Empty).Trim();
            if (provider.Length == 0) throw new CustomException("厂商标识不能为空");
            if (name.Length == 0) throw new CustomException("渠道名不能为空");

            var entity = input.Id > 0
                ? LoadOwned(input.Id)
                : null;
            if (input.Id > 0 && entity == null) throw new CustomException("渠道不存在或不属于当前环境");

            var environment = entity?.Environment ?? AiProviderStore.ResolveEnvironment();

            // 唯一键：Environment + Name（同厂商多渠道靠 Name 区分）
            // 读取时 * 行对所有环境生效，查重必须带上 * 行，否则会出现 (Production, foo) + (*, foo) 两条都命中
            var duplicated = Context.Queryable<AiProvider>()
                .Any(x => x.Id != input.Id && x.Name == name
                          && (x.Environment == environment || x.Environment == "*" || environment == "*"));
            if (duplicated)
            {
                throw new CustomException($"当前环境下已存在同名渠道「{name}」，请换一个渠道名");
            }

            var apiKey = (input.ApiKey ?? string.Empty).Trim();
            var keepExistingKey = AiConfigMasking.IsMasked(apiKey) || apiKey.Length == 0;
            if (!keepExistingKey)
            {
                // 生产环境 fail-closed：加密能力不可用时直接拒绝保存，绝不把明文密钥落库
                AiConfigProtector.EnsureCanProtect("渠道密钥");
            }
            var apiKeyCipher = keepExistingKey
                ? entity?.ApiKeyCipher                      // 没改密钥：保留原密文
                : AiConfigProtector.Protect(apiKey);        // 新密钥：加密
            var modelsJson = JsonSerializer.Serialize(input.Models ?? new List<string>());
            var now = DateTime.Now;
            var operatorName = App.HttpContext?.GetName();

            if (entity == null)
            {
                entity = new AiProvider
                {
                    Environment = environment,
                    Provider = provider,
                    Name = name,
                    BaseUrl = TrimToNull(input.BaseUrl),
                    ChatEndpoint = TrimToNull(input.ChatEndpoint),
                    ApiKeyCipher = apiKeyCipher,
                    Model = TrimToNull(input.Model),
                    VisionModel = TrimToNull(input.VisionModel),
                    ModelsJson = modelsJson,
                    TimeoutSeconds = input.TimeoutSeconds,
                    Priority = input.Priority,
                    Weight = input.Weight,
                    Status = NormalizeStatus(input.Status),
                    Sort = input.Sort,
                    Version = 1,
                    Remark = input.Remark,
                    Create_by = operatorName,
                    Create_time = now
                };
                entity.Id = Context.Insertable(entity).ExecuteReturnBigIdentity();
            }
            else
            {
                entity.Provider = provider;
                entity.Name = name;
                entity.BaseUrl = TrimToNull(input.BaseUrl);
                entity.ChatEndpoint = TrimToNull(input.ChatEndpoint);
                entity.ApiKeyCipher = apiKeyCipher;
                entity.Model = TrimToNull(input.Model);
                entity.VisionModel = TrimToNull(input.VisionModel);
                entity.ModelsJson = modelsJson;
                entity.TimeoutSeconds = input.TimeoutSeconds;
                entity.Priority = input.Priority;
                entity.Weight = input.Weight;
                entity.Status = NormalizeStatus(input.Status);
                entity.Sort = input.Sort;
                entity.Version += 1;
                entity.Remark = input.Remark;
                entity.Update_by = operatorName;
                entity.Update_time = now;

                Context.Updateable(entity)
                    .UpdateColumns(x => new
                    {
                        x.Provider,
                        x.Name,
                        x.BaseUrl,
                        x.ChatEndpoint,
                        x.ApiKeyCipher,
                        x.Model,
                        x.VisionModel,
                        x.ModelsJson,
                        x.TimeoutSeconds,
                        x.Priority,
                        x.Weight,
                        x.Status,
                        x.Sort,
                        x.Version,
                        x.Remark,
                        x.Update_by,
                        x.Update_time
                    })
                    .ExecuteCommand();
            }

            AiProviderStore.Invalidate();
            Logger.Info($"AI 渠道已保存 id={entity.Id} provider={provider} name={name} env={environment} status={entity.Status} priority={entity.Priority} version={entity.Version} operator={operatorName}");
            return entity.Id;
        }

        public int Delete(long id)
        {
            var entity = LoadOwned(id);
            if (entity == null)
            {
                Logger.Warn($"AI 渠道未删除：渠道不存在或不属于当前环境 id={id} env={AiProviderStore.ResolveEnvironment()} operator={App.HttpContext?.GetName()}");
                return 0;
            }

            var affected = Context.Deleteable<AiProvider>().Where(x => x.Id == id).ExecuteCommand();
            AiProviderStore.Invalidate();
            Logger.Info($"AI 渠道已删除 id={id} provider={entity.Provider} name={entity.Name} operator={App.HttpContext?.GetName()}");
            return affected;
        }

        public int ChangeStatus(long id, int status)
        {
            var entity = LoadOwned(id);
            if (entity == null) throw new CustomException("渠道不存在或不属于当前环境");

            entity.Status = NormalizeStatus(status);
            entity.Version += 1;
            entity.Update_by = App.HttpContext?.GetName();
            entity.Update_time = DateTime.Now;

            var affected = Context.Updateable(entity)
                .UpdateColumns(x => new { x.Status, x.Version, x.Update_by, x.Update_time })
                .ExecuteCommand();
            AiProviderStore.Invalidate();
            Logger.Info($"AI 渠道状态变更 id={id} name={entity.Name} status={entity.Status} operator={entity.Update_by}");
            return affected;
        }

        public async Task<AiProviderTestResult> TestAsync(AiProviderTestInput input)
        {
            var result = new AiProviderTestResult();
            if (input == null)
            {
                result.Message = "参数不能为空";
                return result;
            }

            var provider = (input.Provider ?? string.Empty).Trim().ToLowerInvariant();
            var model = (input.Model ?? string.Empty).Trim();
            result.Provider = provider;
            result.Model = model;
            if (provider.Length == 0)
            {
                result.Message = "请填写厂商标识";
                return result;
            }
            if (model.Length == 0)
            {
                result.Message = "请填写模型名";
                return result;
            }

            if (!TryResolveApiKey(input, out var apiKey, out var keyError))
            {
                result.Message = keyError;
                return result;
            }

            // 用文件配置作基线，再整体替换为“待测试渠道”；顶层字段必须清空，否则会覆盖分项
            var options = AiOptionsResolver.GetFromFile();
            options.Enable = true;
            options.Provider = provider;
            options.BaseUrl = null;
            options.ChatEndpoint = null;
            options.Model = null;
            options.ApiKey = null;
            options.VisionProvider = null;
            options.VisionBaseUrl = null;
            options.VisionChatEndpoint = null;
            options.VisionModel = null;
            options.VisionApiKey = null;
            options.TimeoutSeconds = Math.Clamp(input.TimeoutSeconds ?? 15, 1, 120);
            options.MaxTokens = 8;
            options.EnableRetry = false;
            options.Providers = new List<AiProviderOptions>
            {
                new()
                {
                    Provider = provider,
                    BaseUrl = TrimToNull(input.BaseUrl),
                    ChatEndpoint = TrimToNull(input.ChatEndpoint),
                    ApiKey = apiKey,
                    Model = model,
                    TimeoutSeconds = options.TimeoutSeconds
                }
            };

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var reply = await AiLlmClient.ChatAsync(options, "你是连通性测试助手，只回复 OK。", "ping", AiSceneCatalog.HealthCheck)
                    .ConfigureAwait(false);
                stopwatch.Stop();
                result.Ok = true;
                result.LatencyMs = stopwatch.ElapsedMilliseconds;
                result.Message = string.IsNullOrWhiteSpace(reply)
                    ? "连接成功（模型返回空内容，可检查模型是否可用）"
                    : $"连接成功：{AiHelper.ClipText(reply.Trim(), 60)}";
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.LatencyMs = stopwatch.ElapsedMilliseconds;
                result.Message = ex switch
                {
                    AiException ai => string.IsNullOrWhiteSpace(ai.UserMessage) ? ai.Message : ai.UserMessage,
                    HttpRequestException http => http.Message,
                    _ => "连接失败：" + ex.Message
                };
                Logger.Warn($"AI 渠道测试失败 provider={provider} model={model} elapsedMs={result.LatencyMs} err={ex.Message}");
            }

            return result;
        }

        /// <summary>单次最多返回的模型数，避免个别渠道返回上千条把下拉和响应体撑大。</summary>
        private const int MaxModels = 500;

        public async Task<AiProviderModelsResult> FetchModelsAsync(AiProviderTestInput input)
        {
            var result = new AiProviderModelsResult();
            if (input == null)
            {
                result.Message = "参数不能为空";
                return result;
            }

            var provider = (input.Provider ?? string.Empty).Trim().ToLowerInvariant();
            result.Provider = provider;
            if (provider.Length == 0)
            {
                result.Message = "请填写厂商标识";
                return result;
            }

            // Azure OpenAI 按部署名调用，没有可供枚举的 /models；直接给可行动提示而不是发一次必然 404 的请求
            if (provider is "azure" or "azureopenai")
            {
                result.Message = "Azure OpenAI 按部署名调用，不提供模型列表接口，请手工填写模型名";
                return result;
            }

            if (!TryResolveApiKey(input, out var apiKey, out var keyError))
            {
                result.Message = keyError;
                return result;
            }

            var uri = BuildModelsUri(provider, input.BaseUrl, input.ChatEndpoint);
            var timeoutSeconds = Math.Clamp(input.TimeoutSeconds ?? 15, 1, 60);
            var headers = string.IsNullOrWhiteSpace(apiKey)
                ? null
                : new Dictionary<string, string> { ["Authorization"] = "Bearer " + apiKey };

            try
            {
                var response = await HttpHelper
                    .HttpGetDetailedAsync(uri.ToString(), timeoutSeconds, headers)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    result.Message = BuildModelsErrorHint(response.StatusCode, uri);
                    Logger.Warn($"AI 渠道拉取模型失败 provider={provider} status={response.StatusCode} path={uri.AbsolutePath}");
                    return result;
                }

                result.Models = ParseModels(response.Content);
                result.Ok = result.Models.Count > 0;
                result.Message = result.Ok
                    ? $"已获取 {result.Models.Count} 个模型"
                    : "接口已连通，但未返回可识别的模型列表，请手工填写模型名";
                Logger.Info($"AI 渠道拉取模型完成 provider={provider} count={result.Models.Count} path={uri.AbsolutePath}");
            }
            catch (OperationCanceledException)
            {
                // 超时/取消都走这里：HttpHelper 内部是独立 CTS，取消即超时
                result.Message = $"请求超时（{timeoutSeconds} 秒），请检查网络或稍后重试";
                Logger.Warn($"AI 渠道拉取模型超时 provider={provider} timeout={timeoutSeconds}s path={uri.AbsolutePath}");
            }
            catch (Exception ex)
            {
                result.Message = "拉取模型失败：" + ex.Message;
                Logger.Warn(ex, $"AI 渠道拉取模型异常 provider={provider} path={uri.AbsolutePath}");
            }

            return result;
        }

        /// <summary>
        /// 由聊天端点反推模型列表端点：把末段 chat/completions 换成 models，
        /// 这样各家 /v1 前缀的差异自动保留（openai=/v1/models、deepseek=/models、qwen=/compatible-mode/v1/models）。
        /// 端点不含 chat/completions 时按 OpenAI 约定拼 /v1/models。
        /// </summary>
        private static Uri BuildModelsUri(string provider, string baseUrl, string chatEndpoint)
        {
            var profile = AiProviderProfiles.Resolve(provider);
            var root = string.IsNullOrWhiteSpace(baseUrl) ? profile.DefaultBaseUrl : baseUrl.Trim();
            var endpoint = string.IsNullOrWhiteSpace(chatEndpoint) ? profile.DefaultEndpoint : chatEndpoint.Trim();
            if (!endpoint.StartsWith("/"))
            {
                endpoint = "/" + endpoint;
            }

            var modelsEndpoint = endpoint.Contains("chat/completions", StringComparison.OrdinalIgnoreCase)
                ? endpoint.Replace("chat/completions", "models", StringComparison.OrdinalIgnoreCase)
                : "/v1/models";

            return new Uri(new Uri(root.TrimEnd('/') + "/"), modelsEndpoint.TrimStart('/'));
        }

        /// <summary>
        /// 解析模型列表：兼容 OpenAI 的 {"data":[{"id":"..."}]}、Ollama 的 {"models":[{"name":"..."}]}
        /// 以及直接返回字符串数组的形式；解析不出来按空列表处理（由调用方提示手工填写）。
        /// </summary>
        private static List<string> ParseModels(string content)
        {
            var models = new List<string>();
            if (string.IsNullOrWhiteSpace(content)) return models;

            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                JsonElement array;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    array = root;
                }
                else if (root.ValueKind == JsonValueKind.Object
                         && (root.TryGetProperty("data", out var data) || root.TryGetProperty("models", out data))
                         && data.ValueKind == JsonValueKind.Array)
                {
                    array = data;
                }
                else
                {
                    return models;
                }

                foreach (var item in array.EnumerateArray())
                {
                    var name = item.ValueKind switch
                    {
                        JsonValueKind.String => item.GetString(),
                        JsonValueKind.Object => ReadFirstString(item, "id", "name", "model"),
                        _ => null
                    };
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    name = name.Trim();
                    if (!models.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        models.Add(name);
                    }
                }
            }
            catch (JsonException)
            {
                // 非 JSON 响应（如网关返回 HTML）：按无模型处理，不阻断接口
                return new List<string>();
            }

            models.Sort(StringComparer.OrdinalIgnoreCase);
            return models.Count > MaxModels ? models.Take(MaxModels).ToList() : models;
        }

        private static string ReadFirstString(JsonElement element, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }
            return null;
        }

        /// <summary>非 2xx 的可行动提示：只说状态与路径，不回显响应体（可能含内网地址或密钥片段）。</summary>
        private static string BuildModelsErrorHint(int statusCode, Uri uri) => statusCode switch
        {
            401 or 403 => $"鉴权失败（HTTP {statusCode}），请检查 API Key 是否有效",
            404 => $"该渠道未提供模型列表接口（HTTP 404，{uri.AbsolutePath}），请手工填写模型名",
            429 => "请求过于频繁（HTTP 429），请稍后重试",
            >= 500 => $"上游服务异常（HTTP {statusCode}），请稍后重试",
            _ => $"拉取失败（HTTP {statusCode}），请检查 BaseUrl 是否正确"
        };

        /// <summary>
        /// 取测试/拉模型用的明文密钥：新填的值优先；掩码或留空时回退库中已保存的密钥（需带 Id）。
        /// 失败时通过 <paramref name="error"/> 返回可直接展示给用户的提示。
        /// </summary>
        private bool TryResolveApiKey(AiProviderTestInput input, out string apiKey, out string error)
        {
            error = null;
            apiKey = (input?.ApiKey ?? string.Empty).Trim();
            if (apiKey.Length > 0 && !AiConfigMasking.IsMasked(apiKey))
            {
                return true;
            }

            if (input == null || input.Id <= 0)
            {
                apiKey = null;
                error = "请填写 API Key";
                return false;
            }

            var row = LoadOwned(input.Id);
            if (row == null)
            {
                apiKey = null;
                error = "渠道不存在或不属于当前环境，无法使用已保存的密钥";
                return false;
            }

            try
            {
                apiKey = AiConfigProtector.Unprotect(row.ApiKeyCipher);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"渠道密钥解密失败 id={input.Id}");
                apiKey = null;
                error = "已保存的密钥无法解密，请重新填写";
                return false;
            }
        }

        /// <summary>
        /// 按 Id 取“当前环境可见”的渠道：本环境专属行 + * 通用行。
        /// <para>
        /// 共享库同时存放多个环境的渠道，只按 Id 查询会让当前环境的操作者读到/改到/删掉别的环境的渠道；
        /// 因此所有按 Id 的操作都必须走本方法。不属于当前环境时返回 null，由调用方给出统一提示。
        /// </para>
        /// </summary>
        private AiProvider LoadOwned(long id)
        {
            var environment = AiProviderStore.ResolveEnvironment();
            return Context.Queryable<AiProvider>()
                .First(x => x.Id == id && (x.Environment == environment || x.Environment == "*"));
        }

        /// <summary>实体 → 列表 DTO：解密后只回显掩码，避免密文/明文出接口。</summary>
        private static AiProviderListDto ToDto(AiProvider entity)
        {
            var models = new List<string>();
            if (!string.IsNullOrWhiteSpace(entity.ModelsJson))
            {
                try
                {
                    models = JsonSerializer.Deserialize<List<string>>(entity.ModelsJson) ?? new List<string>();
                }
                catch (JsonException)
                {
                    models = new List<string>();
                }
            }

            string apiKeyMask = null;
            if (!string.IsNullOrWhiteSpace(entity.ApiKeyCipher))
            {
                try
                {
                    apiKeyMask = AiConfigMasking.MaskValue(AiConfigProtector.Unprotect(entity.ApiKeyCipher));
                }
                catch (Exception)
                {
                    // 解密失败不阻断列表展示：用固定占位提示管理员重新填写
                    apiKeyMask = "****";
                }
            }

            return new AiProviderListDto
            {
                Id = entity.Id,
                Environment = entity.Environment,
                Provider = entity.Provider,
                Name = entity.Name,
                BaseUrl = entity.BaseUrl,
                ChatEndpoint = entity.ChatEndpoint,
                Model = entity.Model,
                VisionModel = entity.VisionModel,
                Models = models,
                TimeoutSeconds = entity.TimeoutSeconds,
                Priority = entity.Priority,
                Weight = entity.Weight,
                AutoDisable = entity.AutoDisable,
                Status = entity.Status,
                Sort = entity.Sort,
                Version = entity.Version,
                ApiKeyMask = apiKeyMask,
                Remark = entity.Remark,
                Update_by = entity.Update_by,
                Update_time = entity.Update_time
            };
        }

        /// <summary>状态归一化：只认 0（启用）/ 1（停用），避免前端传 2 之类的值入库后永不参与路由。</summary>
        private static int NormalizeStatus(int status) => status == 0 ? 0 : 1;

        private static string TrimToNull(string value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
