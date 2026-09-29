# 贡献指南

## 适用范围
本文件定义项目级协作规范，适用于仓库根目录下所有业务代码。

## AI 模块约定

### IAiAssistantToolProvider 契约（必须遵守）
1. ExecuteAsync(string toolName, string argsJson, long userId) 对于非本提供者负责的工具名，必须返回 null。
2. 对于本提供者负责的工具名，必须返回非 null 的 AiToolExecResult。
3. 可预期的业务失败（如权限不足、参数非法、无数据）必须通过 AiToolExecResult.Error("...") 返回，不应作为异常抛出。
4. 仅不可恢复的系统错误（如依赖服务崩溃）才允许抛出异常，并必须记录日志。
5. 工具实现必须复用现有业务 Service，不得绕过权限、租户与数据范围控制。
6. 工具响应应控制长度，避免把超长原始数据回灌给模型。

### 工具命名与可维护性
1. 工具名必须稳定且语义明确，避免频繁变更。
2. 工具参数需有清晰 JSON Schema（type/properties/required）。
3. 同名工具不应在多个提供者重复注册；如重复，以首个注册者为准。

## 提交要求
1. 变更 ISysAiChatService / SysAiChatService / IAiAssistantToolProvider 后，必须本地编译通过。
2. 涉及流式对话（SSE）时，需验证 delta/tool/done/error 事件链路完整。
3. 任何破坏兼容性的接口变更，必须在 PR 描述中明确。

## 提交信息规范

格式：`:emoji:(scope) 描述`

```
:sparkles:(mall,member) 会员ID自增起始值改为100000
:bug:(host) 修正公网宿主 AddDb 早于 InternalApp 赋值导致启动即失败
```

- emoji 用 gitmoji 短代码，仅允许下列 13 个：

  | 图标 | 短代码 | 含义 |
  |---|---|---|
  | 🐛 | `:bug:` | 修复 bug |
  | ✨ | `:sparkles:` | 新功能 |
  | ⚡ | `:zap:` | 优化性能 |
  | 📝 | `:pencil:` | 写文档 |
  | 🎨 | `:art:` | 结构改进 / 格式化代码 |
  | 🔥 | `:fire:` | 删除代码或文档 |
  | 🔀 | `:twisted_rightwards_arrows:` | 合并分支 |
  | 🚚 | `:truck:` | 文件移动或重命名 |
  | 🗃️ | `:card_file_box:` | 数据库相关改动 |
  | ♻️ | `:recycle:` | 重构 / 清理废弃代码 |
  | ⬆️ | `:arrow_up:` | 升级依赖 |
  | 🔒 | `:lock:` | 安全相关修复 |
  | 🔧 | `:wrench:` | 配置、构建、gitignore 等杂项 |

- emoji 短代码一律小写
- scope 可选，多个用逗号分隔，一律小写且不含空格（如 `(mall,member)`）
- 描述必填，用中文，动词开头，结尾不加句号
- 首行不超过 100 个字符
- 校验只针对新提交，不会改写历史提交；`Merge` / `Revert` 等 git 自动生成的消息自动跳过

### 启用本地校验

clone 后执行一次（每台机器一次，重复执行无害）：

```
bat\init-hooks.bat
```

该校验由 `.githooks/commit-msg` 实现，**不需要 Node / npm 环境**。
手动校验某条消息：`sh .githooks/commit-msg <消息文件>`

