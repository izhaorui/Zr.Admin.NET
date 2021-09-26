## nginx部署文档.md

1. 拷贝zradmin.ini文件代码到nginx配置文件http模块中
2. 在nginx安装目录中的html目录中创建文件夹zradmin_vue
3. 发布ZR.Admin.WebApi项目并部署到服务器中启动
4. 发布ZR.Vue项目并拷贝dist文件中的代码到刚刚创建的zradmin_vue中
5. 浏览器中访问 http://localhost:8080 

注意：服务器防火墙的设置开放8080 端口