<h2 align="center"> ZRAdmin.NET后台管理系统</h2>
<h4 align="center">基于.NET5/.NET6 + vue2.x/vue3.x前后端分离的.net快速开发框架</h4>  


<p align="center">
	<a href="https://gitee.com/izory/ZrAdminNetCore"><img src="https://gitee.com/izory/ZrAdminNetCore/badge/star.svg?theme=dark"></a>
	<a href="https://gitee.com/izory/ZrAdminNetCore/blob/master/LICENSE"><img src="https://img.shields.io/github/license/mashape/apistatus.svg"></a>
</p>

## 🍟概述
* 本项目适合有一定NetCore和 vue基础的开发人员
* 基于.NET5/.NET6实现的通用权限管理平台（RBAC模式）。整合最新技术高效快速开发，前后端分离模式，开箱即用。
* 代码量少、学习简单、通俗易懂、功能强大、易扩展、轻量级，让web开发更快速、简单高效（从此告别996），解决70%的重复工作，专注您的业务，轻松开发从现在开始！
* 提供了技术栈(Ant Design Vue)版[Ant Design Vue](https://gitee.com/billzh/mc-dull.git)
* 七牛云通用云产品优惠券：[点我进入](https://s.qiniu.com/FzEfay)。
* 腾讯云秒杀场：[点我进入](https://curl.qcloud.com/4yEoRquq)。
* 腾讯云优惠券：[点我领取](https://curl.qcloud.com/5J4nag8D)。

```
如果对您有帮助，您可以点右上角 “Star” 收藏一下 ，这样作者才有继续免费下去的动力，谢谢！~
```

## 🍿在线体验
- 官方文档：http://www.izhaorui.cn/doc
- vue3.x版本体验：http://www.izhaorui.cn/vue3
- vue2.x版本体验：http://www.izhaorui.cn/admin
- 账号密码：admin/123456


```
由于是个人项目，资金有限，体验服是低配，请大家爱惜，轻戳，不胜感激！！！
```
## 💒代码仓库
| 仓库        | Github                                                           | Gitee                                                            |
| ----------- | ---------------------------------------------------------------- | ---------------------------------------------------------------- |
| Vue2 + Net5 |                                                                  | [克隆/下载](https://gitee.com/izory/ZrAdminNetCore/)             |
| Vue2 + Net6 | [克隆/下载](https://github.com/izhaorui/ZrAdmin.NET/tree/net6.0) | [克隆/下载](https://gitee.com/izory/ZrAdminNetCore/tree/net6.0/) |
| Vue3        | [克隆/下载](https://github.com/izhaorui/ZRAdminVue)              | [克隆/下载](https://gitee.com/izory/ZRAdmin-vue)   

## 🍁前端技术
Vue版前端技术栈 ：基于vue2.x/vue3.x、vuex、vue-router 、vue-cli 、axios、 element-ui、echats、i18n国际化等，前端采用vscode工具开发

## 🍀后端技术
- 核心框架：.Net5.0/.Net6.0 + Web API + sqlsugar + swagger + signalR + IpRateLimit + Quartz.net + Redis
- 定时计划任务：Quartz.Net组件，支持执行程序集或者http网络请求
- 安全支持：过滤器(数据权限过滤)、Sql注入、请求伪造
- 日志管理：NLog、登录日志、操作日志、定时任务日志
- 工具类：验证码、丰富公共功能
- 接口限流：支持接口限流，避免恶意请求导致服务层压力过大
- 代码生成：高效率开发，代码生成器可以一键生成所有前后端代码
- 数据字典：支持数据字典，可以方便对一些状态进行管理
- 分库分表：使用orm sqlsugar可以很轻松的实现分库分库性能优越
- 多 租 户：支持多租户功能
- 缓存数据：内置内存缓存和Redis


## 🍖内置功能

1. 用户管理：用户是系统操作者，该功能主要完成系统用户配置。
2. 部门管理：配置系统组织机构（公司、部门、小组），树结构展现。
3. 岗位管理：配置系统用户所属担任职务。
4. 菜单管理：配置系统菜单，操作权限，按钮权限标识等。
5. 角色管理：角色菜单权限分配。
6. 字典管理：对系统中经常使用的一些较为固定的数据进行维护。
7. 操作日志：系统正常操作日志记录和查询；系统异常信息日志记录和查询。
8. 登录日志：系统登录日志记录查询包含登录异常。
9. 系统接口：使用 swagger 生成相关 api 接口文档。
10. 服务监控：监视当前系统 CPU、内存、磁盘、堆栈等相关信息。
11. 在线构建器：拖动表单元素生成相应的 VUE 代码。
12. 任务系统：基于 Quartz.NET，可以在线（添加、修改、删除、手动执行)任务调度包含执行结果日志。
13. 文章管理：可以写文章记录。
14. 代码生成：可以一键生成前后端代码(.cs、.vue、.js、sql等)支持下载，自定义配置前端展示控件、让开发更快捷高效。
15. 参数管理：对系统动态配置常用参数。
16. 发送邮件：可以对多个用户进行发送邮件。
17. 文件管理：可以进行上传文件管理，目前支持上传到本地、阿里云。
18. 通知管理：系统通知公告信息发布维护，使用 signalr 实现对用户实时通知。
19. 账号注册：可以注册账号登录系统。
20. 多语言管理：支持静态、后端动态配置国际化。目前只支持中、英、繁体

## 🍻项目结构

```
├─ZR.Service             			->[服务层类库]：提供WebApi接口调用；
├─ZR.Repository                     ->[仓库层类库]：方便提供有执行存储过程的操作；
├─ZR.Model                			->[实体层类库]：提供项目中的数据库表、数据传输对象；
├─ZR.Admin.WebApi               	->[webapi接口]：为Vue版或其他三方系统提供接口服务。
├─ZR.Tasks               			->[定时任务类库]：提供项目定时任务实现功能；
├─ZR.CodeGenerator               	->[代码生成功能]：包含代码生成的模板、方法、代码生成的下载。
├─ZR.Vue               				->[前端UI]：vue2.0版本UI层。
```

## 🍎演示图

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
</table>

## 🎉优势

1. 前台系统不用编写登录、授权、认证模块；只负责编写业务模块即可
2. 后台系统无需任何二次开发，直接发布即可使用
3. 前台与后台系统分离，分别为不同的系统（域名可独立）
4. 全局异常统一处理
5. 自定义的代码生成功能

## 💐 特别鸣谢
- 👉Ruoyi.vue：[Ruoyi](http://www.ruoyi.vip/)
- 👉SqlSugar：[SqlSugar](https://gitee.com/dotnetchina/SqlSugar)
- 👉vue-element-admin：[vue-element-admin](https://github.com/PanJiaChen/vue-element-admin)

## 🎀捐赠
如果你觉得这个项目帮助到了你，你可以请作者喝杯咖啡表示鼓励 ☕️
<img src="https://gitee.com/izory/ZrAdminNetCore/raw/master/document/images/pay.jpg"/>

## 🔧使用说明
如果部署iis访问不了情况可以有以下两种办法：
1. 直接打开ZR.Admin.WebApi.exe文件然后看控制台的错误日志
2. web.config里面有个false 改为 true，iis重启项目后运行网站后，跟目录下面 有个文件夹 log 里面有错误日志文件
