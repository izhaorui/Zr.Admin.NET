// 官方文档：https://docs.microsoft.com/zh-cn/aspnet/core/signalr/javascript-client?view=aspnetcore-6.0&viewFallbackFrom=aspnetcore-2.2&tabs=visual-studio
import * as signalR from '@microsoft/signalr'
import store from '../store'

export default {
  // signalR对象
  SR: {},
  // 失败连接重试次数
  failNum: 4,
  baseUrl: '',
  init(url) {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(url)
      .build();
    // console.log('conn', connection);
    this.SR = connection;

    // 断线重连
    connection.onclose(async () => {
      await this.SR.start();
    })

    connection.on("onlineNum", (data) => {
			store.dispatch("socket/changeOnlineNum", data);
    });
    // 启动
    this.start();
  },
  async start() {
    var that = this;

    try {
      //使用async和await 或 promise的then 和catch 处理来自服务端的异常

      await this.SR.start();

      //console.assert(this.SR.state === signalR.HubConnectionState.Connected);
      console.log('signalR 连接成功了', this.SR.state);
    } catch (error) {
      that.failNum--;
      console.log(`失败重试剩余次数${that.failNum}`, error)
      if (that.failNum > 0) {
        setTimeout(async () => {
          await this.SR.start()
        }, 5000);
      }
    }
  }
}