## 开发

```bash
# 克隆项目
git clone https://gitee.com/y_project/RuoYi-Vue

# 进入项目目录
cd ruoyi-ui

# 安装依赖
npm install

# 建议不要直接使用 cnpm 安装依赖，会有各种诡异的 bug。可以通过如下操作解决 npm 下载速度慢的问题
npm install --registry=https://registry.npm.taobao.org

# 启动服务
npm run dev
```

浏览器访问 http://localhost:80

## 发布

```bash
# 构建测试环境
npm run build:stage

# 构建生产环境
npm run build:prod
```

## nginx配置

``` shell
server {
    listen 8010;
	#绑定多个域名
    server_name 127.0.0.1;

    # charset koi8-r;
    access_log  logs/logs.access.log  main;

	# 后端接口 生产环境
	location /prod-api/ {
		proxy_pass  http://localhost:8085/;
       
		# 后端的Web服务器可以通过X-Forwarded-For获取用户真实IP
		proxy_set_header Host $host;
		proxy_set_header X-Real-IP $remote_addr;
		proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
		
		# 如果请求被负载均衡的服务器返回类似500这样的，将继续请求下一台应用服务器，默认 对post,lock,patch的请求不进行重试，如果要设置在后面添加 non_idemponent
		#	proxy_next_upstream error timeout invalid_header http_500 http_502 http_503 http_504;
    }
	
    # vue 项目
    location / {
      root html/dist;
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