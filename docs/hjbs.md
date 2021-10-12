# 环境部署
## 准备工作

```
.Net >= 5.0 
MySql >= 5.7.0
SqlServer >= 2008(推荐2012以上)
Node >= 10
```

!> 前端安装完node后，最好设置下淘宝镜像源，不建议使用cnpm(可能会出现奇怪的问题)
``` node
npm install --registry=https://registry.npm.taobao.org
```

## 运行系统

前往 Gitee 下载页面(https://gitee.com/izory/ZrAdminNetCore) 下载本项目

## 后端运行
1. 创建数据库 zrAdmin 将项目根目录下面的document文件夹下的选择对应的数据库类型脚本admin-xxxx.sql并导入数据脚本
2. 修改appsettings.json配置文件中的conn_zrAdmin数据库连接字符串以及 - conn_zrAdmin_Type选择对应的数据库类型，目前仅支持MySQL、SQL server
3. 打开项目运行F5 ，出现如下图表示启动成功。
4. 通过项目根目录中的startup.bat启动

``` C#
info: Microsoft.Hosting.Lifetime[0]
      Now listening on: http://localhost:8888
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
10-11 17:51:31 | INFO | Microsoft.Hosting.Lifetime |  |  | Now listening on: http://localhost:8888
info10-11 17:51:31 | INFO | Microsoft.Hosting.Lifetime |  |  | Application started. Press Ctrl+C to shut down.
10-11 17:51:31 | INFO | Microsoft.Hosting.Lifetime |  |  | Hosting environment: Stage
10-11 17:51:31 | INFO | Microsoft.Hosting.Lifetime |  |  | Content root path: F:\ZRAdmin.NET\ZR.Admin.WebApi
```

!> 后端运行成功可以通过(http://localhost:8888) 访问，但是不会出现静态页面，可以继续参考下面步骤部署前端，然后通过前端地址来访问。

## 前端运行
``` node
# 进入项目目录
cd ZR.Vue

# 安装依赖
npm install

# 强烈建议不要用直接使用 cnpm 安装，会有各种诡异的 bug，可以通过重新指定 registry 来解决 npm 安装速度慢的问题。
npm install --registry=https://registry.npm.taobao.org

# 本地开发 启动项目
npm run dev
```
4. 打开浏览器，输入：(http://localhost:8887 ) 默认账户/密码 admin/123456）
若能正确展示登录页面，并能成功登录，菜单及页面展示正常，则表明环境搭建成功

## 必要配置
- 修改数据库连接编辑ZR.Admin.WebApi目录下的appsettings.json 配置

``` json
{
  "ConnectionStrings": {
    "conn_zrAdmin": "server=127.0.0.1;user=zr;pwd=abc123;database=zrAdmin", //修改成你的数据库连接字符串
  },
  "conn_zrAdmin_type": 0, //选择对应的数据库类型MySql = 0, SqlServer = 1
}  
```
- 跨域配置
```json
{
  "sysConfig": {
    "tokenExpire": 1440, //Jwt token超时时间（分）
    "cors": "http://localhost:8887" //跨域地址，配置前端启动地址多个用","隔开
  }
}  
```

## 部署系统
### 后端部署
- 发布WebApi项目文件
vs打开项目后 右键 ZR.Admin.WebApi 选择发布即可，将发布后的文件拷贝到服务器上

### 前端部署
``` sh
# 打包正式环境
npm run build:prod

# 打包预发布环境
npm run build:stage
```

构建打包成功之后，会在根目录生成 dist 文件夹，里面就是构建打包好的文件，通常是 ***.js 、***.css、index.html 等静态文件。

通常情况下 dist 文件夹的静态文件发布到你的 nginx 或者静态服务器即可，其中的 index.html 是后台服务的入口页面。

## 环境变量
所有测试环境或者正式环境变量的配置都在 .env.development等 .env.xxxx文件中。

它们都会通过 webpack.DefinePlugin 插件注入到全局。

!> 环境变量必须以VUE_APP_为开头。如:VUE_APP_API、VUE_APP_TITLE<br/>
 你在代码中可以通过如下方式获取:</br>
 console.log(process.env.VUE_APP_xxxx)


## Nginx配置
1. 创建文件 zradmin.ini 添加以下内容

```nginx
server {
	#修改要监听的端口
  listen 8080;
	#修改要绑定的域名或IP地址
  server_name localhost;

  # charset koi8-r;
  access_log  logs/logs.access.log  main;

	# 后端接口 生产环境
	location /prod-api/ {
		proxy_pass  http://localhost:8888/;
       
		# 后端的Web服务器可以通过X-Forwarded-For获取用户真实IP
		proxy_set_header Host $host;
		proxy_set_header X-Real-IP $remote_addr;
		proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
		
		# 如果请求被负载均衡的服务器返回类似500这样的，将继续请求下一台应用服务器，默认 对post,lock,patch的请求不进行重试，如果要设置在后面添加 non_idemponent
		#	proxy_next_upstream error timeout invalid_header http_500 http_502 http_503 http_504;
    }
	
	# vue项目配置
	location / {
		#将xxxxx路径改成你的发布路径
		root html/zradmin_vue;
		index index.html;
		try_files $uri $uri/ /index.html;
	}
	
    error_page 404              /404.html;

    # redirect server error pages to the static page /50x.html
    error_page 500 502 503 504  /50x.html;
    location = /50x.html {
        root html;
    }
}
```
2. 在nginx安装目录中的html目录中创建文件夹zradmin_vue

4. 将ZR.Vue项目的dist文件中的文件拷贝到刚刚创建的zradmin_vue中
5. 浏览器中访问 http://localhost:8080 

!> 服务器防火墙的设置开放8080 端口