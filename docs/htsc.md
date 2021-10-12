## 👉代码生成
-  修改配置文件appsettings.json
``` json
"gen": {
    "conn": "server=127.0.0.1;user=zr;pwd=abc;database={database}", //代码生成数据库连接字符串
    "dbType": 0, //MySql = 0, SqlServer = 1
    "autoPre": true, //自动去除表前缀
    "author": "zr",
    "tablePrefix": "sys_" //"表前缀（生成类名不会包含表前缀，多个用","分隔）",
  }
```
修改conn数据库连接字符串其中{database}为动态要替换的数据库名
<img src="http://ss.izhaorui.cn/zradmin/15-1.png"/>


👉视频教程
- [观看视频](http://ss.izhaorui.cn/zradmin/%E4%BB%A3%E7%A0%81%E7%94%9F%E6%88%90%E6%BC%94%E7%A4%BA202109250747.mp4)
