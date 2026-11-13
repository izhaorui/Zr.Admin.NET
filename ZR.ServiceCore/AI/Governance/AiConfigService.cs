using System.Text.Json;
using Infrastructure;
using Infrastructure.AI;
using Infrastructure.Attribute;
using Infrastructure.Model;
using ZR.Model.AI;
using ZR.Model.AI.Dto;
using ZR.ServiceCore.AI.IService;
// ServiceCore 全局引入了 Newtonsoft.Json，这里显式指向 System.Text.Json，避免 JsonSerializer/JsonException 二义性
using JsonDocument = System.Text.Json.JsonDocument;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ZR.ServiceCore.AI.Governance
{
    /// <summary>
    /// AI 运行时配置管理实现。
    /// <para>
    /// 写入流程：校验 JSON → 掩码密钥回填（避免前端把 ****xxxx 写回）→ 加密 → 按环境 upsert → 版本 +1 → 清缓存。
    /// 并发保护：前端传入 <see cref="AiConfigSaveDto.Version"/> 时做乐观锁校验，并用
    /// <c>WHERE Id=@id AND Version=@oldVersion</c> 条件更新堵住读改写窗口；首次插入撞唯一索引转为友好提示。
    /// </para>
    /// <para>
    /// 读取流程：库中值（掩码）→ 无则文件默认值（掩码），并附带“数据库覆盖是否生效”等状态。
    /// 取行规则与运行时完全一致（<see cref="AiConfigStore.PickEffectiveRow"/>，只认启用行）。
    /// </para>
    /// </summary>
    [AppService(ServiceType = typeof(IAiConfigService), ServiceLifetime = LifeTime.Transient)]
    public class AiConfigService : BaseService<AiConfig>, IAiConfigService
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
            // 回显/入库时省略 null 字段：避免 "BaseUrl": null 这类噪声让人误判配置来源
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public AiConfigCurrentDto GetCurrent(string section = AiOptionsResolver.SectionName)
        {
            section = NormalizeSection(section);
            var environment = AiConfigStore.ResolveEnvironment();
            var databaseEnabled = AiOptionsResolver.IsDatabaseEnabled(out var source);

            var dto = new AiConfigCurrentDto
            {
                Section = section,
                Environment = environment,
                Source = source,
                DatabaseOverrideEnabled = databaseEnabled
            };

            AiConfig row = null;
            try
            {
                row = FindRow(section, environment);
            }
            catch (Exception ex)
            {
                // 表未建 / 库不可用：仍返回文件配置，便于管理员先看默认值再决定是否建表
                dto.Warning = "AI 配置表不可用（可能尚未执行数据库同步）：" + ex.Message;
                Logger.Warn(ex, $"读取 AI 配置表失败 section={section} env={environment}");
            }

            dto.HasDbConfig = row != null;
            // 有库配置但策略是 file / auto 非生产时，运行时并不会读库，这里必须回显真实生效来源
            dto.EffectiveSource = databaseEnabled && row != null ? "ai_config" : "ai.json";
            dto.Version = row?.Version ?? 0;
            dto.UpdatedBy = row?.Update_by;
            dto.UpdatedTime = row?.Update_time;
            dto.Remark = row?.Remark;
            dto.Encrypted = row?.ConfigJson?.StartsWith("enc:v1:", StringComparison.Ordinal) == true;

            string rawJson;
            if (row != null)
            {
                try
                {
                    rawJson = AiConfigProtector.Unprotect(row.ConfigJson);
                }
                catch (Exception ex)
                {
                    dto.Warning = "库中配置无法解密（数据保护密钥可能已更换）：" + ex.Message;
                    Logger.Error(ex, $"解密 AI 配置失败 section={section} env={environment}");
                    rawJson = GetFileJson(section);
                }
            }
            else
            {
                // 无库配置时回显文件默认值，管理员可据此改为库配置
                rawJson = GetFileJson(section);
                if (rawJson == null)
                {
                    dto.Warning = $"该配置段（{section}）在库中不存在，且暂不支持从文件回显";
                }
            }

            // 一次性去掉注释与多余空白，保证回显内容可直接编辑后再提交（密钥一律掩码）
            dto.ConfigJson = rawJson == null ? null : AiConfigMasking.MaskSecrets(NormalizeJson(section, rawJson));
            return dto;
        }

        public long Save(AiConfigSaveDto input, string section = AiOptionsResolver.SectionName)
        {
            section = NormalizeSection(section);
            var json = input?.ConfigJson?.Trim();
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new CustomException("配置内容不能为空");
            }

            // 校验：合法 JSON；AiOptions 段额外要求能映射到配置结构（否则存进去会让整站 AI 不可用）
            ValidateJson(section, json);

            var environment = AiConfigStore.ResolveEnvironment();
            AiConfig row;
            try
            {
                row = FindRow(section, environment);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"读取 AI 配置表失败 section={section} env={environment}");
                throw new CustomException("AI 配置表不存在或不可用，请先执行数据库同步（--initdb / 后台数据库同步）");
            }

            // 掩码回填：前端回显的是 ****xxxx，直接保存会把真实密钥冲掉。
            // 库中无记录时，掩码来自“文件默认值”的回显，必须以文件配置作为回填来源，
            // 否则首次保存会把 ****xxxx 当成真实密钥写进库。
            var mergeSource = row != null ? SafeUnprotect(row.ConfigJson) : GetFileJson(section);
            if (!string.IsNullOrWhiteSpace(mergeSource))
            {
                json = AiConfigMasking.MergeMaskedSecrets(json, mergeSource);
            }

            // 回填后还留着 ****xxxx，说明这个掩码没能在原配置里找到对应项（数组项被重排/删除后按标识也匹配不上），
            // 此时把掩码当真实密钥存进库等于废掉该密钥，直接拒绝并让管理员重填
            var unresolvedMask = AiConfigMasking.FindUnresolvedMask(json);
            if (unresolvedMask != null)
            {
                Logger.Warn($"AI 配置掩码无法回填 section={section} field={unresolvedMask} operator={App.HttpContext?.GetName()}");
                throw new CustomException($"字段 {unresolvedMask} 仍是掩码（****xxxx），原密钥无法对应上（可能调整了数组顺序或删除了条目），请重新填写该密钥后再保存");
            }

            var normalized = NormalizeJson(section, json);
            // 变更摘要只记字段名（add/update/remove），不记值，避免密钥进日志
            var changeSummary = DescribeChanges(mergeSource, normalized, row == null);
            // 生产环境 fail-closed：整段配置含密钥，加密能力不可用时直接拒绝保存（Protect 内部还有一次兜底）
            AiConfigProtector.EnsureCanProtect("AI 配置");
            var protectedJson = AiConfigProtector.Protect(normalized);
            var operatorName = App.HttpContext?.GetName();
            var now = DateTime.Now;

            if (row == null)
            {
                var entity = new AiConfig
                {
                    Environment = environment,
                    Section = section,
                    ConfigJson = protectedJson,
                    Status = 0,
                    Version = 1,
                    Remark = input.Remark,
                    Create_by = operatorName,
                    Create_time = now
                };

                try
                {
                    entity.Id = Context.Insertable(entity).ExecuteReturnBigIdentity();
                }
                catch (Exception ex) when (IsUniqueViolation(ex))
                {
                    // 并发首次保存：另一个请求已插入同一 (Environment, Section)，撞 uk_ai_config_env_section
                    Logger.Warn(ex, $"AI 配置并发插入冲突 section={section} env={environment} operator={operatorName}");
                    throw new CustomException("该环境下的 AI 配置已被其他管理员创建，请刷新页面后重新编辑，避免覆盖他人改动");
                }
                catch (Exception ex) when (IsTruncation(ex))
                {
                    // 老库把 ConfigJson 建成 nvarchar(n)：加密后的配置必然超长，给出可执行的修复指引
                    Logger.Error(ex, $"AI 配置写入失败：ConfigJson 列长度不足 section={section} env={environment} operator={operatorName}");
                    throw new CustomException(ConfigJsonTruncationHint);
                }

                row = entity;
            }
            else
            {
                // 改的是 * 通用行：该行对所有环境生效，留痕便于排查“另一个环境为什么也变了”
                if (!string.Equals(row.Environment, environment, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Warn($"当前环境无专属配置，正在修改 * 通用行 section={section} env={environment} rowEnvironment={row.Environment} id={row.Id} operator={operatorName}");
                }

                // 乐观锁一：前端回显的版本与库中不一致，说明期间有人改过，直接拒绝
                if (input.Version.HasValue && input.Version.Value != row.Version)
                {
                    throw VersionConflict(section, environment, row.Version);
                }

                var expectedVersion = row.Version;
                var remark = string.IsNullOrWhiteSpace(input.Remark) ? row.Remark : input.Remark;

                // 乐观锁二：条件更新把“读”和“写”之间的窗口也堵住，
                // 两个请求同时通过上面的版本校验时，只有一个能更新成功
                var update = Context.Updateable<AiConfig>()
                    .SetColumns(x => x.ConfigJson == protectedJson)
                    .SetColumns(x => x.Version == expectedVersion + 1)
                    .SetColumns(x => x.Update_time == now);

                // 逐列 SetColumns 遇到 null 值在部分 SqlSugar 版本会被跳过，这里显式判空，
                // 避免把“无变更说明/无操作人”误判成“要写空值”
                if (!string.IsNullOrWhiteSpace(remark))
                {
                    update = update.SetColumns(x => x.Remark == remark);
                }
                if (!string.IsNullOrWhiteSpace(operatorName))
                {
                    update = update.SetColumns(x => x.Update_by == operatorName);
                }

                int affected;
                try
                {
                    affected = update
                        .Where(x => x.Id == row.Id && x.Version == expectedVersion)
                        .ExecuteCommand();
                }
                catch (Exception ex) when (IsTruncation(ex))
                {
                    Logger.Error(ex, $"AI 配置写入失败：ConfigJson 列长度不足 section={section} env={environment} operator={operatorName}");
                    throw new CustomException(ConfigJsonTruncationHint);
                }

                if (affected == 0)
                {
                    throw VersionConflict(section, environment, ReadVersion(row.Id, expectedVersion));
                }

                // 本地对象同步为写入后的状态（仅用于日志与返回，不回写库）
                row.ConfigJson = protectedJson;
                row.Version = expectedVersion + 1;
                row.Remark = remark;
                row.Update_by = operatorName;
                row.Update_time = now;
            }

            AiConfigStore.Invalidate();
            Logger.Info($"AI 配置已保存 section={section} env={environment} version={row.Version} operator={operatorName} changes={changeSummary} remark={input.Remark} encrypted={protectedJson.StartsWith("enc:v1:", StringComparison.Ordinal)}");
            return row.Id;
        }

        /// <summary>
        /// 手动清理本进程的 AI 配置缓存（配置表被直接改库、或多实例滚动发布后使用）。
        /// 多实例部署下各实例仍是各自 60 秒 TTL，本接口只影响当前进程。
        /// </summary>
        public int RefreshCache()
        {
            AiConfigStore.Invalidate();
            AiProviderStore.Invalidate();
            Logger.Info($"AI 配置缓存已手动刷新 env={AiConfigStore.ResolveEnvironment()} operator={App.HttpContext?.GetName()}");
            return 1;
        }

        public int Reset(string section = AiOptionsResolver.SectionName)
        {
            section = NormalizeSection(section);
            var environment = AiConfigStore.ResolveEnvironment();

            int affected;
            try
            {
                // 删除“当前环境实际生效的那一行”，而不是“环境名恰好等于本环境的那一行”：
                // 生效行可能是 * 通用行（本环境没保存过），只按 Environment==environment 删会出现
                // “点了恢复默认却什么都没发生、线上仍读库”的错觉。
                var row = FindRow(section, environment);
                if (row == null)
                {
                    Logger.Info($"AI 配置无需回退：库中无生效配置 section={section} env={environment}");
                    AiConfigStore.Invalidate();
                    return 0;
                }

                var isExactEnvironment = string.Equals(row.Environment, environment, StringComparison.OrdinalIgnoreCase);
                affected = Context.Deleteable<AiConfig>().Where(x => x.Id == row.Id).ExecuteCommand();

                if (!isExactEnvironment)
                {
                    // 通用行被所有环境共享：删除它等于让其它环境也回退到文件，必须留痕
                    Logger.Warn($"当前环境无专属配置，已删除生效的通用行（Environment={row.Environment} id={row.Id}）section={section} env={environment} affected={affected}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"删除 AI 配置失败 section={section} env={environment}");
                throw new CustomException("AI 配置表不存在或不可用，无法回退文件配置");
            }

            AiConfigStore.Invalidate();
            Logger.Info($"AI 配置已回退文件默认值 section={section} env={environment} affected={affected} operator={App.HttpContext?.GetName()}");
            return affected;
        }

        /// <summary>
        /// 当前环境的生效行：与运行时读取共用同一条规则（只认 Status=0，环境精确 → * 通用，无匹配返回 null）。
        /// <para>
        /// 不再退回“任意一行”：共享库里若只有别的环境保存过该段配置，管理端一旦取到那行，
        /// 保存会覆盖、回退会删除别的环境的记录。无匹配时按“当前环境无配置”处理，
        /// 保存走新建，回退不删任何行。
        /// </para>
        /// </summary>
        private AiConfig FindRow(string section, string environment)
            => AiConfigStore.PickEffectiveRow(AiConfigStore.QueryEnabledRows(Context, section), environment);

        /// <summary>版本冲突提示：告知当前版本，便于前端提示“刷新后重试”。</summary>
        private static CustomException VersionConflict(string section, string environment, long currentVersion)
            => new(ResultCode.CUSTOM_ERROR,
                $"配置已被其他管理员修改（{section}/{environment} 当前版本 {currentVersion}），请刷新页面后重新编辑，避免覆盖他人改动",
                notice: false);

        /// <summary>冲突后读回库中真实版本（仅用于提示文案，失败时退回已知版本）。</summary>
        private long ReadVersion(long id, long fallback)
        {
            try
            {
                return Context.Queryable<AiConfig>().InSingle(id)?.Version ?? fallback;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"读取 AI 配置最新版本失败 id={id}");
                return fallback;
            }
        }

        /// <summary>
        /// ConfigJson 列长度不足（老库建成 nvarchar(n)）时的修复指引：
        /// 加密后的整段配置约 2~3KB，直接写会抛 SQL Server 2628 / MySQL 1406。
        /// </summary>
        private const string ConfigJsonTruncationHint =
            "AI 配置保存失败：ai_config.ConfigJson 列长度不足以存放加密后的配置（写入被截断）。" +
            "请执行 ALTER TABLE ai_config ALTER COLUMN ConfigJson nvarchar(max) NULL 后重试；" +
            "或重启时带 --initdb / 在后台「数据库同步」执行一次结构同步，会自动把该列加宽为长文本";

        /// <summary>
        /// 是否“字符串超长被截断”（SQL Server 2628 / MySQL 1406）。命中后转成带修复 SQL 的提示，
        /// 避免管理员只看到一句数据库原始错误无从下手。
        /// </summary>
        private static bool IsTruncation(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                var message = current.Message ?? string.Empty;
                if (message.Contains("truncated", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("Data too long", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("2628")
                    || message.Contains("1406"))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 是否唯一索引冲突（uk_ai_config_env_section / 主键）。不同数据库错误码与文案不同，
        /// 这里按常见关键字判断，命中后转成“已被他人创建”的友好提示，而不是 500。
        /// </summary>
        private static bool IsUniqueViolation(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                var message = current.Message ?? string.Empty;
                if (message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("2627")   // SQL Server：违反唯一约束
                    || message.Contains("2601")   // SQL Server：违反唯一索引
                    || message.Contains("1062")   // MySQL：Duplicate entry
                    || message.Contains("重复"))
                {
                    return true;
                }
            }
            return false;
        }

        private static string SafeUnprotect(string stored)
        {
            try
            {
                return AiConfigProtector.Unprotect(stored);
            }
            catch (Exception ex)
            {
                // 解密失败时不阻断保存：仅意味着无法回填掩码密钥，日志留痕
                Logger.Warn(ex, "回填掩码密钥时解密旧配置失败，本次将按提交内容保存");
                return null;
            }
        }

        /// <summary>配置段校验：AiOptions 需能映射到结构；其它段只要求是合法 JSON 对象。</summary>
        private static void ValidateJson(string section, string json)
        {
            if (IsAiOptions(section))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<AiOptions>(json, JsonOptions);
                    if (parsed == null) throw new CustomException("配置 JSON 不能为空对象");
                }
                catch (JsonException ex)
                {
                    throw new CustomException($"配置 JSON 格式不正确：{ex.Message}");
                }
                return;
            }

            try
            {
                using var _ = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new CustomException($"配置 JSON 格式不正确：{ex.Message}");
            }
        }

        /// <summary>反序列化再序列化：统一字段顺序/缩进，并剔除注释与多余空白。</summary>
        private static string NormalizeJson(string section, string json)
        {
            try
            {
                if (!IsAiOptions(section))
                {
                    using var doc = JsonDocument.Parse(json);
                    return JsonSerializer.Serialize(doc.RootElement, JsonOptions);
                }

                var parsed = JsonSerializer.Deserialize<AiOptions>(json, JsonOptions) ?? new AiOptions();
                // 渠道已独立成 ai_provider 表：全局配置不再承载 Providers，
                // 避免“JSON 里改了渠道、实际读的却是表”的双写歧义。
                parsed.Providers = null;
                return JsonSerializer.Serialize(parsed, JsonOptions);
            }
            catch (JsonException)
            {
                return json;
            }
        }

        /// <summary>文件兜底 JSON：目前仅 AiOptions 有强类型回显；其它段返回 null。</summary>
        private static string GetFileJson(string section)
        {
            if (!IsAiOptions(section)) return null;

            var options = AiOptionsResolver.GetFromFile();
            // 渠道只在 ai_provider 维护，回显时不展示文件里的 Providers（避免管理员误以为可在此改）
            options.Providers = null;
            return JsonSerializer.Serialize(options, JsonOptions);
        }

        private static bool IsAiOptions(string section)
            => string.Equals(section, AiOptionsResolver.SectionName, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 配置变更摘要：只输出变化的字段名与操作类型，绝不输出字段值（含密钥）。
        /// </summary>
        private static string DescribeChanges(string beforeJson, string afterJson, bool isNew)
        {
            if (isNew) return "首次创建";
            if (string.IsNullOrWhiteSpace(beforeJson)) return "旧配置不可读";
            try
            {
                using var before = JsonDocument.Parse(beforeJson);
                using var after = JsonDocument.Parse(afterJson);
                if (before.RootElement.ValueKind != JsonValueKind.Object || after.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return "非对象配置";
                }

                var changed = new List<string>();
                foreach (var property in after.RootElement.EnumerateObject())
                {
                    if (!before.RootElement.TryGetProperty(property.Name, out var oldValue))
                    {
                        changed.Add(property.Name + ":add");
                    }
                    else if (!string.Equals(oldValue.GetRawText(), property.Value.GetRawText(), StringComparison.Ordinal))
                    {
                        changed.Add(property.Name + ":update");
                    }
                }
                foreach (var property in before.RootElement.EnumerateObject())
                {
                    if (!after.RootElement.TryGetProperty(property.Name, out _))
                    {
                        changed.Add(property.Name + ":remove");
                    }
                }

                if (changed.Count == 0) return "无字段变化";
                return string.Join(",", changed.Take(20))
                    + (changed.Count > 20 ? $",...共{changed.Count}项" : string.Empty);
            }
            catch (Exception)
            {
                return "无法比较";
            }
        }

        private static string NormalizeSection(string section)
            => string.IsNullOrWhiteSpace(section) ? AiOptionsResolver.SectionName : section.Trim();
    }
}
