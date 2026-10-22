<h2 align="center">ZR.Admin.NET 企业级后台管理系统</h2>
<h4 align="center">基于 .NET 8 + SqlSugar + Vue 3 的前后端分离、跨平台通用权限管理框架（RBAC）</h4>
<h5 align="center">SaaS 多租户 · 双因子与登录安全 · 可视化工作流 · 一键代码生成 · 任务调度 · 缓存与实时通知 · 限流与数据权限 · 动态国际化 · AI 助手</h5>

<!-- <p align="center">
	<a href="https://gitee.com/izory/ZrAdminNetCore"><img src="https://gitee.com/izory/ZrAdminNetCore/badge/star.svg?theme=dark"></a>
<a href='https://gitee.com/izory/ZrAdminNetCore/members'><img src='https://gitee.com/izory/ZrAdminNetCore/badge/fork.svg?theme=dark' alt='fork'></img></a>
	<a href="https://gitee.com/izory/ZrAdminNetCore/blob/master/LICENSE"><img src="https://img.shields.io/github/license/mashape/apistatus.svg"></a>
	<a href="http://www.izhaorui.cn/doc/changelog.html"><img src="https://img.shields.io/badge/更新日志-20230920-yellow"/></a>
</p> -->

<div style="text-align:center">

[![stars](https://gitee.com/izory/ZrAdminNetCore/badge/star.svg?theme=dark)](https://gitee.com/izory/ZrAdminNetCore)
[![fork](https://gitee.com//izory/ZrAdminNetCore/badge/fork.svg?theme=dark)](https://gitee.com/izory/ZrAdminNetCore/members)
[![更新日志](https://img.shields.io/badge/更新日志-20260723-yellow)](http://www.izhaorui.cn/doc/changelog.html)

[![GitHub license](https://img.shields.io/github/license/izhaorui/ZrAdmin.NET)](https://github.com/izhaorui/ZrAdmin.NET/blob/main/LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/izhaorui/ZrAdmin.NET?style=social)](https://github.com/izhaorui/ZrAdmin.NET/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/izhaorui/ZrAdmin.NET?style=social)](https://github.com/izhaorui/ZrAdmin.NET/network)

</div>

---

<div align="center">
	<p><strong><a href="README.md">简体中文</a> | <a href="README.en.md">English</a></strong></p>
</div>

---

## 🍟 概述

- 本项目适合有一定 .NET 和 Vue 基础的开发人员
- 基于 .NET 8 实现的通用权限管理平台（RBAC 模式）。整合最新技术高效快速开发，前后端分离模式，跨平台、开箱即用。
- 内置 SaaS 多租户（一租户一库）、TOTP 双因子与登录安全、可视化工作流引擎、一键代码生成、任务调度与 AI 助手等企业级能力。
- 代码量少、学习简单、通俗易懂、功能强大、易扩展、轻量级，让 web 开发更快速、更简单高效，专注您的业务。
- 另提供社区适配的 [Ant Design Vue](https://gitee.com/billzh/mc-dull.git) 版前端
- 七牛云通用云产品优惠券：[点我进入](https://s.qiniu.com/FzEfay)。
- 腾讯云秒杀场：[☛☛ 点我进入 ☚☚](https://curl.qcloud.com/4yEoRquq)。
- 腾讯云优惠券：[☛☛ 点我领取 ☚☚](https://curl.qcloud.com/5J4nag8D)。
- 阿里云特惠专区：[☛☛ 点我进入 ☚☚](https://www.aliyun.com/minisite/goods?userCode=uotn5vt1&share_source=copy_link)

```
如果对您有帮助，您可以点右上角 “Star” 收藏一下 ，谢谢！~
```

## 📈 快速开始

- 快速开始：[https://www.izhaorui.cn/doc/quickstart.html](https://www.izhaorui.cn/doc/quickstart.html)

## 🛠 数据库初始化（CLI）

「自动迁移 + 种子数据」通过命令行参数显式触发，适合部署、首次建库、灌种子时使用：

```bash
# 在仓库根目录执行（.sln 所在目录）
dotnet run --project ZR.Admin.WebApi -- --initdb
# 或简写
dotnet run --project ZR.Admin.WebApi -- -i

# 或者 
双击执行（脚本位于 bat 目录）
bat/init-run.bat
```

## 🍿 在线体验

- 官方文档：http://www.izhaorui.cn/doc
- 加入群聊：[立即加入](http://www.izhaorui.cn/doc/contact.html)
- web 端体验：[http://demo.izhaorui.cn/vue3](http://demo.izhaorui.cn/vue3)
- Uniapp 版本体验(vue2)：[http://demo.izhaorui.cn/h5](http://demo.izhaorui.cn/h5)
- Uniapp 版本体验(vue3)：[http://demo.izhaorui.cn/uplus](http://demo.izhaorui.cn/uplus)
- 移动端工作流体验(vue3-ts版)：[http://demo.izhaorui.cn/wf](http://demo.izhaorui.cn/wf)
- 账号密码：user/123456(工作流演示联系作者)

| H5                                                                                     | 工作流 |
| -------------------------------------------------------------------------------------- |--------|
| <img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/qrcodeH5.png" alt="H5 体验" width="200" height="200" /> | <img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/workflow_demo.png" alt="工作流体验" width="200" height="200" /> |

```
由于是个人项目，资金有限，体验服务器是低配，请大家爱惜，轻戳，不胜感激！！！
```

## 💒 代码仓库

| 仓库                | Github                                                 | Gitee                                               |
| ------------------- | ------------------------------------------------------ | --------------------------------------------------- |
| net8                | [克隆/下载](https://github.com/izhaorui/Zr.Admin.NET)  | [克隆/下载](https://gitee.com/izory/ZrAdminNetCore) |
| web 前端 vue3(推荐) | [克隆/下载](https://github.com/izhaorui/ZR.Admin.Vue3) | [克隆/下载](https://gitee.com/izory/ZRAdmin-vue)    |
| 移动端              | [联系作者](http://www.izhaorui.cn/vip/)                | [联系作者](http://www.izhaorui.cn/vip/)             |
| 工作流版              | [联系作者](http://www.izhaorui.cn/enterprise/)                | [联系作者](http://www.izhaorui.cn/enterprise/)             |

## 🍁 前端技术

Vue 版前端技术栈 ：基于 vue2.x/vue3.x/uniapp、vuex、vue-router 、vue-cli 、axios、 element-ui、echats、i18n 国际化等，前端采用 vscode 工具开发

## 🍀 后端技术

- 核心框架：.NET 8.0 + Web API + SqlSugar + Swagger + SignalR + IpRateLimit + Quartz.NET + Redis + MiniExcel + AI
- 定时计划任务：Quartz.Net 组件，支持执行程序集或者 http 网络请求
- 安全支持：过滤器(数据权限过滤)、Sql 注入、请求伪造、TOTP 双因子、登录设备登记与异常提醒、登录失败锁定、存量令牌的用户/租户状态复核
- 日志管理：NLog、登录日志、操作日志、定时任务日志
- 工具类：验证码、丰富公共功能
- 接口限流：支持接口限流，避免恶意请求导致服务层压力过大
- 代码生成：高效率开发，代码生成器可以一键生成所有前后端代码
- 数据字典：支持数据字典，可以方便对一些状态进行管理
- 分库分表：使用 orm `sqlSugar` 可以很轻松的实现分库分库性能优越
- 多 租 户：内置完整 SaaS 多租户方案（DB-per-tenant 独立数据库 + 套餐菜单授权），详见下方 [SaaS 多租户](#-saas-多租户)
- 缓存数据：内置内存缓存和 `Redis`，并提供 `ICache` 统一抽象（原子自增 / 原子增减 / `TrySet`），限流、下单幂等、AI 额度等跨实例场景共用
- signalR：使用 `signalr` 管理用户在线状态
- 工作流：内置审批流（WorkFlow）模块，覆盖「流程定义 → 发起申请 → 节点审批 → 记录追踪」全链路。
- 对象存储：本地上传 / 阿里云 OSS / S3 兼容存储（RustFS、MinIO、Ceph RGW、Cloudflare R2、Backblaze B2、AWS S3），默认存储类型可配置
- 全文检索：文章正文与商城商品名支持全文索引，索引未就绪时自动退化为 `LIKE`，不因部署缺失导致搜索不可用

## 🍖 内置功能

1. 用户管理：用户是系统操作者，该功能主要完成系统用户配置。
2. 部门管理：配置系统组织机构（公司、部门、小组），树结构展现。
3. 岗位管理：配置系统用户所属担任职务。
4. 菜单管理：配置系统菜单，操作权限，按钮权限标识等。
5. 角色管理：角色菜单权限分配。
6. 字典管理：对系统中经常使用的一些较为固定的数据进行维护。
7. 操作日志：系统正常操作日志记录和查询；系统异常信息日志记录和查询。
8. 登录日志：系统登录日志记录查询包含登录异常。
9. 系统接口：使用 `swagger` 生成相关 api 接口文档。
10. 服务监控：监视当前系统 CPU、内存、磁盘、堆栈等相关信息。
11. 在线构建器：拖动表单元素生成相应的 VUE 代码(仅支持 vue2)。
12. 任务系统：基于 `Quartz.NET`，可以在线（添加、修改、删除、手动执行）任务调度包含执行结果日志；支持程序集 / HTTP 任务、按租户分发，SQL 类任务经 `SqlTaskGuard` 护栏校验（禁多语句、DDL、系统存储过程与无 WHERE 的全表更新/删除）。
13. 文章管理：可以写文章记录。
14. 代码生成：可以一键生成前后端代码(.cs、.vue、.js、.sql、uniapp 等)支持下载，自定义配置前端展示控件、让开发更快捷高效。
15. 参数管理：对系统动态配置常用参数。
16. 发送邮件：可以对多个用户进行发送邮件。
17. 文件管理：可以进行上传文件管理，支持上传到本地、阿里云 OSS、S3 兼容对象存储，默认存储类型可配置。
18. 通知管理：系统通知公告信息发布维护，使用 signalr 实现对用户实时通知。
19. 账号注册：可以注册账号登录系统。
20. 多语言管理：支持静态、后端动态配置国际化。目前只支持中、英、繁体(仅支持 vue3)
21. 在线用户：可以查看正在登录使用的用户，可以对其踢出、通知操作
22. db 审计日志：数据库审计功能
23. 三方登录：提供三方登录实现逻辑
24. 导入导出：支持中文表头导入、字典数据转换成文本导出
25. 数据大屏：更直观的展示数据
26. 商城管理：完整电商闭环后端（商品/SKU/规格模板/分类/品牌/运费模板/订单 OMS/售后/支付流水/地区字典），配套 C 端 H5 购物链路与销售统计，详见下方 [商城模块](#-商城模块)。
27. saas多租户：内置完整的多租户 SaaS 体系，支持租户全生命周期管理、套餐授权、用户配额、到期提醒等。
28. 工作流模块：内置可视化审批流引擎，支持动态表单、流程版本管理与发布前试运行、流程设计复制、通过/驳回/撤回/转办/加签减签等全场景流转、驳回策略与会签模型、Webhook 集成与效率统计，并提供软删除与防呆校验保障数据安全。
29. AI 助手：内置全局办公助手（对话/工具编排/发图）、业务页 AI、工作流 AI 与安全图表能力，并配套 AI 治理（多级限额、模型价格、用量统计、配置检查与探活）。
30. 双因子认证：TOTP 认证器 + 一次性恢复码 + 「记住此设备」，覆盖账号密码 / 手机号 / 扫码三条登录入口，支持自助绑定解绑与管理员重置，登录日志记录本次鉴权方式。
31. 登录安全：登录设备登记与逐台移除、新设备登录提醒（站内信 + 邮件）、双因子连续失败告警、登录失败锁定与解锁提醒、改密后清空全部设备信任。
32. 对象存储：除本地与阿里云 OSS 外新增 S3 兼容存储接入，上传按 `storeType` 路由，可通过 `Upload:defaultStoreType` 统一指定默认存储。
33. 全文检索：文章正文与商城商品名支持全文检索，自动探测索引是否就绪并按需回退。

## 🏢 SaaS 多租户

项目内置了一套完整的 **SaaS 多租户解决方案**，采用 **DB-per-tenant（独立数据库）** 架构，通过 SqlSugar 的 `SugarScope` 多配置能力实现动态数据库路由。每个租户拥有独立的数据库实例，业务数据完全隔离；租户、套餐、菜单等共享数据集中存放在**主库**。开箱即用，一键开关。

### 核心能力

| 能力 | 说明 |
| ---- | ---- |
| 多租户开关 | 通过 `TenantSettings:UseTenant` 一键启停，非多租户模式行为完全不变 |
| 独立数据库 | 每个租户对应独立的数据库实例，数据物理隔离，安全可靠 |
| 租户生命周期 | 开通、初始化、停服、续费、注销全流程管理，支持一键开通（建档+建表+种子数据+默认套餐） |
| 套餐体系 | 预置免费版/专业版套餐，支持自定义套餐 CRUD |
| 套餐菜单授权 | 为每个套餐勾选可用菜单，租户能力由「角色菜单 ∩ 套餐菜单」决定 |
| 用户配额 | 按套餐限制租户用户数上限，新增/导入用户自动校验 |
| 到期管理 | 自动到期校验、到期提醒，支持批量查询即将到期租户 |
| 平台菜单隔离 | 平台专属菜单（租户管理、菜单管理等）仅主租户可见，双重防护 |

## 🔐 登录与账号安全

在 JWT 登录之上补齐「异常可感知、事后可追溯」的账号安全闭环，相关配置集中在 `appsettings` 的 `TwoFactor` 节点（全部可缺省，默认即可用）。

| 能力 | 说明 |
| ---- | ---- |
| 双因子认证 | 基于 TOTP（认证器 App）；密钥 AES-GCM 加密落库，启用时一次性展示恢复码，支持自助解绑与管理员重置 |
| 多入口一致 | 账号密码、手机号验证码、扫码三条登录入口共用同一套第二因子判定与设备登记，避免某个入口漏走一步 |
| 可信设备 | 「记住此设备」由服务端签发凭据并只存哈希，客户端指纹不参与信任判定；信任期默认 7 天，可逐台吊销 |
| 设备管理 | 每次登录登记设备（浏览器 / 系统 / IP / 时间），个人中心可查看全部登记设备并逐台移除；移除后再登录按新设备提醒 |
| 异常提醒 | 新设备登录、双因子连续失败、账号被锁定均发站内信（可配邮件），带账号级限频，避免攻击者刷爆告警 |
| 登录失败锁定 | 凭据错误按阈值锁定（`sys.account.maxRetryCount` / `sys.account.lockTime`），成功后清零；不区分账号是否存在 |
| 令牌状态复核 | 验签后再复核用户与租户状态，禁用用户、停服或到期租户的存量 token 立即失效，且复核发生在自动续期之前 |
| 改密收敛 | 自助改密与管理员重置密码都会清空该账号全部设备信任期并告知用户；2FA 重置保留设备登记，避免误报新设备 |
| 协议预留 | 第二因子方法由服务端下发列表、前端按列表渲染；WebAuthn / Passkey 常量与接入点已预留，落地时无需改前端 |

## ⚡ 工作流模块

内置可视化审批流（WorkFlow）模块，覆盖「流程定义 → 发起申请 → 节点审批 → 记录追踪」全链路，开箱即用地支撑企业日常审批场景。

<p align="center">
  <img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/wf6.png" alt="流程设计器" width="720" />
  <br/>
  <sub>可视化流程设计器：拖拽编排审批、条件分支、并行分叉/汇聚、抄送等节点</sub>
</p>

<p align="center">
  <img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/wf3.png" alt="动态表单设计" width="48%" />
  &nbsp;
  <img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/wf5.png" alt="审批详情与流程图" width="48%" />
  <br/>
  <sub>左：动态表单设计器（零代码配置字段）&nbsp;|&nbsp;右：审批详情流程图与审批记录追踪</sub>
</p>

- **动态表单**：流程定义中以 JSON 配置表单字段，前端按字段类型自动渲染对应控件（单行/多行文本、数字、日期、日期时间、下拉、单选、开关、图片上传等），无需硬编码页面。
- **流程设计器**：可视化拖拽编排审批节点与流转连线，支持条件网关、并行网关（分叉/汇聚）、抄送节点，一键复制流程生成停用副本，避免重复搭建。
- **流程版本管理**：同一 `FlowCode` 多版本共存（版本号联合唯一），支持另存新版本、发布 / 回滚、设为现行版本、复制与导入导出；草稿态与发布态分离，已产生实例的定义禁止就地编辑，避免历史实例按新流程图跑。
- **发布前试运行**：按给定表单推演审批路线（条件选路 / 并行汇聚 / 审批人解析），不落库，一次性暴露空审批人、条件求值失败等配置问题。
- **审批动作**：通过、驳回、撤回、转办、加签、减签、委托代审，管理员可改派与跳转节点；审批人可指定用户 / 角色 / 部门，灵活适配各类审批规则。
- **驳回策略**：节点级可配「驳回发起人 / 上一审批节点 / 指定节点 / 审批人运行时自选」，自选在驳回时由审批人提交，非法选择在事务外直接拒绝。
- **会签模型**：或签、会签、依次审批、比例会签（达到设定通过比例即推进），兼顾效率与合规场景。
- **催办提醒**：申请人可对审批中实例向当前节点审批人发送催办通知，后端限制同实例 24 小时内仅可催办一次。
- **运行数据面板**：聚合当前用户的待办 / 已办 / 我发起 / 抄送数量；效率统计给出平均 / 最短 / 最长审批时长、节点耗时分布与按月完成率趋势。
- **管理员运维**：流程实例支持终止 / 挂起 / 恢复 / 改派 / 跳转节点，管理员可查看全部用户流程并按申请人筛选。
- **超时自动处理**：节点可配置超时时长与超时动作（自动通过 / 自动驳回 / 转交指定人），由定时任务按租户周期扫描超时待办并自动处置，全程留痕与通知；超时窗口按受理人起算，依次审批轮转、转办、改派、加签后重新计时。
- **Webhook 集成**：节点进出事件在业务事务内写 Outbox，由独立投递任务回调外部系统，失败指数退避重试、超限进入死信，可查投递状态、重试次数与最近错误。
- **性能与并发**：热查询补充索引，超时扫描分批读取，单节点完成判定按需查询，避免大流程下的全表扫描与重复计算。
- **软删除与防呆**：删除流程定义时保留全部历史数据（节点/实例/任务/记录），且存在审批中或撤回中实例时拒绝删除，保障数据可追溯。
- **多端协同**：配套独立的移动端工作流工作台菜单，按权限过滤展示，随时随地发起与审批。

## 🤖 AI 功能

内置开箱即用的 AI 能力，助手、业务页、工作流共用同一套 LLM 客户端与「AI 治理」，统一管控额度、成本与审计；模型走 OpenAI 兼容协议（`openai` / `deepseek` / `qwen` / `ollama`），每个供应商可分别配置文本 `Model` 与 `VisionModel`。

### 能力总览

| 域 | 关键能力 |
| ---- | ---- |
| 办公助手 | 多轮会话、SSE 流式、停止生成、自动标题、按登录用户隔离；有图走视觉（最多 4 张）、无图走工具编排 |
| 助手工具 | 日程查询 / 日程解析草稿 / 周报草稿；登录安全分析、操作健康分析；工作流待办；预注册图表查询；失败会提示并再给一轮纠错 |
| 发图看图 | 上传或粘贴图片；本机/内网文件转 data URI 并发模型前压缩；看图 token 落库；工作流审批建议 / 风险预判也可带图片附件 |
| 业务页 AI | 多语言翻译、Cron 生成、代码生成列推断、登录/操作日志分析（各页独立按钮权限） |
| 工作流 AI | 自然语言生成流程、审批建议、流程体检、意图填单、实例摘要、风险预判、审批记录摘要 |
| 图表管道 | 白名单数据集（禁止 NL2SQL）：每日登录数、登录地域、操作日志多维统计；对话内组装折线/柱状/饼图，结果可导出 Excel |
| 治理与成本 | 多级策略（user > role > tenant > global）、模型价格、用量统计/导出、配置检查（含 `VisionModel` 名）、Provider 探活；助手头栏展示 Token 与金额额度 |
| 调用稳定性 | 自动重试与统一异常体系、AI 调用审计日志；SSE 流式按「空闲超时 + 总时长硬上限」终止，429 按 `Retry-After` 退避并透出治理拒绝原因 |
| 并发与配额 | 并发数与在途额度改用 `CacheStore` 跨实例原子计数，多节点部署下额度不被放大，异常残留计数自动回收 |
| 上下文与工具 | 历史滑窗摘要压缩、工具并行调用、工具 Provider 注册（内置工具即插即用）、管理端工具目录、空态示例问句由后端下发、图表结果缓存 |
| 模型接入 | OpenAI 兼容协议：`openai` / `deepseek` / `qwen` / `ollama`；供应商分项可配文本 `Model` 与 `VisionModel`，渠道差异收敛为能力表，新增供应商无需改客户端 |

## 🛒 商城模块

`ZR.Mall` 是一套**开箱即用的电商闭环后端**，覆盖「商品上架 → C 端浏览下单 → 支付 → 发货 → 收货 → 售后」全链路，支持实物/虚拟商品、单/多规格 SKU、运费模板与偏远地区排除，并天然接入 SaaS 多租户（一租户一商城库）与 AI 能力。

### 核心功能

| 端 / 域 | 关键能力 |
| ---- | ---- |
| 后台 · 商品 | 商品 CRUD（软删除）、批量上下架、排序、导入导出、商品形态（实物/虚拟）、购买表单定义（`formMeta` 元数据 + 下单校验 + 订单快照） |
| 后台 · SKU/规格 | 多规格 SKU 与规格模板（颜色/尺寸/容量等组合）、规格摘要与价格区间自动回写、库存与销量冗余列自动重算 |
| 后台 · 分类/品牌 | 树形多级分类（排序、导出）、品牌管理（删除前校验商品引用） |
| 后台 · 运费 | 运费模板：按件/按重计费、首件续件与首重续重、包邮门槛、偏远地区不配送排除、默认模板回落 |
| 后台 · 订单(OMS) | 列表（状态统计）、详情、通用状态更新、发货与 Excel 批量发货、取消、整单退款、销售统计与经营概览（客单价/退款率/售后率/复购率）、Top 商品与图表导出；敏感字段独立权限 + 操作留痕 |
| 后台 · 售后 | 售后单列表/详情、审核、确认收货、登记退款、代客撤销；覆盖「仅退款」与「退货退款」两条流程，申请受时效约束（默认确认收货后 15 天，可配置） |
| 后台 · 支付/会员/地区 | 支付流水对账（含预支付凭证与回调原文）、会员列表与启用禁用（会员由 C 端注册产生）、省市区地区字典（懒加载 tree） |
| 后台 · AI | 商品营销文案草稿生成（标题/卖点/详情/SEO）；并为全局 AI 助手注册「商品库存查询」「近期订单概况」工具 |
| C 端 | 首页轮播与搜索（全文索引 + 相关性分级）、分类树、商品列表（综合/销量/价格/上新 + 仅看有货，缺货沉底 / 热度衰减 / 置顶时效）、商品详情与多规格选择、结算与运费试算、微信支付、我的订单（取消/去支付/确认收货）、我的售后（申请/填退货物流）、收货地址 |
| 部署与性能 | C 端接口走公网宿主 `ZR.Front.WebApi`（仅 `[PublicApi]` 接口可达，后台接口 404）；商城热查询补索引、商品名全文索引随初始化自动创建、首页与列表查询缓存 |


### 支付、库存与并发

- **支付方式**：微信 H5 / 小程序 JSAPI / App 渠道，开发联调可选模拟支付（`MallSettings:AllowMockPay`）；支付宝为预留枚举。
- **支付回调**：回调金额一致性校验 + 条件更新幂等 + 订单状态与支付流水同事务；预支付凭证按「订单 + 渠道」唯一。
- **库存**：下单原子扣减（`Stock >= qty`，乐观锁 `Version`），取消/关单/未发货退款回补可售库存，发货释放锁定库存，已发货退款只冲销量。
- **下单幂等**：Redis 原子占位幂等键，默认 15 分钟内复用同一订单，防重复提交。
- **状态迁移**：全部使用带原状态的条件更新，并发下仅一方命中。


### C 端演示

<table>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/mall/1.jpeg" alt="会员登录"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/mall/7.jpeg" alt="商城首页"/></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/mall/5.jpeg" alt="商品分类"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/mall/4.jpeg" alt="商品详情"/></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/mall/6.jpeg" alt="规格选择"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/mall/2.jpeg" alt="我的订单"/></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/mall/3.jpeg" alt="会员中心"/></td>
        <td></td>
    </tr>
</table>

> 说明：本仓库为商城**后端**（`ZR.Mall`）；上图为配套 C 端（移动端 H5）实际界面，前端位于独立的移动端仓库。

## 🍻 项目结构

<p align="center">
  <img src="./document/images/project-structure.svg" alt="ZR.Admin.NET 项目结构（分层架构图）" width="1100" />
</p>

```
├─ZR.Service                ->[你的业务服务层类库]：提供自己业务数据Api接口调用；
├─ZR.ServiceCore            ->[系统服务层类库]：提供系统Api接口；
├─ZR.Repository             ->[仓库层类库]：方便提供有执行存储过程的操作；
├─ZR.Model                	->[实体层类库]：自己业务库表、数据传输对象；
├─ZR.Admin.WebApi           ->[webapi接口]：为Vue版或其他三方系统提供接口服务。
├─ZR.Front.WebApi           ->[公网 C 端宿主]：只注册带 [PublicApi] 标记的接口，后台管理接口一律不可达。
├─Infrastructure            ->[基础设施]：缓存抽象、AI 客户端与治理、模块初始化、中间件与公共模型。
├─ZR.Common                 ->[公共类库]：动态 API、缓存、邮件、微信等通用工具。
├─ZR.Tasks               	->[定时任务类库]：提供项目定时任务实现功能；
├─ZR.CodeGenerator          ->[代码生成功能]：包含代码生成的模板、方法、代码生成的下载。
├─ZR.Mall                   ->[商城后端]：商城相关的后端代码。
├─ZR.Workflow               ->[工作流后端]：工作流相关的后端代码。
├─ZR.Vue               		->[前端UI]：vue2.0版本UI层(已经不再更新推荐使用vue3)。
├─document               	->[文档]：数据库脚本(已弃用)
```

## 🍎 演示图

<table>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/1.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/2.png"/></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/3.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/4.png"/></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/5.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/6.png"/></td>
    </tr>
	<tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/7.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/8.png"/></td>
    </tr>	
	<tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/9.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/10.png"/></td>
    </tr>
	<tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/11.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/12.png"/></td>
    </tr>
	<tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/13.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/14.png"/></td>
    </tr>
	<tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/15.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/16.png"/></td>
    </tr>
	<tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/17.png"/></td>
				<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/18.png"/></td>
    </tr>
	<tr>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/19.png"/></td>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/20.png"/></td>
	</tr>
	<tr>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/21.png"/></td>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/22.png"/></td>
	</tr>
	<tr>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/23.png"/></td>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/24.png"/></td>
	</tr>
    <tr>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/25.jpeg"/></td>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/26.jpeg"/></td>
	</tr>
     <tr>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/27.jpeg"/></td>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/28.jpeg"/></td>
	</tr>     
	<tr>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/29.png"/></td>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/30.png"/></td>
	</tr>
    <tr>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/ai1.png"/></td>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/ai2.png"/></td>
	</tr>
</table>

## 📱 移动端(vue2)

<table>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/a1.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/a2.png"/></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/a8.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/a4.png"/></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/a5.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/a6.png"/></td>
    </tr>
		<tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/a7.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/a9.png"/></td>
    </tr>
		<tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/a10.png"/></td>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/a11.png"/></td>
    </tr>
		<tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/a12.png"/></td>
    </tr>
</table>

## 📱 移动端(vue3)

<table>
		<tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/12.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/13.png"/></td>
    </tr>
		<tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/14.png"/></td>
				<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/15.png"/></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/16.png"/></td>
    		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/17.png"/></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/18.png"/></td>
    		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/19.png"/></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/21.jpg"/></td>
    		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/22.jpg"/></td>
    </tr>    
		<tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/23.jpg"/></td>
    </tr>

</table>

## 工作流

<table>
	<tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/wf1.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/wf2.png"/></td>
    </tr>
	<tr>
         <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/wf3.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/wf5.png"/></td>
    </tr>
    <tr> 
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/wf6.png"/></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/wf7.png"/></td>
    </tr>
</table>

## 🎉 优势

1. 前台系统不用编写登录、授权、认证模块；只负责编写业务模块即可
2. 后台系统无需任何二次开发，直接发布即可使用
3. 前台与后台系统分离，分别为不同的系统（域名可独立）
4. 全局异常统一处理
5. 自定义的代码生成功能
6. 依赖少(只需数据库即可使用)，上手容易
7. 文档全面

## 💐 特别鸣谢

- 👉Ruoyi.vue：[Ruoyi](http://www.ruoyi.vip/)
- 👉SqlSugar：[SqlSugar](https://gitee.com/dotnetchina/SqlSugar)
- 👉vue-element-admin：[vue-element-admin](https://github.com/PanJiaChen/vue-element-admin)
- 👉Meiam.System：[Meiam.System](https://github.com/91270/Meiam.System)
- 👉Furion：[Furion](https://gitee.com/dotnetchina/Furion)

## 🎀 捐赠

如果你觉得这个项目帮助到了你，你可以请作者喝杯咖啡表示鼓励 ☕️
<img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/pay.jpg"/>
