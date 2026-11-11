using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Infrastructure.AI
{
    /// <summary>
    /// AI 配置的落库加密：整段 JSON 用 DataProtection 加密后存库，避免密钥随数据库备份泄露。
    /// <para>
    /// 存储格式：<c>enc:v1:{密文}</c>。数据保护服务不可用（如未配置密钥环的宿主、单元测试）时：
    /// 生产环境（含宿主未初始化，按 Production 处理）<strong>失败关闭</strong>，拒绝写入明文；
    /// 非生产环境退化为 <c>raw:{明文}</c> 并告警，保留本地调试可用性。历史 <c>raw:</c> 数据仍可解密。
    /// </para>
    /// </summary>
    public static class AiConfigProtector
    {
        private const string Purpose = "ZRAdmin.AiConfig";
        private const string EncryptedPrefix = "enc:v1:";
        private const string PlainPrefix = "raw:";

        private static readonly ILogger Logger = AiLog.GetLogger("Infrastructure.AI.AiConfigProtector");
        private static readonly object SyncRoot = new();

        private static IDataProtector _protector;
        private static bool _resolved;
        private static bool _unavailableLogged;

        private static IDataProtector Protector
        {
            get
            {
                if (_resolved) return _protector;
                lock (SyncRoot)
                {
                    if (_resolved) return _protector;

                    try
                    {
                        _protector = App.GetService<IDataProtectionProvider>()?.CreateProtector(Purpose);
                    }
                    catch (Exception ex)
                    {
                        if (!_unavailableLogged)
                        {
                            _unavailableLogged = true;
                            Logger.LogWarning(ex, "获取数据保护器失败：生产环境将拒绝保存密钥，非生产环境按明文降级");
                        }
                    }

                    // 只在解析成功时缓存：宿主/DI 尚未就绪（启动早期、--initdb）时不锁定结果，
                    // 否则失败关闭会把“容器还没起来”误判成“DataProtection 缺失”，此后所有保存都被拒。
                    // 解析失败期间只告警一次，避免每次保存都刷日志。
                    _resolved = _protector != null;
                    if (_resolved) _unavailableLogged = false;
                    return _protector;
                }
            }
        }

        /// <summary>
        /// 是否按生产口径处理。宿主未初始化时同样按 Production 处理，
        /// 与 <c>AiConfigStore.ResolveEnvironment</c> 的默认值保持一致，避免“无宿主即放行明文”。
        /// </summary>
        private static bool IsProductionEnvironment
            => string.Equals(InternalApp.WebHostEnvironment?.EnvironmentName ?? "Production", "Production", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 写入前的失败关闭闸门：保存渠道/配置时先调用，确保“无法加密就绝不落库明文”。
        /// 生产环境保护器不可用时直接抛 <see cref="CustomException"/> 拒绝保存；
        /// 非生产环境只告警，保留 <c>raw:</c> 明文降级的本地可用性。
        /// </summary>
        public static void EnsureCanProtect(string fieldName = "AI 配置")
        {
            if (Protector != null) return;

            if (!IsProductionEnvironment)
            {
                Logger.LogWarning($"数据保护服务不可用，{fieldName} 将以明文（raw:）写入；该降级仅允许在非生产环境");
                return;
            }

            Logger.LogError($"数据保护服务不可用，生产环境拒绝以明文保存 {fieldName}，请检查 DataProtection 配置（证书/密钥环）");
            throw PlaintextRejected(fieldName);
        }

        /// <summary>加密（含密钥时为密文）；保护器缺失时生产环境失败关闭，非生产环境退化为 raw: 明文并告警。</summary>
        public static string Protect(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;

            var protector = Protector;
            if (protector != null)
            {
                return EncryptedPrefix + protector.Protect(json);
            }

            // 兜底失败关闭：即使调用方漏了 EnsureCanProtect，也不允许生产环境把明文写进库
            if (IsProductionEnvironment)
            {
                Logger.LogError("数据保护服务不可用，生产环境拒绝以明文写入 AI 配置");
                throw PlaintextRejected("AI 配置");
            }

            Logger.LogWarning("数据保护服务不可用，AI 配置以明文（raw:）写入，请检查 DataProtection 配置");
            return PlainPrefix + json;
        }

        /// <summary>明文落库被拒的统一异常：走 CustomException 以便后台直接把原因回显给操作人。</summary>
        private static CustomException PlaintextRejected(string fieldName)
            => new(ResultCode.CUSTOM_ERROR,
                $"密钥保护服务（DataProtection）不可用，已拒绝以明文保存{fieldName}，请先配置 DataProtection 证书或密钥环",
                notice: false);

        /// <summary>解密；兼容不带前缀的历史明文（手工写入）。</summary>
        public static string Unprotect(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return stored;

            if (stored.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
            {
                var protector = Protector;
                if (protector == null)
                {
                    throw new InvalidOperationException("AI 配置为密文，但数据保护服务不可用（检查 DataProtection:CertificatePath 或密钥环目录）");
                }
                return protector.Unprotect(stored[EncryptedPrefix.Length..]);
            }

            return stored.StartsWith(PlainPrefix, StringComparison.Ordinal)
                ? stored[PlainPrefix.Length..]
                : stored;
        }

        /// <summary>重置探测器缓存（宿主重建数据保护服务后调用；主要供测试）。</summary>
        public static void ResetCache()
        {
            lock (SyncRoot)
            {
                _protector = null;
                _resolved = false;
                _unavailableLogged = false;
            }
        }
    }

    /// <summary>
    /// AI 配置的密钥掩码与“掩码回填”：接口只回显 <c>****后4位</c>，
    /// 保存时遇到掩码值则沿用库中已有密钥，避免前端把掩码写回导致密钥被破坏。
    /// </summary>
    public static class AiConfigMasking
    {
        /// <summary>字段名含这些片段（忽略大小写）时视为敏感值。</summary>
        private static readonly string[] SecretKeyTokens = { "apikey", "secret", "token", "password", "credential" };

        /// <summary>数组项的稳定标识字段（按优先级）：用于掩码回填时跨版本对齐同一条目。</summary>
        private static readonly string[] IdentityKeys = { "provider", "id", "code", "name", "key", "model" };

        /// <summary>把 JSON 中所有敏感字段替换为 <c>****后4位</c>；解析失败原样返回。</summary>
        public static string MaskSecrets(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;
            try
            {
                var node = JsonNode.Parse(json);
                if (node == null) return json;
                Walk(node, mask: true, existing: null);
                return node.ToJsonString();
            }
            catch (Exception)
            {
                // 非法 JSON 不在这里抛：掩码只用于回显，保存路径会单独校验
                return json;
            }
        }

        /// <summary>
        /// 用已有配置回填入参中的掩码密钥（<c>****xxxx</c> 视为“未修改”）。
        /// 入参/已有任一为空时直接返回入参。
        /// </summary>
        public static string MergeMaskedSecrets(string incomingJson, string existingJson)
        {
            if (string.IsNullOrWhiteSpace(incomingJson) || string.IsNullOrWhiteSpace(existingJson))
            {
                return incomingJson;
            }

            try
            {
                var incoming = JsonNode.Parse(incomingJson);
                var existing = JsonNode.Parse(existingJson);
                if (incoming == null || existing == null) return incomingJson;
                Walk(incoming, mask: false, existing);
                return incoming.ToJsonString();
            }
            catch (Exception)
            {
                return incomingJson;
            }
        }

        /// <summary>单值掩码：保留后 4 位，短值只留 ****。</summary>
        public static string MaskValue(string value)
            => string.IsNullOrEmpty(value) ? value : (value.Length <= 8 ? "****" : $"****{value[^4..]}");

        /// <summary>是否为掩码占位值。</summary>
        public static bool IsMasked(string value)
            => !string.IsNullOrEmpty(value) && value.StartsWith("****", StringComparison.Ordinal);

        /// <summary>字段名是否属于敏感字段。</summary>
        public static bool IsSecretKey(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            foreach (var token in SecretKeyTokens)
            {
                if (name.Contains(token, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// 查找回填后仍残留的掩码敏感值，返回其路径（如 <c>providers[1].apiKey</c>），没有则返回 null。
        /// 用于保存前拦截：残留掩码说明前端传来的 <c>****xxxx</c> 没能在原配置里找到对应项
        /// （典型场景是数组项被重排/删除后按标识也匹配不上），此时把掩码当真实密钥存进库会直接废掉密钥。
        /// </summary>
        public static string FindUnresolvedMask(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var node = JsonNode.Parse(json);
                return node == null ? null : FindMasked(node, null);
            }
            catch (Exception)
            {
                return null;   // 非法 JSON 由保存路径的 JSON 校验单独报错
            }
        }

        private static string FindMasked(JsonNode node, string path)
        {
            if (node is JsonObject obj)
            {
                foreach (var pair in obj)
                {
                    var childPath = string.IsNullOrEmpty(path) ? pair.Key : $"{path}.{pair.Key}";
                    if (pair.Value is JsonObject || pair.Value is JsonArray)
                    {
                        var nested = FindMasked(pair.Value, childPath);
                        if (nested != null) return nested;
                        continue;
                    }

                    if (pair.Value is JsonValue value
                        && IsSecretKey(pair.Key)
                        && value.TryGetValue<string>(out var text)
                        && IsMasked(text))
                    {
                        return childPath;
                    }
                }
                return null;
            }

            if (node is JsonArray array)
            {
                for (var i = 0; i < array.Count; i++)
                {
                    if (array[i] == null) continue;
                    var nested = FindMasked(array[i], $"{path}[{i}]");
                    if (nested != null) return nested;
                }
            }

            return null;
        }

        /// <summary>
        /// 递归遍历 JSON：mask=true 时写掩码；mask=false 且 existing 非空时回填掩码值。
        /// </summary>
        private static void Walk(JsonNode node, bool mask, JsonNode existing)
        {
            if (node is JsonObject obj)
            {
                foreach (var key in obj.Select(pair => pair.Key).ToList())
                {
                    var value = obj[key];
                    var exists = existing is JsonObject eo && eo.TryGetPropertyValue(key, out var ev) ? ev : null;

                    if (value is JsonObject || value is JsonArray)
                    {
                        Walk(value, mask, exists);
                        continue;
                    }

                    if (!IsSecretKey(key) || value is not JsonValue jsonValue) continue;
                    if (!jsonValue.TryGetValue<string>(out var text) || string.IsNullOrEmpty(text)) continue;

                    if (mask)
                    {
                        obj[key] = MaskValue(text);
                    }
                    else if (IsMasked(text)
                             && exists is JsonValue existingValue
                             && existingValue.TryGetValue<string>(out var existingText)
                             && !string.IsNullOrEmpty(existingText))
                    {
                        obj[key] = existingText;
                    }
                }
                return;
            }

            if (node is JsonArray array)
            {
                var existingArray = existing as JsonArray;
                for (var i = 0; i < array.Count; i++)
                {
                    var item = array[i];
                    if (item == null) continue;
                    Walk(item, mask, FindExistingItem(existingArray, item, i));
                }
            }
        }

        /// <summary>
        /// 数组项回填对齐优先用稳定标识（Provider/Id/Code/Name…），仅在没有标识时退回下标。
        /// 按下标对应会在前端重排、删除中间项时把密钥错配到别的条目上（例如视觉渠道换成主渠道的 Key）。
        /// </summary>
        private static JsonNode FindExistingItem(JsonArray existingArray, JsonNode item, int index)
        {
            if (existingArray == null) return null;

            var identity = TryGetIdentity(item);
            if (identity != null)
            {
                foreach (var candidate in existingArray)
                {
                    if (candidate == null) continue;
                    if (string.Equals(TryGetIdentity(candidate), identity, StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }

                // 有稳定标识但旧数据里没有同一项：视为新增项，不回填，
                // 否则会按位置取到别的条目，把别人的密钥抄过来
                return null;
            }

            return index < existingArray.Count ? existingArray[index] : null;
        }

        /// <summary>取数组项的稳定标识（如 <c>provider:qwen</c>）；取不到返回 null。</summary>
        private static string TryGetIdentity(JsonNode item)
        {
            if (item is not JsonObject obj) return null;

            foreach (var key in IdentityKeys)
            {
                if (!obj.TryGetPropertyValue(key, out var value) || value is not JsonValue jsonValue) continue;

                if (!jsonValue.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text)) continue;
                return $"{key}:{text.Trim()}";
            }
            return null;
        }
    }
}
