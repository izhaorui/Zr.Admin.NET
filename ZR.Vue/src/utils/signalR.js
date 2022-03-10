// 官方文档：https://docs.microsoft.com/zh-cn/aspnet/core/signalr/javascript-client?view=aspnetcore-6.0&viewFallbackFrom=aspnetcore-2.2&tabs=visual-studio
import * as signalR from '@microsoft/signalr'
import store from '../store'
import { getToken } from '@/utils/auth'
import { Notification } from 'element-ui'

export default {
  // signalR对象
  SR: {},
  // 失败连接重试次数
  failNum: 4,
  baseUrl: '',
  init(url) {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(url, { accessTokenFactory: () => getToken() })
      .withAutomaticReconnect()//自动重新连接
      .configureLogging(signalR.LogLevel.Information)
      .build();
    this.SR = connection;
    // 断线重连
    connection.onclose(async () => {
      console.log('断开连接了')
			console.assert(connection.state === signalR.HubConnectionState.Disconnected);
			// 建议用户重新刷新浏览器
      await this.start();
    })

    connection.onreconnected(() => {
      console.log('断线重新连接成功')
    })
    this.receiveMsg(connection);
    // 启动
    // this.start();
  },
  /**
   * 调用 this.signalR.start().then(async () => { await this.SR.invoke("method")})
   * @returns 
   */
  async start() {
    var that = this;

    try {
      //使用async和await 或 promise的then 和catch 处理来自服务端的异常
      await this.SR.start();
      //console.assert(this.SR.state === signalR.HubConnectionState.Connected);
      console.log('signalR 连接成功了', this.SR.state);
      return true;
    } catch (error) {
      that.failNum--;
      console.log(`失败重试剩余次数${that.failNum}`, error)
      if (that.failNum > 0) {
        setTimeout(async () => {
          await this.SR.start()
        }, 5000);
      }
      return false;
    }
  },
	// 接收消息处理
  receiveMsg(connection) {
    connection.on("onlineNum", (data) => {
      store.dispatch("socket/changeOnlineNum", data);
    });
    // 接收欢迎语
    connection.on("welcome", (data) => {
      console.log('welcome', data)
      Notification.info(data)
    });
    // 接收后台手动推送消息
    connection.on("receiveNotice", (title, data) => {
      Notification({
        type: 'info',
        title: title,
        message: data,
        dangerouslyUseHTMLString: true,
        duration: 0
      })
    })
    // 接收系统通知/公告
    connection.on("moreNotice", (data) => {
      if (data.code == 200) {
        store.dispatch("socket/getNoticeList", data.data);
      }
    })
  }
}