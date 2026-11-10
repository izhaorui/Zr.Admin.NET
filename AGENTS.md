# ZRAdminNetCore AI Agent 开发规范

适用于任何能读取并修改本仓库代码的 AI Agent（Codex / Claude Code / Cursor / Copilot 等）。**修改本项目前必须先阅读并遵守本文件。**

## 1. 项目定位

前后端分离的 .NET 后台管理系统：.NET Web API + SqlSugar + SQL Server + RBAC，前端 Vue3 + Vite + Element Plus + Pinia，另有定时任务、Redis、SignalR、代码生成器、多租户及商城扩展。

核心工程：

```text
ZR.Model      实体 / DTO / ViewModel
ZR.Repository 数据访问（已有 Repository 模式）
ZR.Service    业务模块（逻辑主要位置）
ZR.ServiceCore 系统级公共业务（用户/角色/菜单/权限/部门/字典/日志）
ZR.Admin.WebApi Web API / Controller（保持轻量）
ZR.Tasks      Quartz 定时任务
ZR.CodeGenerator 已有代码生成能力
ZR.Mall       商城模块
ZR.Vue        旧版 Vue2（新功能勿优先改）
```

新功能优先 Vue3，Vue2 仅维护用。

## 2. AI 最重要的原则

1. **先理解，再修改**：读取需求 → 查结构 → 搜代码 → 找现有实现 → 理解调用链 → 定最小方案 → 改 → 编译 → 验证。禁止凭经验直接造代码。
2. **优先复用**：新增功能前先搜相似 Controller/Service/Model/DTO/Vue 页/API/权限/表/组件/生成器代码，复制并调整已有模式，不自创架构。
3. **不要过度设计**：不擅自引入 CQRS/DDD/MediatR/新 ORM/新 UI 框架/新状态管理，除非用户明确要求。
4. **最小修改**：只改需求所需代码，不顺手重构无关模块、改名、升级依赖、改公共组件/库结构、格式化全项目、修无关 Bug。发现问题告知用户，不擅自扩大范围。

## 3. 分层与目录职责

- **Controller**：收参、验证、调 Service、返回。不堆业务逻辑。
- **Service**：业务逻辑、查询、事务、状态流转的主要位置。不为"分层"增加项目中不存在的层。
- 普通查询优先现有 SqlSugar 用法（`Queryable/Insertable/Updateable/Deleteable/SqlFunc`），不为普通查询新建 Repository 架构。
- Service 中勿随意定义大量 DTO，新增实体/DTO 优先参考同类已有文件。
- **using 与 GlobalUsing**：各工程根目录的 `GlobalUsing.cs` 已统一引入常用命名空间（如 `Infrastructure`、`Infrastructure.Attribute`、`Infrastructure.Extensions`、`Infrastructure.Model`、`SqlSugar`、`ZR.Model.System` 等，各工程清单不同）。写代码前先看所在工程的 GlobalUsing.cs，**GlobalUsing 已引入的命名空间不要在文件里重复 `using`**；新增常用命名空间优先加进 GlobalUsing，而不是逐个文件补。文件内只写 GlobalUsing 未覆盖的 using。
- 修改 `ZR.ServiceCore` 系统核心需特别谨慎，不因业务需求直接改核心逻辑。

## 4. 数据库与 SqlSugar

- 主要用 SqlSugar，不混用其他ORM。
- **性能**：避免循环查询、N+1、无条件大表扫描；批量查后内存 Dictionary 映射。大数据列表用数据库分页，不 `ToList` 后内存分页。
- **新增表/字段需谨慎**：
  - 字段考虑类型/长度/NULL/默认值/索引/唯一约束/历史兼容。
  - 表考虑主键/索引/逻辑删除/创建修改时间/租户字段/生命周期。
  - 不轻易加表：先确认“现有表 → 加字段 → 扩展/JSON 字段”都无法满足才加。
  - 不随意改主键/外键/唯一索引/权限/状态/租户字段——改前搜全项目用途。
  - 脚本尽量幂等（`IF NOT EXISTS ...`）。
- **禁止 AI 直接操作数据库**：不得连接数据库执行任何语句（**包括只读查询**），也不得通过脚本/命令行（`sqlcmd`、`mysql` 等）绕过。需要改表或查数据时，只输出 **SQL + 影响说明**，由用户自行在客户端执行并回传结果；确需 AI 执行的，必须逐条获得用户明确确认，不得默认授权、不得打包成一条命令顺手执行。
- **SQL Server 修改后必须明确告知用户**：新增了哪些表/字段/索引、改了什么结构。
- **表达式树限制**：SqlSugar 解析 `Where/Select/GroupBy/Having/OrderBy`、`SqlFunc.*`、`Expressionable` 时会求值表达式里引用的成员，**私有 static 字段会直接抛 `Field "xxx" can't be private`**（`private static readonly` 与可变 `private static` 都会中招；`SysLoginService` 曾因 `it.Status == LoginAuditSuccess` 导致登录日志 AI 分析整体失败）。可用写法：`const`（编译期内联）、先落局部变量再用（编译器闭包字段为 public）、`public/internal/protected static readonly`、实例字段/属性、静态方法调用。**禁止把 `private static` 字段直接写进查询表达式**。

## 5. 多租户

多租户/多数据库环境下，任何业务数据修改前先确认归属（平台/租户/公共）。涉及租户数据须确认当前租户、库、用户权限、数据所属租户；**严禁凭用户传入 ID 直接跨租户访问**。使用租户分库时不机械给所有表加 `TenantId`，先确认模块实际架构。

## 6. 权限

新增功能须考虑菜单/页面/按钮/权限编码/API，沿用现有 `xxx:list/query/add/edit/remove/export` 命名，不建第二套体系。

## 7. 前端（Vue3）

- 新页优先 `<script setup>` + `ref/reactive/computed/watch/onMounted`。
- 不重复造组件：新增页前先搜列表/表单/Dialog/Upload/Pagination/Table/Tree/Selector 等，复用已有。
- API 调用用项目已有 `request` 封装，不遍地 `axios.get/post`。
- UI 用 Element Plus，不引 Ant Design Vue/Naive UI 等。
- 全局状态用 Pinia Store，局部用 `ref/reactive`，不用 `window/global/eventBus` 替代。

## 8. Workflow / LogicFlow

视为业务扩展模块，不改变 ZRAdmin 原有架构。

- **LogicFlow**：流程设计/可视化/节点/边/布局，不是引擎。
- **Workflow Engine**：流程定义/实例/节点执行/审批任务/抄送/条件/状态流转/并发控制/任务抢占。执行逻辑不塞进 LogicFlow。
- **FlowJSON**：用于设计器恢复/预览/编辑；执行模型用于运行。二者不强行合并。

## 9. 并发与状态

- 并发：查询/更新/删除/事务。
- 状态流转：审批/订单/支付/库存/权限/租户/任务。

## 10. 定时任务

必须幂等：执行前检查当前状态，仅处理符合条件的数据，处理后更新状态。不假设只执行一次，多实例须防并发重复。

## 11. 日志、异常、事务

**日志**

- **通道看场景**：业务代码里"事后要能查到"的信息用 NLog（`private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();`）；NLog 未就绪（宿主启动早期、非 Web 宿主工具）或只需控制台可见时用 `Infrastructure.Log.WriteLine`（带颜色、不落文件）。
- **落点**：业务日志 → `logs/all.log` + 控制台；`logs/error.log` 只收 `GlobalExceptionMiddleware` 的未处理异常（业务 `logger.Error` 也在 all.log）。
- **写法**：插值 `logger.Info($"下单成功：OrderNo={orderNo}")`，不用占位符 `("...{OrderNo}...", orderNo)`；异常必须传参 `logger.Error(ex, $"...")`，否则堆栈不输出。
- **级别**：`Info`=状态成功流转；`Warn`=业务拒绝与并发未命中；`Error`=事务失败、安全事件、后端异常；`Debug`=高频项（幂等命中、任务空跑）。
- **内容**：中文 + `Key=Value`，含业务标识（订单号/ID/会员/金额/状态 `from→to`）与具体失败原因；批量操作记汇总（数量 + 前 20 个 ID），循环里不打 `Info`。
- **脱敏**：手机号/姓名/证件/卡号/邮箱/IP 统一用 `ZR.Infrastructure.Helper.MaskUtil`，**禁记密码/Token/Secret/完整卡号**；收货人、详细地址不入技术日志。
- **事务**：失败记 `tran.ErrorMessage`；校验类逻辑放 `UseTran` **之前**，否则异常被吞成笼统的"添加失败/修改失败"。

**异常与事务**

- 禁止空 `catch (Exception) {}` 吞异常；捕获后须记录、判断可否恢复、必要时重抛。用全局异常处理，不为每个 Controller 自创返回格式。
- 多个必须同时成功的操作须用事务，保证失败不部分成功，不拆成互不相关提交。

## 12. API 兼容与安全

- 不随意改已有 URL/Method/参数名/返回字段结构/状态值。前端已用则改前搜调用方，变更优先兼容。
- 主动关注 SQL 注入/XSS/越权/IDOR/文件上传/路径穿越/Token 泄露/权限绕过/租户串数据。**用户传入的 ID 不代表其有权访问，必须做业务权限校验**。
- 文件上传考虑大小/类型/扩展名/MIME/文件名/保存路径/权限/恶意文件，不直接拼接用户文件名成服务器路径。
- 第三方 API 考虑 Timeout/Cancellation/Retry/Rate Limit/Exception/幂等/日志，不无限重试，不把 Key/Secret/Password/Token 写死在代码。

## 13. 依赖管理

新增 NuGet/npm 依赖前确认：项目是否已存在、现有代码能否实现、是否真需要、是否兼容版本、是否增加维护成本。小功能不引大型框架。不擅自升级 .NET/NuGet/SqlSugar/ASP.NET Core，除非用户要求。

## 14. Git 与安全操作

- 未授权不得执行 `git reset --hard`/`git clean -fd`/`git push --force`/删远程分支/删大量文件/覆盖用户未提交修改。
- 改前查 `git status`；已有用户修改**必须保留，不得覆盖**；不因测试失败回滚整个工作区。
- 提交前查 `git status`/`git diff`，确认无误修改、无无关文件、无敏感信息、无调试/临时代码，再提交。

## 15. 生成类任务

- **标准 CRUD**：查数据库 → 看 `ZR.CodeGenerator` → 看同类模块 → 遵循已有 Model/Service/Controller/Vue → 补菜单权限。不发明新 CRUD 架构。
- **Workflow 流程**：自然语言 → 结构化数据 → 校验节点/条件 → 转 LogicFlow → 用户确认 → 保存。不直接生成 SQL；生成的流程须可被系统验证。
- **AI 数据查询（未来）**：用户问题 → 意图识别 → 权限/租户检查 → 结构化查询 → 安全检查 → 执行。**严禁默认执行 DROP/TRUNCATE/全表 DELETE/全表 UPDATE**。

## 16. 修改前与不确定时

- 改方法前至少搜：方法定义、调用位置、相似实现、相关 Model/API/前端调用；改公共方法尤甚。
- 项目代码与常识冲突时**以项目实际代码为准**，不因"一般项目都这样"改设计。
- 影响结果的重要歧义可问用户；项目已有同类实现则优先参考，不反复问。
- 不主动扩大需求（如"加商品分类"只做分类，不自动加品牌/标签/搜索/AI/统计）。

## 17. 编译与测试

- 改后端跑 `dotnet build`（命令以实际 `.sln`/`.csproj` 为准）；改前端跑 `npm run build`。不假设命令一定存在。
- 核心修改至少覆盖：正常/异常/边界/并发/权限/租户，重点关注 Workflow/Order/Payment/Inventory/Permission/Tenant/Task。

## 18. 任务完成输出

简洁说明三段：**修改内容**（实际改了什么）、**关键设计**（架构/库/并发/兼容影响）、**验证结果**（build/test PASS，或明确"未执行测试"，不得谎称通过）。

## 19. 最终准则

ZRAdminNetCore 是成熟项目，AI 目标是理解现有架构、复用现有代码、最小修改、减少重复、提高效率、保证业务正确。优先级：安全 → 数据正确 → 业务正确 → 兼容 → 并发正确 → 性能 → 可维护性 → 代码美观。**项目已有代码永远优先于 AI 的默认习惯。**
