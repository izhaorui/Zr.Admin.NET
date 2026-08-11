<h2 align="center"> ZR.Admin.NET后台管理系统</h2>
<h4 align="center">基于.Net8 + vue2.x/vue3.x/uniapp前后端分离的.net快速开发框架</h4>

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

- 本项目适合有一定 .Net 和 vue 基础的开发人员
- 基于.NET8 实现的通用权限管理平台（RBAC 模式）。整合最新技术高效快速开发，前后端分离模式，开箱即用。
- 代码量少、学习简单、通俗易懂、功能强大、易扩展、轻量级，让 web 开发更快速、简单高效（从此告别 996），解决 70%的重复工作，专注您的业务，轻松开发从现在开始！
- 提供了技术栈(Ant Design Vue)版[Ant Design Vue](https://gitee.com/billzh/mc-dull.git)
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
双击执行
init-run.bat
```

## 🍿 在线体验

- 官方文档：http://www.izhaorui.cn/doc
- 加入群聊：[立即加入](http://www.izhaorui.cn/doc/contact.html)
- web 端体验：[http://demo.izhaorui.cn/vue3](http://demo.izhaorui.cn/vue3)
- Uniapp 版本体验(vue2)：[http://demo.izhaorui.cn/h5](http://demo.izhaorui.cn/h5)
- Uniapp 版本体验(vue3)：[http://demo.izhaorui.cn/uplus](http://demo.izhaorui.cn/uplus)
- 账号密码：admin/123456，普通用户 user/123456

| H5                                                                                     |
| -------------------------------------------------------------------------------------- |
| ![alt](https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/qrcodeH5.png) |

```
由于是个人项目，资金有限，体验服务器是低配，请大家爱惜，轻戳，不胜感激！！！
```

## 💒 代码仓库

| 仓库                | Github                                                 | Gitee                                               |
| ------------------- | ------------------------------------------------------ | --------------------------------------------------- |
| net8                | [克隆/下载](https://github.com/izhaorui/Zr.Admin.NET)  | [克隆/下载](https://gitee.com/izory/ZrAdminNetCore) |
| web 前端 vue3(推荐) | [克隆/下载](https://github.com/izhaorui/ZR.Admin.Vue3) | [克隆/下载](https://gitee.com/izory/ZRAdmin-vue)    |
| 移动端              | [联系作者](http://www.izhaorui.cn/vip/)                | [联系作者](http://www.izhaorui.cn/vip/)             |
| 工作流版              | [联系作者](http://www.izhaorui.cn/vip/)                | [联系作者](http://www.izhaorui.cn/vip/)             |

## 🍁 前端技术

Vue 版前端技术栈 ：基于 vue2.x/vue3.x/uniapp、vuex、vue-router 、vue-cli 、axios、 element-ui、echats、i18n 国际化等，前端采用 vscode 工具开发

## 🍀 后端技术

- 核心框架：.Net8.0 + Web API + sqlsugar + swagger + signalR + IpRateLimit + Quartz.net + Redis
- 定时计划任务：Quartz.Net 组件，支持执行程序集或者 http 网络请求
- 安全支持：过滤器(数据权限过滤)、Sql 注入、请求伪造
- 日志管理：NLog、登录日志、操作日志、定时任务日志
- 工具类：验证码、丰富公共功能
- 接口限流：支持接口限流，避免恶意请求导致服务层压力过大
- 代码生成：高效率开发，代码生成器可以一键生成所有前后端代码
- 数据字典：支持数据字典，可以方便对一些状态进行管理
- 分库分表：使用 orm `sqlSugar` 可以很轻松的实现分库分库性能优越
- 多 租 户：内置完整 SaaS 多租户方案（DB-per-tenant 独立数据库 + 套餐菜单授权），详见下方 [SaaS 多租户](#-saas-多租户)
- 缓存数据：内置内存缓存和 `Redis`
- signalR：使用 `signalr` 管理用户在线状态
- 工作流：内置轻量级审批流（Work Flow）模块，覆盖「流程定义 → 发起申请 → 节点审批 → 记录追踪」全链路。

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
12. 任务系统：基于 `Quartz.NET`，可以在线（添加、修改、删除、手动执行）任务调度包含执行结果日志。
13. 文章管理：可以写文章记录。
14. 代码生成：可以一键生成前后端代码(.cs、.vue、.js、.sql、uniapp 等)支持下载，自定义配置前端展示控件、让开发更快捷高效。
15. 参数管理：对系统动态配置常用参数。
16. 发送邮件：可以对多个用户进行发送邮件。
17. 文件管理：可以进行上传文件管理，目前支持上传到本地、阿里云。
18. 通知管理：系统通知公告信息发布维护，使用 signalr 实现对用户实时通知。
19. 账号注册：可以注册账号登录系统。
20. 多语言管理：支持静态、后端动态配置国际化。目前只支持中、英、繁体(仅支持 vue3)
21. 在线用户：可以查看正在登录使用的用户，可以对其踢出、通知操作
22. db 审计日志：数据库审计功能
23. 三方登录：提供三方登录实现逻辑
24. 导入导出：支持中文表头导入、字典数据转换成文本导出
25. 数据大屏：更直观的展示数据
26. 商城管理：商城功能，包含订单管理、发货、分类、品牌管理、销售统计；（前端还在开发中）
27. SaaS 多租户：内置完整的多租户 SaaS 体系，支持租户全生命周期管理、套餐授权、用户配额、到期提醒等。
28. 工作流模块：内置轻量级可视化审批流引擎，支持动态表单、流程设计复制、通过/驳回/撤回/转办/加签等全场景流转、运行数据面板，并提供软删除与防呆校验保障数据安全。

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

## ⚡ 工作流模块

内置轻量级可视化审批流（Work Flow）模块，覆盖「流程定义 → 发起申请 → 节点审批 → 记录追踪」全链路，开箱即用地支撑企业日常审批场景。

- **动态表单**：流程定义中以 JSON 配置表单字段，前端按字段类型自动渲染对应控件（单行/多行文本、数字、日期、日期时间、下拉、单选、开关、图片上传等），无需硬编码页面。
- **流程设计器**：可视化拖拽编排审批节点与流转连线，支持条件网关、并行分支、抄送节点，一键复制流程生成停用副本，避免重复搭建。
- **审批动作**：通过、驳回、撤回、转办、加签；支持或签/会签，审批人可指定用户/角色/部门，灵活适配各类审批规则。
- **运行数据面板**：聚合当前用户的待办 / 已办 / 我发起 / 抄送数量，进度一目了然。
- **软删除与防呆**：删除流程定义时保留全部历史数据（节点/实例/任务/记录），且存在审批中或撤回中实例时拒绝删除，保障数据可追溯。
- **多端协同**：配套独立的移动端工作流工作台菜单，按权限过滤展示，随时随地发起与审批。

## 🍻 项目结构

![alt](https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/kj.png)

```
├─ZR.Service                ->[你的业务服务层类库]：提供自己业务数据Api接口调用；
├─ZR.ServiceCore            ->[系统服务层类库]：提供系统Api接口；
├─ZR.Repository             ->[仓库层类库]：方便提供有执行存储过程的操作；
├─ZR.Model                	->[实体层类库]：自己业务库表、数据传输对象；
├─ZR.Admin.WebApi           ->[webapi接口]：为Vue版或其他三方系统提供接口服务。
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
