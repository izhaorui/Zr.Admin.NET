<h2 align="center"> ZR.Admin.NET Enterprise Admin System</h2>
<h4 align="center">A cross-platform, RBAC-based, front-end/back-end separated admin framework built on .NET 8 + SqlSugar + Vue 3</h4>
<h5 align="center">SaaS multi-tenancy · Visual workflow · One-click code generator · Task scheduling · Cache &amp; real-time notifications · Rate limiting &amp; data scope · Dynamic i18n · AI assistant</h5>

<div style="text-align:center">

[![stars](https://gitee.com/izory/ZrAdminNetCore/badge/star.svg?theme=dark)](https://gitee.com/izory/ZrAdminNetCore)
[![fork](https://gitee.com//izory/ZrAdminNetCore/badge/fork.svg?theme=dark)](https://gitee.com/izory/ZrAdminNetCore/members)
[![Change log](https://img.shields.io/badge/ChangeLog-20260418-yellow)](http://www.izhaorui.cn/doc/changelog.html)

[![GitHub license](https://img.shields.io/github/license/izhaorui/ZrAdmin.NET)](https://github.com/izhaorui/ZrAdmin.NET/blob/main/LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/izhaorui/ZrAdmin.NET?style=social)](https://github.com/izhaorui/ZrAdmin.NET/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/izhaorui/ZrAdmin.NET?style=social)](https://github.com/izhaorui/ZrAdmin.NET/network)

</div>

---

<div align="center">
	<p><strong><a href="README.md">简体中文</a> | <a href="README.en.md">English</a></strong></p>
</div>

---

## 🍟 overview

- This project is suitable for developers with some .NET and Vue foundation.
- A general-purpose rights management platform (RBAC model) built on **.NET 8**: the latest technology stack, front-end/back-end separation, cross-platform and out of the box.
- Ships enterprise-ready capabilities: **SaaS multi-tenancy, a visual workflow engine, a one-click code generator, task scheduling, caching, real-time notifications, rate limiting, data-scope filtering, dynamic i18n and an AI assistant**.
- Less code, easier to learn and understand, powerful yet lightweight and easy to extend; make web development faster and simpler, and focus on your business.
- A community-maintained [Ant Design Vue](https://gitee.com/billzh/mc-dull.git) front-end version is also available.

```
If it helps you, you can click "Star" in the upper right corner to collect it, so that the author has the motivation to continue to go on for free, thank you! ~
```

## 📈 Quick start

- Quick start：[https://www.izhaorui.cn/doc/quickstart.html](https://www.izhaorui.cn/doc/quickstart.html)

## 🛠 Database initialization (CLI)

The full "auto migration + seed data" has been decoupled from the Web startup pipeline and is **no longer executed automatically on every `dotnet run`** (to avoid blocking/slowing down startup). Trigger it explicitly via a command-line argument — ideal for deployment, first-time setup, or seeding:

```bash
# Run from the repository root (where the .sln lives)
dotnet run --project ZR.Admin.WebApi -- --initdb
# or the short form
dotnet run --project ZR.Admin.WebApi -- -i
```

- A normal Web start (`dotnet run`) only performs **idempotent multi-tenant column patching** (`MigrateTenantColumns`); it does not re-run seeding, so startup is faster and more stable.
- Full migration + seed data is triggered **solely by the `--initdb` argument**; there is no "initialize on startup" switch to configure in `appsettings.json`.
- On failure, the initializer prints the exception and exits with code 1, making it easy to detect from deployment scripts.

## 🍿 Online experience

- Official documentation：http://www.izhaorui.cn/doc
- Join a group chat：[立即加入](http://www.izhaorui.cn/doc/contact.html)
- Vue3.x experience：[http://www.izhaorui.cn/vue3](http://www.izhaorui.cn/vue3)
- Uniapp experience：[http://www.izhaorui.cn/h5](http://www.izhaorui.cn/h5)
- account/password：admin/123456

| H5                                                                                     | WeChat mini program                                                                  |
| -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| ![alt](https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/qrcodeH5.png) | ![alt](https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/qrcode.jpg) |

```
Since it is a personal project, the funds are limited, and the experience server is low-fied, please cherish it, poke it lightly, and appreciate it!!
```

## 💒 Code repository

| repository | Github                                                               | Gitee                                                    |
| ---------- | -------------------------------------------------------------------- | -------------------------------------------------------- |
| net8       | [Clone/Download](https://github.com/izhaorui/Zr.Admin.NET) | [Clone/Download](https://gitee.com/izory/ZrAdminNetCore) |
| Vue3(Hot)  | [Clone/Download](https://github.com/izhaorui/ZR.Admin.Vue3)          | [Clone/Download](https://gitee.com/izory/ZRAdmin-vue)    |
| mobile     | [contact author](http://www.izhaorui.cn/vip/)                        | [contact author](http://www.izhaorui.cn/vip/)            |

## 🍁 Front-end technology

Vue Front-end technology stack: Based on Vue2.x/Vue3.x/UniApp, Vue, Vue-router, Vue-CLI, AXIOS, Element-UI, Echats, i18N Internationalization, etc., the front-end adopts VSCODE tool development

## 🍀 Back-end technology

- Core Framework: .NET 8.0 + Web API + SqlSugar + Swagger + SignalR + IpRateLimit + Quartz.NET + Redis + MiniExcel + AI
- Scheduled tasks: Quartz.Net component that supports the execution of assemblies or HTTP network requests
- Security support: filters (data permission filtering), SQL injection, request forgery
- Log management: NLog, login log, operation log, scheduled task log
- Tools: Captcha, rich public functions
- Interface throttling: Supports interface throttling to avoid excessive pressure on the service layer caused by malicious requests
- Code generation: efficient development, the code generator can generate all front-end and back-end code with one click
- Data dictionary: Support data dictionary, which can facilitate the management of some states
- Sharding: Using the SqlSugar ORM, you can easily achieve high-performance DB/table sharding
- Multi-tenant: Built-in full SaaS multi-tenant solution (DB-per-tenant with independent databases + package-based menu authorization); see [SaaS multi-tenant](#-saas-multi-tenant)
- Cache data: Built-in memory cache and Redis
- SignalR: Uses SignalR to manage user online status and push real-time notifications
- AI: Built-in AI assistant (chat / tool orchestration / image), business-page AI, workflow AI and a governed chart pipeline, plus AI governance (multi-level quotas, model pricing, usage stats, config checks and provider health probes)

## 🍖 Built-in features

1. User management: The user is the system operator, and this function mainly completes the system user configuration.
2. Department management: configure the system organization (company, department, group), tree structure display.
3. Job management: configure the position of the system user.
4. Menu management: configure system menus, operation permissions, button permission identification, etc.
5. Role Management: Role menu permission assignment.
6. Dictionary management: maintain some relatively fixed data that is often used in the system.
7. Operation log: system normal operation log records and queries; System exception information logging and querying.
8. Logon logon: The system logon log record query contains logon exceptions.
9. System Interface: Use Swagger to generate relevant API interface documentation.
10. Service monitoring: Monitor the current system CPU, memory, disk, stack, and other related information.
11. Online Builder: Drag form elements to generate the corresponding VUE code (only VUE2 supported).
12. Task system: Based on the Quartz.NET, you can schedule tasks online (add, modify, delete, manually execute) including execution result logs.
13. Article management: You can write article records.
14. Code generation: You can generate front-end and back-end code (.cs, .vue, .js, .sql, etc.) with one click, support download, customize the configuration of front-end display controls, and make development faster and more efficient (the strongest in history).
15. Parameter management: dynamically configure common parameters for the system.
16. Send Mail: You can send mail to multiple users.
17. File management: You can manage uploaded files, which currently supports uploading to on-premises and Alibaba Cloud.
18. Notification management: The system notifies and announces information release and maintenance, and uses SignalR to realize real-time notification to users.
19. Account Registration: You can register an account to log in to the system.
20. Multi-language management: support static and back-end dynamic configuration internationalization. Currently only supports Chinese, English, and Traditional characters (only VUE3 is supported)
21. Workflow module: a built-in lightweight approval workflow supporting dynamic forms, process copy, approve/reject/withdraw/transfer/add-sign, dashboard, and soft-delete with guards. See [Workflow module documentation](document/工作流模块说明.md)
22. SaaS multi-tenant: a complete multi-tenant SaaS system with full tenant lifecycle management, package authorization, user quotas and expiry reminders.
23. AI assistant: a global office assistant (chat / tool orchestration / image), business-page AI, workflow AI and a governed chart pipeline, plus AI governance (multi-level quotas, model pricing, usage stats, config checks and health probes).
24. Mall management: a complete e-commerce back end (product / SKU / spec template / category / brand / freight template / order OMS / after-sales / payment transactions / area dictionary) with a companion H5 storefront flow and sales statistics. See [Mall module](#-mall-module).

## 🍻 Project structure

```
├─ZR.Service                 ->[你的业务服务层类库]：提供WebApi接口调用；
├─ZR.ServiceCore             		->[系统服务层类库]：提供WebApi接口调用；
├─ZR.Repository                     ->[仓库层类库]：方便提供有执行存储过程的操作；
├─ZR.Model                		->[实体层类库]：提供项目中的数据库表、数据传输对象；
├─ZR.Admin.WebApi               	->[webapi接口]：为Vue版或其他三方系统提供接口服务。
├─ZR.Tasks               		->[定时任务类库]：提供项目定时任务实现功能；
├─ZR.CodeGenerator               	->[代码生成功能]：包含代码生成的模板、方法、代码生成的下载。
├─ZR.Vue               			->[前端UI]：vue2.0版本UI层(已经不再更新推荐使用vue3)。
├─document               		->[文档]：数据库脚本
```

## ⚡ Workflow module

A built-in lightweight approval workflow module covering the full chain of "definition → initiation → node approval → record tracking".

- **Dynamic form**: Configure form fields as JSON in the process definition; the front end auto-renders 9 control types (single/multi-line text, number, date, datetime, select, radio, switch, image upload).
- **Process copy**: One-click copy of the process definition and all node configurations, generating a disabled copy to avoid rebuilding.
- **Approval actions**: Approve, reject, withdraw, transfer, add-sign; supports OR/AND sign; approvers can be specific users/roles/departments.
- **Dashboard**: Aggregates the current user's todo / done / initiated-by-me / cc counts.
- **Soft delete & guard**: Deleting a process definition keeps historical data (nodes/instances/tasks/records); deletion is rejected if approval-pending or withdrawn instances exist.
- **Mobile menu**: Standalone workflow App workbench menu, filtered by permission.

See [Workflow module documentation](document/工作流模块说明.md).

## 🏢 SaaS Multi-tenant

The project ships a complete **SaaS multi-tenant solution** based on **DB-per-tenant** architecture, using SqlSugar's `SugarScope` multi-configuration capability for dynamic database routing. Each tenant owns an independent database with fully isolated business data, while shared data (tenants, packages, menus) stays in the **main database**. Out of the box, toggled by a single switch.

| Capability | Description |
| ---- | ---- |
| Multi-tenant switch | Enabled/disabled via `TenantSettings:UseTenant`; behaviour in non-multi-tenant mode is unchanged |
| Independent database | Each tenant maps to its own database instance, physically isolated and secure |
| Tenant lifecycle | Provisioning, initialization, suspension, renewal and deregistration, with one-click provisioning (schema + tables + seed data + default package) |
| Package system | Built-in Free/Pro packages plus custom package CRUD |
| Package menu authorization | Tick available menus per package; tenant capability = role menus ∩ package menus |
| User quota | Per-package user limits, validated on user creation/import |
| Expiry management | Automatic expiry checks, reminders and batch queries for soon-to-expire tenants |
| Platform menu isolation | Platform-only menus (tenant/menu management, etc.) visible only to the main tenant |

## 🤖 AI

Built-in, ready-to-use AI capabilities: the assistant, business pages and workflow share one LLM client and one "AI governance" layer, with unified quota, cost and audit control. Models follow the OpenAI-compatible protocol (`openai` / `deepseek` / `qwen` / `ollama`), and each provider can configure a separate text `Model` and `VisionModel`.

| Domain | Key capabilities |
| ---- | ---- |
| Office assistant | Multi-turn chat, SSE streaming, stop generation, auto titles, isolated per logged-in user; vision when images are attached (max 4), tool orchestration otherwise |
| Assistant tools | Schedule query/drafting, weekly report drafts, login-security and operation-health analysis, workflow todos, pre-registered chart queries |
| Image in/out | Upload or paste images, compressed before sending to the model with token usage persisted; workflow approval advice / risk prediction can carry image attachments |
| Business-page AI | Multi-language translation, Cron generation, code-generator column inference, login/operation log analysis |
| Workflow AI | Natural-language process generation, approval advice, process health check, intent form filling, instance summary, risk prediction, approval record summary |
| Chart pipeline | Whitelisted datasets only (no NL2SQL): daily logins, login regions, multi-dimensional operation-log stats; line/bar/pie charts assembled in chat |
| Governance & cost | Multi-level policies (user > role > tenant > global), model pricing, usage stats/export, config checks and provider health probes |

## 🛒 Mall module

`ZR.Mall` is an **out-of-the-box e-commerce back end** covering the full chain of "product listing → storefront browsing & ordering → payment → delivery → receipt → after-sales", with physical/virtual products, single/multi-spec SKUs, freight templates with remote-area exclusion, and native support for SaaS multi-tenancy (one mall database per tenant) and AI.

### Core capabilities

| Side / Domain | Key capabilities |
| ---- | ---- |
| Admin · Product | Product CRUD (soft delete), batch on/off-shelf, sorting, import/export, order-form metadata (`formMeta`) |
| Admin · SKU & Specs | Multi-spec SKUs and spec templates (color/size/capacity combinations), automatic spec summary and price-range write-back, auto recalculation of stock/sales helper columns |
| Admin · Category & Brand | Multi-level category tree (sorting, export), brand management (reference check before delete) |
| Admin · Freight | Freight templates: charge by piece or by weight, first/continued piece & weight pricing, free-shipping threshold, non-deliverable remote areas, default-template fallback |
| Admin · Orders (OMS) | List with status stats, detail, generic status update, single & Excel batch delivery, cancel, full-order refund, sales statistics and business overview (AOV / refund rate / after-sales rate / repurchase rate), top products and chart export; sensitive fields behind a dedicated permission with audit trail |
| Admin · After-sales | After-sale list/detail, audit, confirm receipt, register refund, cancel on behalf of buyer; both "refund only" and "return & refund" flows |
| Admin · Payment/Member/Area | Payment transaction reconciliation (incl. prepay credentials and raw callbacks), member list with enable/disable (members are created from the storefront), province/city/district dictionary (lazy tree) |
| Admin · AI | Product marketing copy drafts (title / selling points / description / SEO); registers "product stock query" and "recent order overview" tools for the global AI assistant |
| Storefront | Home banners & search, category tree, product list (comprehensive/sales/price/newest + in-stock only), product detail with multi-spec selection, checkout with freight calculation, WeChat Pay, my orders (cancel / pay / confirm receipt), my after-sales (apply / return tracking), shipping addresses (max 20, first one becomes default) |
| Operations | One-click mall database sync (tables / columns / hot-query indexes / product full-text index / menu and job seeds), auto-close of unpaid orders with stock rollback, auto-confirm receipt after delivery |

### Order state machine

```
Pending payment(0) --pay--> To be shipped(1) --deliver--> Shipped(2) --confirm/auto receipt--> Completed(3)
        |                        |                              |
        +-- timeout/cancel --> Cancelled(4)   +-- refund/after-sales done --> Closed(5)
```

After-sales states: Pending audit → Waiting for buyer return → Waiting for merchant receipt → Waiting for refund → Completed ("refund only" skips the return steps), with reject and cancel supported; the after-sales window defaults to 15 days from receipt and is configurable.

### Payment, stock & concurrency

- **Payment methods**: WeChat H5 / Mini Program JSAPI / App channels, plus an optional mock payment for development (`MallSettings:AllowMockPay`); Alipay is a reserved enum.
- **Payment callback**: callback amount consistency check + conditional-update idempotency + order status and payment transaction written in one transaction; prepay credentials are unique per order + channel.
- **Stock**: atomic deduction on order creation (`Stock >= qty` with optimistic-lock `Version`), stock restored on cancel/timeout/refund-before-delivery, locked stock released on delivery, and only the sales volume is adjusted for refunds after delivery.
- **Order idempotency**: Redis atomic idempotency key, reusing the same order within 15 minutes by default to prevent duplicate submissions.
- **State transitions**: all conditional updates carrying the expected current status, so only one side wins under concurrency.

### Scheduled jobs

| Job | Frequency | Rule |
| ---- | ---- | ---- |
| Close unpaid orders | Every 5 minutes | Auto-close unpaid orders and roll back stock; timeout defaults to 30 minutes (configurable), processed in independent batches |
| Auto-confirm receipt | Hourly | Auto-complete shipped orders after N days (default 7, configurable) |

### Storefront screenshots

<table>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/mall/1.jpeg" alt="Member login"/><br/><sub>Mobile login with SMS code (members auto-created)</sub></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/mall/7.jpeg" alt="Mall home"/><br/><sub>Home: search, sorting and free-shipping tags</sub></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/mall/5.jpeg" alt="Categories"/><br/><sub>Categories: multi-level tree with sales/price/newest sorting and in-stock filter</sub></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/mall/4.jpeg" alt="Product detail"/><br/><sub>Product detail: stock, sales and freight rules shown transparently</sub></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/mall/6.jpeg" alt="Spec selection"/><br/><sub>Multi-spec selection: per-SKU stock and price range</sub></td>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/mall/2.jpeg" alt="My orders"/><br/><sub>My orders: filtered by status, pay or view detail per order</sub></td>
    </tr>
    <tr>
        <td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/app/mall/3.jpeg" alt="Member center"/><br/><sub>Member center: order status shortcuts, shipping addresses and after-sales, with home/category/cart/mine navigation</sub></td>
        <td></td>
    </tr>
</table>

> Note: this repository contains the mall **back end** (`ZR.Mall`). The screenshots above are from the companion mobile (H5) storefront, which lives in a separate mobile repository.

## 🍎 Storyplate

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
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/29.png"/></td>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/30.png"/></td>
	</tr>
	<tr>
		<td><img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/23.png"/></td>
	</tr>
</table>

## 📱 Mobile Storyplate(vue2)

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

## 📱 Mobile Storyplate(vue3)

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

## 🎉 Advantages

1. The front-end system does not need to write login, authorization, and authentication modules; Just write the business module
2. The background system can be used directly after release without any secondary development
3. The front-end and back-end systems are separated, and they are separate systems (domain names can be independent)
4. Unified handling of global exceptions
5. Custom code generation features
6. Less dependence, easy to get started
7. Comprehensive documentation

## 💐 Special thanks

- 👉Ruoyi.vue：[Ruoyi](http://www.ruoyi.vip/)
- 👉SqlSugar：[SqlSugar](https://gitee.com/dotnetchina/SqlSugar)
- 👉vue-element-admin：[vue-element-admin](https://github.com/PanJiaChen/vue-element-admin)
- 👉Meiam.System：[Meiam.System](https://github.com/91270/Meiam.System)
- 👉Furion：[Furion](https://gitee.com/dotnetchina/Furion)

## 🎀 donation

If you feel that the project has helped you, you can ask the author for a cup of coffee as a sign of encouragement ☕️
<img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/pay.jpg"/>
