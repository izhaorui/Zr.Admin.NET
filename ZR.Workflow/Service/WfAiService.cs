using Infrastructure.Helper;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;
using STJson = System.Text.Json;

namespace ZR.Workflow.Service
{
    /// <summary>
    /// 工作流 AI 生成服务：自然语言 → 结构化流程草稿。
    /// 仅做 Prompt 编排、LLM 调用、JSON 解析与业务校验，不直接落库。
    /// </summary>
    [AppService(ServiceType = typeof(IWfAiService))]
    public class WfAiService : IWfAiService
    {
        private static readonly ILogger<WfAiService> Logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<WfAiService>();
        private readonly IWfFlowDefinitionService _definitionService;

        public WfAiService(IWfFlowDefinitionService definitionService)
        {
            _definitionService = definitionService;
        }

        // 允许的节点类型，复用 ZR.Workflow.Enum.WfNodeType
        private static readonly HashSet<int> ValidNodeTypes = new()
        {
            (int)WfNodeType.Audit, (int)WfNodeType.Cc, (int)WfNodeType.Condition,
            (int)WfNodeType.ParallelFork, (int)WfNodeType.ParallelJoin
        };
        // 条件运算符，复用 ZR.Workflow.Enum.WfConditionOp
        private static readonly HashSet<int> ValidOps = new()
        {
            (int)WfConditionOp.None, (int)WfConditionOp.Lt, (int)WfConditionOp.Le,
            (int)WfConditionOp.Gt, (int)WfConditionOp.Ge, (int)WfConditionOp.Eq, (int)WfConditionOp.Ne
        };
        private static readonly STJson.JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // 提示词加载器：从磁盘 Prompts 目录读取 .md（目录可经 AiOptions.PromptDir 配置）。
        // 文件缺失不抛异常、不阻断编译，仅在对应能力调用时通过 GetPromptOrThrow 抛友好提示。
        private static readonly PromptLoader PromptLoader =
            new(AppSettings.Get<AiOptions>("AiOptions")?.PromptDir);

        /// <summary>
        /// 读取提示词，缺失时抛出友好异常（含文件名与目录，便于运维补文件）。
        /// </summary>
        private static string GetPromptOrThrow(string fileName, string capability)
        {
            var text = PromptLoader.Load(fileName);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new Exception($"AI 能力「{capability}」所需提示词文件缺失：{fileName}（请检查 AiOptions:PromptDir 指向的 Prompts 目录）");
            }
            return text;
        }

        /// <summary>
        /// 构建 validateWorkflow 工具的 function schema（OpenAI 兼容）。
        /// </summary>
        private static object BuildValidateToolSchema()
        {
            return new
            {
                type = "function",
                function = new
                {
                    name = "validateWorkflow",
                    description = "校验工作流草稿是否符合结构约束：节点类型合法、连线端点存在、条件字段归属表单、条件网关出边与条件齐备、并行分组一致。errors 为空表示通过。",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            nodes = new
                            {
                                type = "array",
                                description = "节点数组，按 index 顺序，连线用 sourceIndex/targetIndex 引用",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        nodeType = new { type = "integer", description = "节点类型：1审批 2抄送 4条件网关 7并行分叉 8并行汇聚" },
                                        nodeName = new { type = "string", description = "节点中文名" },
                                        approverType = new { type = "integer", description = "审批人类型：0指定用户 4部门负责人 5发起人主管" },
                                        approverIds = new { type = "string", description = "userId 逗号串，approverType=0 时使用" },
                                        approverNames = new { type = "string", description = "审批人姓名快照逗号串" },
                                        signType = new { type = "integer", description = "0或签 1会签，仅审批节点" },
                                        parallelGroup = new { type = "integer", description = "并行分组号，分叉/汇聚须一致" }
                                    },
                                    required = new[] { "nodeType", "nodeName" }
                                }
                            },
                            links = new
                            {
                                type = "array",
                                description = "连线数组",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        sourceIndex = new { type = "integer", description = "源节点下标" },
                                        targetIndex = new { type = "integer", description = "目标节点下标" },
                                        field = new { type = "string", description = "条件字段key，须存在于 formItems.field；默认分支填空" },
                                        op = new { type = "integer", description = "运算符：0无 1小于 2小于等于 3大于 4大于等于 5等于 6不等于" },
                                        value = new { type = "string", description = "条件比较值字符串" }
                                    },
                                    required = new[] { "sourceIndex", "targetIndex" }
                                }
                            },
                            formItems = new
                            {
                                type = "array",
                                description = "表单字段",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        field = new { type = "string", description = "字段key(英文驼峰)" },
                                        label = new { type = "string", description = "展示名" },
                                        type = new { type = "string", description = "input/textarea/number/date/datetime/select/radio/switch/image" },
                                        required = new { type = "boolean", description = "是否必填" },
                                        options = new { type = "string", description = "选项逗号串" }
                                    },
                                    required = new[] { "field", "label", "type", "required" }
                                }
                            }
                        },
                        required = new[] { "nodes", "links", "formItems" }
                    }
                }
            };
        }

        /// <summary>
        /// 连线是否携带条件（field/op/value 任一非空）。
        /// </summary>
        private static bool HasCondition(WfAiLinkDto link)
        {
            return !string.IsNullOrWhiteSpace(link.Field) || link.Op != 0 || !string.IsNullOrWhiteSpace(link.Value);
        }

        /// <summary>
        /// Skill 编排中模型最多自我修正轮次（每轮 = 生成/修正 + 调用 validateWorkflow 自检）。
        /// </summary>
        private const int MaxRepairRounds = 3;

        /// <summary>
        /// validateWorkflow 工具描述（OpenAI 兼容 function schema），参数即流程草稿结构。
        /// 模型生成 / 修正后调用本工具进行结构化自检，errors 为空表示通过。
        /// </summary>
        private static readonly object ValidateToolSchema = BuildValidateToolSchema();

        public async Task<WfAiGenerateResultDto> GenerateFlowAsync(WfAiGenerateInput input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.Description))
            {
                throw new Exception("流程描述不能为空");
            }

            var options = EnsureAiEnabled();
            var description = input.Description.Trim();

            // 消息数组（由本服务持有维护）：system + user + 后续的 assistant(tool_calls) / tool 回灌
            var messages = new List<object>
            {
                new { role = "system", content = GetPromptOrThrow("flow-generate.md", "流程生成") },
                new { role = "user", content = description }
            };
            var tools = new object[] { ValidateToolSchema };

            string lastJson = null;
            var requestId = Guid.NewGuid().ToString("N");

            for (var round = 0; round <= MaxRepairRounds; round++)
            {
                AiLlmClient.ChatToolResult turn;
                try
                {
                    turn = await AiLlmClient.ChatWithToolsAsync(options, messages.ToArray(), tools).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    throw new Exception("调用 AI 服务失败：" + ex.Message);
                }
                catch (TaskCanceledException)
                {
                    throw new Exception("调用 AI 服务超时，请稍后重试");
                }

                // 模型请求调用 validateWorkflow：执行本地校验并回灌结果，进入下一轮修正
                if (turn.ToolCalls.Count > 0)
                {
                    foreach (var call in turn.ToolCalls)
                    {
                        if (!string.Equals(call.Name, "validateWorkflow", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var errors = RunValidateTool(call.Arguments, out var parsedJson);
                        if (!string.IsNullOrWhiteSpace(parsedJson))
                        {
                            lastJson = parsedJson;
                        }

                        // 保留 assistant 消息（含 tool_calls）以便模型上下文连续，再以 role=tool 回灌校验结果
                        var assistantMsg = BuildAssistantToolMessage(turn.Content, call);
                        messages.Add(assistantMsg);
                        messages.Add(new
                        {
                            role = "tool",
                            tool_call_id = call.Id,
                            name = call.Name,
                            content = STJson.JsonSerializer.Serialize(new { errors })
                        });

                        LogValidateErrors(requestId, round, errors);
                    }

                    continue;
                }

                // 模型未调用工具，视为给出最终结果（可能是通过后的 JSON 文本）
                lastJson = turn.Content;
                break;
            }

            return FinalizeResult(requestId, lastJson);
        }

        /// <summary>
        /// 校验结果日志：通过打绿色提示，未通过打黄色汇总 + 逐条结构化 Warning。
        /// </summary>
        private void LogValidateErrors(string requestId, int round, List<string> errors)
        {
            if (errors.Count == 0)
            {
                Log.WriteLine(ConsoleColor.Green, $"[WfAi] 第 {round} 轮 validateWorkflow 校验通过");
                return;
            }

            Log.WriteLine(ConsoleColor.Yellow, $"[WfAi] 第 {round} 轮校验未通过：{errors.Count} 条错误，回灌模型修正");
            foreach (var e in errors)
            {
                Logger.LogWarning("[WfAi][{RequestId}] 第 {Round} 轮校验错误：{Error}", requestId, round, e);
            }
        }

        /// <summary>
        /// Skill 闭环结束后的收尾：先直接反序列化（要求 AI 严格返回纯 JSON），
        /// 失败则 ExtractFirstJsonObject 兜底，再失败抛异常。
        /// </summary>
        private WfAiGenerateResultDto FinalizeResult(string requestId, string lastJson)
        {
            if (string.IsNullOrWhiteSpace(lastJson))
            {
                throw new Exception("AI 未返回流程草稿，请重新描述或稍后重试");
            }

            var result = TryDeserializeDirect(lastJson);
            if (result == null)
            {
                var extracted = JsonHelper.ExtractFirstJsonObject(lastJson);
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    result = TryDeserializeDirect(extracted);
                }
            }

            if (result == null)
            {
                Log.WriteLine(ConsoleColor.Red, "[WfAi] AI 输出无法解析为合法 JSON 流程结构");
                Logger.LogWarning("[WfAi][{RequestId}] 原始输出：{Raw}", requestId, lastJson);
                throw new Exception("AI 输出格式错误，请重新描述或稍后重试");
            }

            NormalizeResult(result);
            var errors = ValidateWorkflow(result);
            if (errors.Count > 0)
            {
                Log.WriteLine(ConsoleColor.Red, $"[WfAi] Skill 闭环后仍校验失败：{errors[0]}");
                Logger.LogWarning("[WfAi][{RequestId}] 解析后 JSON：{Json}", requestId, STJson.JsonSerializer.Serialize(result));
                throw new Exception(errors[0]);
            }

            Logger.LogInformation("[WfAi] Skill 闭环后校验通过，节点数={NodesCount}, 连线数={LinksCount}, 表单字段数={FormItemsCount}", result.Nodes.Count, result.Links.Count, result.FormItems.Count);
            Log.WriteLine(ConsoleColor.Green, "[WfAi] 生成校验通过");
            return result;
        }

        /// <summary>
        /// 直接反序列化：成功返回对象，失败返回 null（不抛异常，由调用方决定兜底策略）。
        /// 先尝试标准反序列化，若模型用 sourceNodeId 代替 sourceIndex 则回退到 ParseLenient。
        /// </summary>
        private static WfAiGenerateResultDto TryDeserializeDirect(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                var result = STJson.JsonSerializer.Deserialize<WfAiGenerateResultDto>(json, JsonOptions);
                if (result != null) return result;
            }
            catch (STJson.JsonException)
            {
                // 模型偶发用 sourceNodeId/targetNodeId 代替 sourceIndex/targetIndex，
                // 反序列化整数端点会失败；用 JsonDocument 手动解析并归一化端点。
                try
                {
                    return ParseLenient(json);
                }
                catch (Exception lenientEx)
                {
                    Logger.LogDebug(
                        lenientEx,
                        "[WfAi] 宽松解析失败，原始 JSON 解析异常：{Message}",
                        lenientEx.Message);

                    return null;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// 执行 validateWorkflow 工具：解析参数草稿并本地校验，返回错误列表；parsedJson 携带成功解析的草稿。
        /// </summary>
        private static List<string> RunValidateTool(string arguments, out string parsedJson)
        {
            parsedJson = null;
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return new List<string> { "validateWorkflow 调用缺少参数" };
            }

            WfAiGenerateResultDto draft;
            try
            {
                draft = STJson.JsonSerializer.Deserialize<WfAiGenerateResultDto>(arguments, JsonOptions);
            }
            catch (STJson.JsonException ex)
            {
                return new List<string> { "validateWorkflow 参数无法解析为流程结构：" + ex.Message };
            }

            if (draft == null)
            {
                return new List<string> { "validateWorkflow 参数为空结构" };
            }

            NormalizeResult(draft);
            parsedJson = STJson.JsonSerializer.Serialize(draft);
            return ValidateWorkflow(draft);
        }

        /// <summary>
        /// 构造携带 tool_calls 的 assistant 消息，保留模型请求上下文以便后续回灌 tool 结果。
        /// </summary>
        private static object BuildAssistantToolMessage(string content, AiLlmClient.ToolCall call)
        {
            return new
            {
                role = "assistant",
                content = content,
                tool_calls = new[]
                {
                    new
                    {
                        id = call.Id,
                        type = "function",
                        function = new { name = call.Name, arguments = call.Arguments }
                    }
                }
            };
        }

        /// <summary>
        /// 容错解析：当模型未严格遵守 sourceIndex/targetIndex 整数约定（如用了 sourceNodeId:"n_3" /
        /// targetNodeId / nodeId 字符串）时，从原始 JSON 手动抽取并归一化端点为数组下标。
        /// </summary>
        private static WfAiGenerateResultDto ParseLenient(string json)
        {
            using var doc = STJson.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var result = new WfAiGenerateResultDto();

            if (root.TryGetProperty("nodes", out var nodesEl) && nodesEl.ValueKind == STJson.JsonValueKind.Array)
            {
                foreach (var n in nodesEl.EnumerateArray())
                {
                    var node = new WfAiNodeDto();
                    if (n.TryGetProperty("nodeType", out var nt)) node.NodeType = nt.GetInt32();
                    if (n.TryGetProperty("nodeName", out var nn)) node.NodeName = nn.GetString();
                    if (n.TryGetProperty("approverType", out var at) && at.ValueKind == STJson.JsonValueKind.Number) node.ApproverType = at.GetInt32();
                    if (n.TryGetProperty("approverIds", out var aids)) node.ApproverIds = aids.GetString() ?? string.Empty;
                    if (n.TryGetProperty("approverNames", out var an)) node.ApproverNames = an.GetString() ?? string.Empty;
                    if (n.TryGetProperty("signType", out var st) && st.ValueKind == STJson.JsonValueKind.Number) node.SignType = st.GetInt32();
                    if (n.TryGetProperty("parallelGroup", out var pg)) node.ParallelGroup = pg.GetInt32();
                    result.Nodes.Add(node);
                }
            }

            if (root.TryGetProperty("links", out var linksEl) && linksEl.ValueKind == STJson.JsonValueKind.Array)
            {
                foreach (var l in linksEl.EnumerateArray())
                {
                    var link = new WfAiLinkDto();
                    link.SourceIndex = ParseIndex(l, "sourceIndex", "sourceNodeId");
                    link.TargetIndex = ParseIndex(l, "targetIndex", "targetNodeId");
                    if (l.TryGetProperty("field", out var f)) link.Field = f.GetString() ?? string.Empty;
                    if (l.TryGetProperty("op", out var o) && o.ValueKind == STJson.JsonValueKind.Number) link.Op = o.GetInt32();
                    if (l.TryGetProperty("value", out var v)) link.Value = v.GetString() ?? string.Empty;
                    result.Links.Add(link);
                }
            }

            if (root.TryGetProperty("formItems", out var formsEl) && formsEl.ValueKind == STJson.JsonValueKind.Array)
            {
                foreach (var f in formsEl.EnumerateArray())
                {
                    var field = new WfAiFormFieldDto();
                    if (f.TryGetProperty("field", out var ff)) field.Field = ff.GetString() ?? string.Empty;
                    if (f.TryGetProperty("label", out var fl)) field.Label = fl.GetString() ?? string.Empty;
                    if (f.TryGetProperty("type", out var ft)) field.Type = ft.GetString() ?? "input";
                    if (f.TryGetProperty("required", out var fr) && fr.ValueKind == STJson.JsonValueKind.True) field.Required = true;
                    if (f.TryGetProperty("options", out var fo)) field.Options = fo.GetString() ?? string.Empty;
                    result.FormItems.Add(field);
                }
            }

            return result;
        }

        /// <summary>
        /// 从 link 元素读取端点下标：优先 sourceIndex（整数或数字字符串）；否则尝试 sourceNodeId（如 "n_3" 或纯数字串），
        /// 取末尾整数作为数组下标。无法解析时返回 -1（交由 ValidateWorkflow 报端点越界）。
        /// </summary>
        private static int ParseIndex(STJson.JsonElement link, string primary, string alt)
        {
            if (link.TryGetProperty(primary, out var p))
            {
                if (p.ValueKind == STJson.JsonValueKind.Number) return p.GetInt32();
                if (p.ValueKind == STJson.JsonValueKind.String && int.TryParse(p.GetString(), out var iv)) return iv;
            }
            if (link.TryGetProperty(alt, out var a) && a.ValueKind == STJson.JsonValueKind.String)
            {
                var s = a.GetString() ?? string.Empty;
                var digits = new string(s.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
                if (digits.Length > 0 && int.TryParse(digits, out var idx)) return idx;
            }
            return -1;
        }

        /// <summary>
        /// 由合法连线（端点均在节点数组范围内）计算各节点的入度/出度。
        /// ValidateWorkflow 与 NormalizeResult 共用，避免重复实现导致累加方向不一致。
        /// </summary>
        private static (int[] InDeg, int[] OutDeg) ComputeDegrees(List<WfAiNodeDto> nodes, List<WfAiLinkDto> links)
        {
            var inDeg = new int[nodes.Count];
            var outDeg = new int[nodes.Count];
            foreach (var l in links)
            {
                if (l.TargetIndex >= 0 && l.TargetIndex < nodes.Count) inDeg[l.TargetIndex]++;
                if (l.SourceIndex >= 0 && l.SourceIndex < nodes.Count) outDeg[l.SourceIndex]++;
            }
            return (inDeg, outDeg);
        }

        /// <summary>
        /// 业务校验（作为 validateWorkflow 工具的本地实现）：返回错误列表。
        /// 空列表表示校验通过。供 Skill 编排中模型自纠回调，以及兜底校验共用。
        /// </summary>
        private static List<string> ValidateWorkflow(WfAiGenerateResultDto result)
        {
            var errors = new List<string>();
            var nodes = result.Nodes ?? new List<WfAiNodeDto>();
            if (nodes.Count == 0)
            {
                errors.Add("未生成任何流程节点");
                return errors;
            }

            for (var i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (!ValidNodeTypes.Contains(n.NodeType))
                {
                    errors.Add($"节点[{i}] nodeType={n.NodeType} 非法，仅支持 1审批/2抄送/4条件网关/7并行分叉/8并行汇聚");
                }
                if (string.IsNullOrWhiteSpace(n.NodeName))
                {
                    errors.Add($"节点[{i}] 缺少 nodeName");
                }
            }

            var formFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in result.FormItems ?? new List<WfAiFormFieldDto>())
            {
                if (!string.IsNullOrWhiteSpace(f.Field)) formFields.Add(f.Field.Trim());
            }

            var links = result.Links ?? new List<WfAiLinkDto>();
            var outsBySource = new Dictionary<int, List<WfAiLinkDto>>();
            foreach (var l in links)
            {
                if (l.SourceIndex < 0 || l.SourceIndex >= nodes.Count || l.TargetIndex < 0 || l.TargetIndex >= nodes.Count)
                {
                    var nodeList = string.Join(", ", nodes.Select((n, i) => $"{i}={n.NodeName}"));
                    errors.Add($"连线 sourceIndex={l.SourceIndex}/targetIndex={l.TargetIndex} 超出节点数组范围(0~{nodes.Count - 1})。当前节点清单(下标=名称)：[{nodeList}]，请按此清单把越界的端点改为正确下标");
                    continue;
                }

                if (!outsBySource.TryGetValue(l.SourceIndex, out var outs))
                {
                    outs = new List<WfAiLinkDto>();
                    outsBySource[l.SourceIndex] = outs;
                }
                outs.Add(l);

                if (HasCondition(l))
                {
                    if (!ValidOps.Contains(l.Op))
                    {
                        errors.Add($"连线条件运算符 op={l.Op} 非法");
                    }
                    else if (string.IsNullOrWhiteSpace(l.Field) || !formFields.Contains(l.Field.Trim()))
                    {
                        errors.Add($"连线条件字段 '{l.Field}' 不在表单字段(formItems)中");
                    }
                }
            }

            // 条件网关必须有 ≥2 出边且 ≥1 条带条件
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].NodeType != (int)WfNodeType.Condition) continue;
                if (!outsBySource.TryGetValue(i, out var outs))
                {
                    outs = new List<WfAiLinkDto>();
                }

                if (outs.Count < 2)
                {
                    errors.Add($"条件网关节点[{nodes[i].NodeName}] 出边不足 2 条，请完善流程描述后重试");
                }
                else if (!outs.Any(HasCondition))
                {
                    errors.Add($"条件网关节点[{nodes[i].NodeName}] 未生成任何带条件的出边（AI 可能漏填），请在描述中明确分支条件或重新生成");
                }
                // 默认分支（field/op/value 全空）非强制，但至多一条（描述明确有兜底语义时才生成）
                var defaultBranches = outs.Where(l => !HasCondition(l)).ToList();
                if (defaultBranches.Count > 1)
                {
                    errors.Add($"条件网关节点[{nodes[i].NodeName}] 默认分支多于 1 条，请只保留一条（描述明确有「否则/兜底」语义时才该生成）");
                }
            }

            // 条件分支后普通审批/抄送节点不得被多分支共享（避免布局交叉与运行态歧义）。
            // 仅允许无出边的「终点节点」（如最终统一抄送）被多分支汇入。
            // 仅统计已通过端点范围检查的连线（outsBySource 内的 link），避免越界端点直接抛 IndexOutOfRange。
            var (inDegree, outDegree) = ComputeDegrees(nodes, links);
            for (var i = 0; i < nodes.Count; i++)
            {
                var t = nodes[i].NodeType;
                // 仅审批节点(1)受非法共享约束：被多分支指向且有后续会引发布局交叉/运行态歧义。
                // 抄送节点(2)天然允许多分支汇入（同一人被多分支抄送是正常语义），不视为非法。
                if (t != (int)WfNodeType.Audit) continue;
                if (inDegree[i] <= 1) continue;
                if (outDegree[i] == 0) continue; // 多个分支最终汇入同一抄送/归档是允许的
                errors.Add($"节点[{nodes[i].NodeName}] 被多个分支同时指向且仍有后续节点，属于条件分支间非法共享节点。请将各金额段/条件分支中的同名审批节点拆分为独立节点（每分支都有自己的「{nodes[i].NodeName}」），再重新生成");
            }

            return errors;
        }

        /// <summary>
        /// 剥离可能的 ```json ``` 代码块包裹，并提取首个 JSON 对象。
        /// </summary>
        private static void NormalizeResult(WfAiGenerateResultDto result)
        {
            result.Nodes ??= new List<WfAiNodeDto>();
            result.Links ??= new List<WfAiLinkDto>();
            result.FormItems ??= new List<WfAiFormFieldDto>();

            NormalizeNodes(result.Nodes);
            NormalizeLinks(result.Links);
            NormalizeFormItems(result.FormItems);

            // 三个兜底（顺序有关：先拆分共享节点 → 再清越界连线 → 最后补缺失条件字段）
            SplitSharedNodes(result);
            DropOutOfRangeLinks(result);
            EnsureConditionFields(result);
        }

        private static void NormalizeNodes(List<WfAiNodeDto> nodes)
        {
            foreach (var n in nodes)
            {
                n.NodeName = (n.NodeName ?? string.Empty).Trim();
                n.ApproverIds = (n.ApproverIds ?? string.Empty).Trim();
                n.ApproverNames = (n.ApproverNames ?? string.Empty).Trim();

                if ((n.NodeType == (int)WfNodeType.Audit || n.NodeType == (int)WfNodeType.Cc) && !n.ApproverType.HasValue)
                {
                    n.ApproverType = (int)WfApproverType.DeptLeader;
                }

                if (n.NodeType == (int)WfNodeType.Audit && !n.SignType.HasValue)
                {
                    n.SignType = (int)WfSignType.Or;
                }

                // 并行分叉(7)/汇聚(8) 的 parallelGroup 原样保留。
                // 并行成员（审批1/抄送2 带 parallelGroup>0）也须保留：前端 buildLayout 靠 parallelGroup
                // 把同组节点聚合成并行行，若这里把成员清成 0，前端会漏聚合导致并行渲染错乱。
                // 只有「无并行语义」（非 7/8 且非并行成员）的节点才清 0。
                var isParallelNode = n.NodeType == (int)WfNodeType.ParallelFork || n.NodeType == (int)WfNodeType.ParallelJoin;
                var isParallelMember = n.ParallelGroup > 0;
                if (!isParallelNode && !isParallelMember)
                {
                    n.ParallelGroup = 0;
                }
            }
        }

        private static void NormalizeLinks(List<WfAiLinkDto> links)
        {
            foreach (var l in links)
            {
                l.Field = (l.Field ?? string.Empty).Trim();
                l.Value = (l.Value ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(l.Field) && string.IsNullOrWhiteSpace(l.Value))
                {
                    l.Op = 0;
                }
            }
        }

        private static void NormalizeFormItems(List<WfAiFormFieldDto> formItems)
        {
            foreach (var f in formItems)
            {
                f.Field = (f.Field ?? string.Empty).Trim();
                f.Label = (f.Label ?? string.Empty).Trim();
                f.Type = string.IsNullOrWhiteSpace(f.Type) ? "input" : f.Type.Trim().ToLowerInvariant();
                f.Options = (f.Options ?? string.Empty).Trim();
            }
        }

        /// <summary>
        /// 兜底：自动拆分条件分支间非法共享的审批节点（入度>1 且有出边）。
        /// 仅审批节点(1)需拆分（抄送节点天然允许多分支汇入）；并行分叉(7)/汇聚(8)不在此处理。
        /// </summary>
        private static void SplitSharedNodes(WfAiGenerateResultDto result)
        {
            var splitGuard = 0;
            bool needSplit;
            do
            {
                needSplit = false;
                var (inDeg, outDeg) = ComputeDegrees(result.Nodes, result.Links);
                for (var i = 0; i < result.Nodes.Count; i++)
                {
                    if (result.Nodes[i].NodeType != (int)WfNodeType.Audit) continue;
                    if (inDeg[i] <= 1) continue;
                    if (outDeg[i] == 0) continue;   // 无后续的统一终点允许共享

                    var inbound = result.Links.Where(l => l.TargetIndex == i).ToList();
                    for (var k = 1; k < inbound.Count; k++)
                    {
                        var source = result.Nodes[i];
                        var clone = new WfAiNodeDto
                        {
                            NodeType = source.NodeType,
                            NodeName = source.NodeName,
                            ApproverType = source.ApproverType,
                            ApproverIds = source.ApproverIds,
                            ApproverNames = source.ApproverNames,
                            SignType = source.SignType,
                            ParallelGroup = source.ParallelGroup
                        };
                        result.Nodes.Add(clone);
                        var cloneIdx = result.Nodes.Count - 1;
                        inbound[k].TargetIndex = cloneIdx;

                        foreach (var oe in result.Links.Where(l => l.SourceIndex == i).ToList())
                        {
                            if (oe.TargetIndex < 0 || oe.TargetIndex >= result.Nodes.Count) continue; // 不复制越界出边
                            result.Links.Add(new WfAiLinkDto
                            {
                                SourceIndex = cloneIdx,
                                TargetIndex = oe.TargetIndex,
                                Field = oe.Field,
                                Op = oe.Op,
                                Value = oe.Value
                            });
                        }
                        Log.WriteLine(ConsoleColor.DarkYellow, $"[WfAi] 已自动拆分共享节点[{source.NodeName}]（原下标={i}→副本下标={cloneIdx}）");
                    }
                    needSplit = true;
                    break; // 重算入度后再循环处理其余共享节点
                }
            } while (needSplit && ++splitGuard < 200);
        }

        /// <summary>
        /// 兜底：清理端点越界的连线（AI 偶发数错 nodes 下标）。丢弃越界连线并记日志。
        /// </summary>
        private static void DropOutOfRangeLinks(WfAiGenerateResultDto result)
        {
            var nodeCount = result.Nodes.Count;
            var removed = result.Links
                .Where(l => l.SourceIndex < 0 || l.SourceIndex >= nodeCount || l.TargetIndex < 0 || l.TargetIndex >= nodeCount)
                .ToList();
            if (removed.Count == 0) return;

            result.Links = result.Links.Except(removed).ToList();
            foreach (var l in removed)
            {
                Log.WriteLine(ConsoleColor.DarkYellow, $"[WfAi] 已忽略越界连线 sourceIndex={l.SourceIndex}/targetIndex={l.TargetIndex}（节点数={nodeCount}）");
            }
        }

        /// <summary>
        /// 兜底：连线条件字段(Field)若在 formItems 中缺失，自动补一个 number 类型字段。
        /// </summary>
        private static void EnsureConditionFields(WfAiGenerateResultDto result)
        {
            if (result.FormItems == null) result.FormItems = new List<WfAiFormFieldDto>();
            var existFields = new HashSet<string>(result.FormItems.Select(f => (f.Field ?? string.Empty).Trim()), StringComparer.OrdinalIgnoreCase);
            var usedFields = result.Links
                .Select(l => (l.Field ?? string.Empty).Trim())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var f in usedFields)
            {
                if (existFields.Contains(f)) continue;
                result.FormItems.Add(new WfAiFormFieldDto
                {
                    Field = f,
                    Label = f,
                    Type = "number",
                    Required = true,
                    Options = string.Empty
                });
                Log.WriteLine(ConsoleColor.DarkYellow, $"[WfAi] 已自动补条件字段[{f}]（AI 未提供 formItems，默认 number/必填）");
            }
        }

        #region AI 扩展能力（审批意见 / 流程体检 / 自然语言填单）

        /// <summary>
        /// 统一校验 AI 开关与 ApiKey，未启用抛友好异常。供新增扩展能力复用，避免重复判断。
        /// </summary>
        private static AiOptions EnsureAiEnabled()
        {
            var options = AppSettings.Get<AiOptions>("AiOptions");
            if (options == null || !options.Enable)
            {
                throw new Exception("AI 功能未启用，请在 appsettings.json 配置 AiOptions");
            }
            var resolved = AiLlmClient.ResolveProvider(options);
            if (string.IsNullOrWhiteSpace(resolved.ApiKey))
            {
                throw new Exception("AI 功能未配置 ApiKey，请在 appsettings.json 的 AiOptions 或 Providers 中配置");
            }
            return options;
        }

        /// <summary>
        /// 调用大模型并包裹常见网络异常为友好提示。仅用于单次问答型能力。
        /// </summary>
        private async Task<string> ChatSafeAsync(string system, string user)
        {
            var options = EnsureAiEnabled();
            try
            {
                return await AiLlmClient.ChatAsync(options, system, user).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new Exception("调用 AI 服务失败：" + ex.Message);
            }
            catch (TaskCanceledException)
            {
                throw new Exception("调用 AI 服务超时，请稍后重试");
            }
        }

        // ===== 提交前审批意见话术建议（提示词见 Prompts/approval-suggest.md） =====

        public async Task<WfAiApprovalSuggestResult> SuggestApprovalAsync(WfAiApprovalSuggestInput input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.NodeName))
            {
                throw new Exception("缺少当前审批节点名称，无法生成审批建议，请刷新页面后重试");
            }

            var formText = string.IsNullOrWhiteSpace(input.FormContent) ? "（表单为空）" : input.FormContent;
            var draft = string.IsNullOrWhiteSpace(input.DraftOpinion) ? string.Empty : $"\n已有草稿意见：{input.DraftOpinion}";
            var user = $"审批节点：{input.NodeName}\n表单内容：{formText}{draft}";

            var text = await ChatSafeAsync(GetPromptOrThrow("approval-suggest.md", "审批意见建议"), user).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new Exception("AI 未返回建议内容，请稍后重试");
            }
            return new WfAiApprovalSuggestResult { Suggestion = JsonHelper.StripMarkdown(text).Trim() };
        }

        // ===== 提交后审批记录摘要（落痕用，异步调用，提示词见 Prompts/flow-review.md） =====

        public async Task<WfAiApprovalSummaryResult> SummarizeApprovalAsync(string action, string nodeName, string opinion, string formContent)
        {
            var formText = string.IsNullOrWhiteSpace(formContent) ? "（表单为空）" : formContent;
            var op = string.IsNullOrWhiteSpace(opinion) ? "（未填写意见）" : opinion;
            var user = $"审批动作：{action}\n审批节点：{nodeName}\n审批意见：{op}\n表单内容：{formText}";

            var text = await ChatSafeAsync(GetPromptOrThrow("flow-review.md", "审批记录摘要"), user).ConfigureAwait(false);
            return new WfAiApprovalSummaryResult { Summary = JsonHelper.StripMarkdown(text).Trim() };
        }

        // ===== 流程优化体检（提示词见 Prompts/flow-optimize.md） =====

        public async Task<WfAiFlowAnalyzeResult> AnalyzeFlowAsync(WfAiFlowAnalyzeInput input)
        {
            if (input == null || input.FlowId <= 0)
            {
                throw new Exception("流程定义 Id 不能为空");
            }
            var def = _definitionService.GetInfo(input.FlowId);
            if (def == null)
            {
                throw new Exception("流程定义不存在");
            }

            var formDesc = string.IsNullOrWhiteSpace(def.FormItems) ? "（无表单）" : def.FormItems;

            // 语义化上下文：把数字枚举/节点 Id 翻译为人类可读名称，避免 AI 输出"节点13""审批人类型5"等用户看不懂的技术表述。
            var nodes = def.Nodes ?? new List<WfFlowNodeDto>();
            var nodeNameById = nodes.Where(n => n.NodeId > 0).ToDictionary(n => n.NodeId, n => n.NodeName);
            string NodeLabel(long id) => nodeNameById.TryGetValue(id, out var name) ? $"「{name}」" : $"节点{id}";
            var nodesDesc = string.Join("\n", nodes.Select(n =>
            {
                var typeName = NodeTypeName(n.NodeType);
                var extra = n.NodeType == (int)WfNodeType.Audit ? $"，审批方式：{SignTypeName(n.SignType)}，审批人：{ApproverTypeName(n.ApproverType)}" :
                    n.NodeType == (int)WfNodeType.Cc ? "（抄送，无需审批）" : string.Empty;
                return $"- 节点「{n.NodeName}」：{typeName}{extra}";
            }));
            var linksDesc = string.Join("\n", (def.NodeLinks ?? new List<WfNodeLinkDto>()).Select(l => $"- 连线：{NodeLabel(l.SourceNodeId)} → {NodeLabel(l.TargetNodeId)}（条件：{l.ConditionJson ?? "无"}）"));
            var user = $"流程名称：{def.FlowName}\n表单字段（JSON）：{formDesc}\n节点列表：\n{nodesDesc}\n连线列表：\n{linksDesc}";

            var text = await ChatSafeAsync(GetPromptOrThrow("flow-optimize.md", "流程优化体检"), user).ConfigureAwait(false);
            return ParseAnalyzeResult(text);
        }

        private static WfAiFlowAnalyzeResult ParseAnalyzeResult(string raw)
        {
            var result = new WfAiFlowAnalyzeResult();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            var json = JsonHelper.StripMarkdown(raw);
            if (string.IsNullOrWhiteSpace(json)) return result;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("analysis", out var a)) result.Analysis = a.GetString() ?? string.Empty;
                if (root.TryGetProperty("suggestions", out var s) && s.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in s.EnumerateArray())
                    {
                        var v = item.GetString();
                        if (!string.IsNullOrWhiteSpace(v)) result.Suggestions.Add(v);
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                result.Analysis = raw.Trim();
                result.Suggestions = new List<string> { "（AI 返回非结构化文本，已原样呈现）" };
            }
            return result;
        }

        /// <summary>节点类型数字 → 中文名（供 AI 上下文与提示词使用）。</summary>
        private static string NodeTypeName(int nodeType) => nodeType switch
        {
            (int)WfNodeType.Start => "开始节点",
            (int)WfNodeType.Audit => "审批节点",
            (int)WfNodeType.Cc => "抄送节点",
            (int)WfNodeType.End => "结束节点",
            (int)WfNodeType.Condition => "条件网关",
            (int)WfNodeType.ParallelFork => "并行分叉网关",
            (int)WfNodeType.ParallelJoin => "并行汇聚网关",
            _ => $"未知类型({nodeType})"
        };

        /// <summary>签类型数字 → 中文名（0 或签 / 1 会签 / 2 依次审批）。</summary>
        private static string SignTypeName(int? signType) => signType switch
        {
            (int)WfSignType.And => "会签（需全部通过）",
            (int)WfSignType.Sequential => "依次审批",
            _ => "或签（一人通过即可）"
        };

        /// <summary>审批人类型数字 → 中文名（0 指定用户 / 1 角色 / 2 部门 / 3 表单字段 / 4 部门负责人 / 5 发起人主管）。</summary>
        private static string ApproverTypeName(int? approverType) => approverType switch
        {
            (int)WfApproverType.User => "指定用户",
            (int)WfApproverType.Role => "指定角色",
            (int)WfApproverType.Dept => "指定部门",
            (int)WfApproverType.Field => "表单字段（动态审批人）",
            (int)WfApproverType.DeptLeader => "部门负责人",
            (int)WfApproverType.ApplyLeader => "发起人主管",
            null => "未配置",
            _ => $"未知({approverType})"
        };

        // ===== 自然语言发起申请（Web 端：匹配流程 + 预填表单，提示词见 Prompts/intent-match.md） =====

        public async Task<WfAiMatchFillResult> MatchAndFillFormAsync(WfAiMatchFillInput input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.Description))
            {
                throw new Exception("申请描述不能为空");
            }

            // 取待匹配流程清单：指定 FlowId 则只用该流程；否则取全部启用且已发布的流程
            var candidates = new List<WfFlowDefinitionDto>();
            if (input.FlowId.HasValue && input.FlowId.Value > 0)
            {
                var single = _definitionService.GetInfo(input.FlowId.Value);
                if (single != null) candidates.Add(single);
            }
            else
            {
                var page = _definitionService.GetList(new WfFlowDefinitionQueryDto { Status = 1, IsDraft = 0, PageSize = 200 });
                candidates = page.Result ?? new List<WfFlowDefinitionDto>();
            }

            if (candidates.Count == 0)
            {
                throw new Exception("当前没有可用（已发布且启用）的流程定义，无法智能填单");
            }

            var catalog = string.Join("\n\n", candidates.Select(c =>
            {
                var fields = string.IsNullOrWhiteSpace(c.FormItems)
                    ? "（无表单字段）"
                    : c.FormItems;
                return $"FlowId={c.FlowId}\nFlowName={c.FlowName}\n字段（JSON）：{fields}";
            }));

            var user = $"可选流程清单：\n{catalog}\n\n用户描述：{input.Description}";

            var text = await ChatSafeAsync(GetPromptOrThrow("intent-match.md", "自然语言填单"), user).ConfigureAwait(false);
            return ParseMatchFillResult(text, candidates);
        }

        private static WfAiMatchFillResult ParseMatchFillResult(string raw, List<WfFlowDefinitionDto> candidates)
        {
            var result = new WfAiMatchFillResult();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            var json = JsonHelper.StripMarkdown(raw);
            if (string.IsNullOrWhiteSpace(json)) return result;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                long matchFlowId = 0;
                if (root.TryGetProperty("matchFlowId", out var mf) && mf.ValueKind == JsonValueKind.Number) matchFlowId = mf.GetInt64();
                if (matchFlowId <= 0 && root.TryGetProperty("flowId", out var fid) && fid.ValueKind == JsonValueKind.Number) matchFlowId = fid.GetInt64();

                var chosen = candidates.FirstOrDefault(c => c.FlowId == matchFlowId) ?? candidates.FirstOrDefault();
                if (chosen == null) return result;

                result.FlowId = chosen.FlowId;
                result.FlowName = chosen.FlowName ?? string.Empty;
                if (root.TryGetProperty("reason", out var r)) result.Reason = r.GetString() ?? string.Empty;

                if (root.TryGetProperty("formContent", out var fc) && fc.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in fc.EnumerateObject())
                    {
                        result.FormContent[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() ?? string.Empty : prop.Value.GetRawText();
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // 解析失败时回退到首个候选，保证前端至少能进入填单页
                var first = candidates.FirstOrDefault();
                if (first != null)
                {
                    result.FlowId = first.FlowId;
                    result.FlowName = first.FlowName ?? string.Empty;
                    result.Reason = "（AI 返回解析失败，已默认选中首个流程）";
                }
            }
            return result;
        }

        #endregion
    }
}
