# ZRAdmin.NET

## 🍟概述
* 本项目适合有一定NetCore和 vue基础的开发人员
* 基于.NET 5实现的通用权限管理平台（RBAC模式）。整合最新技术高效快速开发，前后端分离模式，开箱即用。
* 代码量少、学习简单、通俗易懂、功能强大、易扩展、轻量级，让web开发更快速、简单高效，解决70%的重复工作，专注您的业务，轻松开发从现在开始！
* 前端采用Vue2.0、Element UI。
* 后端采用Net5、Sqlsugar、MySQL。
* 权限认证使用Jwt，支持多终端认证系统。
* 支持加载动态权限菜单，多方式轻松权限控制

> 如果对您有帮助，您可以点右上角 “Star” 收藏一下 ，这样作者才有继续免费下去的动力，谢谢！~

## 🍿在线体验
体验地址：http://www.izhaorui.cn:8080/
管理员：admin
密  码：123456

**由于是个人项目，资金有限，体验服是低配，请大家爱惜，轻戳，不胜感激！！！**

## 🍁前端技术
Vue版前端技术栈 ：基于vue、vuex、vue-router 、vue-cli 、axios 和 element-ui，前端采用vscode工具开发

## 🍀后端技术
核心框架：.Net5.0 + Web API + sqlsugar + swagger

定时计划任务：Quartz.Net组件

安全支持：过滤器、Sql注入、请求伪造

日志管理：NLog、登录日志、操作日志

工具类：验证码、丰富公共功能、代码生成

## 🍄快速启动
需要安装：VS2019（最新版）、npm或yarn（最新版）
准备工作：将document文件夹下面的admin.sql脚本导入到数据库中，修改ZR.Admin.WebApi项目里面配置文件中的conn_zrAdmin数据库连接字符串以及DbType选择对应的数据库类型，目前仅支持MySQL、SQL server
启动后台：打开项目根目录的startup.bat即可启动（数据库默认MySQL）
启动前端：打开ZR.Vue文件夹，运行npm install命令，再运行npm run serve启动
浏览器访问：http://localhost:8887 （默认前端端口为：8887，后台端口为：8888）


## 🍖内置功能

1. [X] 用户管理：用户是系统操作者，该功能主要完成系统用户配置。
2. [X] 部门管理：配置系统组织机构（公司、部门、小组），树结构展现。
3. [X] 岗位管理：配置系统用户所属担任职务。
4. [X] 菜单管理：配置系统菜单，操作权限，按钮权限标识等。
5. [X] 角色管理：角色菜单权限分配。
6. [X] 字典管理：对系统中经常使用的一些较为固定的数据进行维护。
7. [ ] 参数管理：对系统动态配置常用参数。
8. [X] 操作日志：系统正常操作日志记录和查询；系统异常信息日志记录和查询。
9. [X] 登录日志：系统登录日志记录查询包含登录异常。
10. [ ] 在线用户：当前系统中活跃用户状态监控。
11. [X] 系统接口：使用swagger生成相关api接口文档。
12. [X] 服务监控：监视当前系统CPU、内存、磁盘、堆栈等相关信息。
13. [X] 在线构建器：拖动表单元素生成相应的VUE代码。
14. [X] 任务系统：基于Quartz.NET定时任务执行。
15. [X] 文章管理：可以写文章记录。
16. [X] 代码生成：可以一键生成前后端代码(.cs、.vue、.js)，自定义配置前端展示控件、让开发更快捷高效。

## 项目结构

ZRAdmin解决方案包含：

ZR.Service[服务层类库]：提供WebApi接口调用；

ZR.Tasks[定时任务类库]：提供项目定时任务实现功能；

ZR.Repository[仓库层类库]：方便提供有执行存储过程的操作；

ZR.Model[实体层类库]，提供项目中的数据库表、数据传输对象；

ZR.CodeGenerator[代码生成功能]，包含代码生成的模板、方法、代码生成的下载。

ZR.Admin.WebApi[webapi接口]：为Vue版或其他三方系统提供接口服务。

ZR.Vue[前端UI]：vue版本UI层。

DataBase是最新数据库备份文件，目前支持MS SQL Server和MySql。


## 🍎演示图

<table>
    <tr>
        <td><img src="https://www.izhaorui.cn/images/zradmin/1.png"/></td>
        <td><img src="https://www.izhaorui.cn/images/zradmin/2.png"/></td>
    </tr>
    <tr>
        <td><img src="https://www.izhaorui.cn/images/zradmin/3.png"/></td>
        <td><img src="https://www.izhaorui.cn/images/zradmin/4.png"/></td>
    </tr>
    <tr>
        <td><img src="https://www.izhaorui.cn/images/zradmin/5.png"/></td>
        <td><img src="https://www.izhaorui.cn/images/zradmin/6.png"/></td>
    </tr>
	<tr>
        <td><img src="https://www.izhaorui.cn/images/zradmin/7.png"/></td>
        <td><img src="https://www.izhaorui.cn/images/zradmin/8.png"/></td>
    </tr>	
	<tr>
        <td><img src="https://www.izhaorui.cn/images/zradmin/9.png"/></td>
        <td><img src="https://www.izhaorui.cn/images/zradmin/10.png"/></td>
    </tr>
	<tr>
        <td><img src="https://www.izhaorui.cn/images/zradmin/11.png"/></td>
        <td><img src="https://www.izhaorui.cn/images/zradmin/12.png"/></td>
    </tr>
	<tr>
        <td><img src="https://www.izhaorui.cn/images/zradmin/13.png"/></td>
        <td><img src="https://www.izhaorui.cn/images/zradmin/14.png"/></td>
    </tr>
	<tr>
        <td><img src="https://www.izhaorui.cn/images/zradmin/15.png"/></td>
        <td><img src="https://www.izhaorui.cn/images/zradmin/16.png"/></td>
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
- 👉SqlSugar：https://gitee.com/dotnetchina/SqlSugar

## 🍻参与贡献
- Fork 本项目
- 新建 Feat_xxx 分支
- 提交代码
- 新建 Pull Request

## 联系作者
QQ：599854767

## 🎀捐赠
如果这个项目对您有所帮助，请扫下方二维码打赏一杯咖啡。
<img src="https://www.izhaorui.cn/static/pay.jpg"/>