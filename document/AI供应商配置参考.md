# AI 供应商配置参考

`ZR.Admin.WebApi/ai.json` 的 `Providers` **只保留当前在用的供应商**（默认 `qwen`）。切换或新增厂商时，把本文对应条目追加到 `Providers`，并把顶层 `Provider` 改成该厂商标识即可，无需预先保留所有厂商。

## 1. 解析规则

- 顶层 `Provider` 决定使用哪一家；`Providers` 中同名条目（忽略大小写）用于补齐 `BaseUrl` / `ChatEndpoint` / `Model` / `ApiKey`。
- 顶层 `ApiKey`、`Model`、`VisionModel` 非空时**覆盖**分项同名值；`TimeoutSeconds` 相反，**分项优先**，顶层为全局默认。
- 已在能力表 [AiProviderProfile.cs](../Infrastructure/AI/AiProviderProfile.cs) 登记的厂商中，`openai` / `deepseek` / `qwen` / `ollama` 可省略 `BaseUrl`、`ChatEndpoint`、`Model`，运行时取内置默认值（如 `qwen` 默认模型为 `qwen-turbo`，建议仍显式写 `Model`）；`bigmodel` 只登记了协议差异，地址与模型必须显式配置。
- 未登记的厂商按 OpenAI 标准协议处理（不下发 `stream_options` 等扩展字段），`BaseUrl`、`ChatEndpoint`、`Model` 需显式配置。
- 看图：目标厂商 = `VisionProvider`（未配置则同文本 `Provider`），模型取分项 `VisionModel`；顶层 `VisionModel` / `VisionBaseUrl` / `VisionApiKey` 等可覆盖。不支持多模态的厂商不要配 `VisionModel`。

## 2. 供应商速查

| 供应商 | BaseUrl | ChatEndpoint | 备注 |
| --- | --- | --- | --- |
| `openai` | `https://api.openai.com` | `/v1/chat/completions` | 协议基准 |
| `deepseek` | `https://api.deepseek.com` | `/chat/completions` | 无看图模型 |
| `qwen` | `https://dashscope.aliyuncs.com/compatible-mode/v1` | `/chat/completions` | usage 用 `input/output_tokens`，支持 `enable_thinking` |
| `ollama` | `http://127.0.0.1:11434/v1` | `/chat/completions` | 本地服务，免 Key，不认 `stream_options` |
| `bigmodel` | `https://open.bigmodel.cn/api/paas/v4` | `/chat/completions` | 智谱，地址与模型需显式配置 |

## 3. 参考配置

### 3.1 通义千问（默认在用）

```json
{
  "Provider": "qwen",
  "Label": "通义千问",
  "BaseUrl": "https://dashscope.aliyuncs.com/compatible-mode/v1",
  "ChatEndpoint": "/chat/completions",
  "ApiKey": "",
  "Model": "qwen3.7-plus",
  "VisionModel": "qwen3.7-flash",
  "Models": [ "qwen-turbo", "qwen-plus", "qwen-max", "qwen3.7-plus", "qwen3.7-flash", "qwen3-vl-flash", "qwen3-vl-plus", "qwen-vl-plus" ]
}
```

### 3.2 OpenAI

```json
{
  "Provider": "openai",
  "Label": "OpenAI",
  "BaseUrl": "https://api.openai.com",
  "ChatEndpoint": "/v1/chat/completions",
  "ApiKey": "",
  "Model": "gpt-4o-mini",
  "VisionModel": "gpt-4o-mini",
  "Models": [ "gpt-4o-mini", "gpt-4o" ]
}
```

### 3.3 DeepSeek

```json
{
  "Provider": "deepseek",
  "Label": "DeepSeek",
  "BaseUrl": "https://api.deepseek.com",
  "ChatEndpoint": "/chat/completions",
  "ApiKey": "",
  "Model": "deepseek-chat",
  "Models": [ "deepseek-chat", "deepseek-reasoner" ]
}
```

### 3.4 Ollama（本地）

```json
{
  "Provider": "ollama",
  "Label": "Ollama",
  "BaseUrl": "http://127.0.0.1:11434/v1",
  "ChatEndpoint": "/chat/completions",
  "ApiKey": "ollama",
  "Model": "qwen2.5:7b",
  "VisionModel": "llava",
  "Models": [ "qwen2.5:7b", "llama3.2", "deepseek-r1", "llava" ],
  "TimeoutSeconds": 120
}
```

Ollama 部署在其他机器时把 `BaseUrl` 改为 `http://host:11434/v1`；模型名必须与 `ollama list` 一致；本地可用占位 `ApiKey`。

### 3.5 智谱 BigModel

```json
{
  "Provider": "bigmodel",
  "Label": "智谱",
  "BaseUrl": "https://open.bigmodel.cn/api/paas/v4",
  "ChatEndpoint": "/chat/completions",
  "ApiKey": "",
  "Model": "glm-5.3-flash",
  "Models": [ "glm-5.3-flash" ]
}
```

### 3.6 其他 OpenAI 兼容厂商

`BaseUrl` 填到 `/v1` 一级、`ChatEndpoint` 用 `/chat/completions`，补齐 `ApiKey` 与 `Model` 即可。协议有差异（usage 字段名、`stream_options`、`enable_thinking` 等）时，需在 [AiProviderProfile.cs](../Infrastructure/AI/AiProviderProfile.cs) 的能力表补一条，避免回落到保守的 OpenAI 基准。

## 4. 本地覆盖与密钥

- `ai.Development.json` 已 gitignore，**只写本地差异**：顶层 `ApiKey` / `Model` / `VisionModel` / `TimeoutSeconds`。
- `Providers` 数组按整段覆盖而非合并，本地文件不要重复整份供应商配置，否则会把 `ai.json` 里的在用列表整体替换掉。
- 密钥不要提交到仓库，优先环境变量：

```powershell
# 当前 Provider 使用的 Key（非空时覆盖分项 ApiKey）
$env:AiOptions__ApiKey = "sk-你的密钥"

# 或指定某一家（下标以实际 json 中 Providers 顺序为准，单供应商时为 0）
$env:AiOptions__Providers__0__ApiKey = "sk-千问密钥"
```

改环境变量或 json 后需重启 API。
