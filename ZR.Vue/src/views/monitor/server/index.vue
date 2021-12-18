<template>
  <div class="app-container">
    <el-row>
      <el-col :lg="12" class="card-box">
        <el-card>
          <div slot="header"><span>CPU</span></div>
          <div class="el-table el-table--enable-row-hover el-table--medium">
            <table cellspacing="0" style="width: 100%;">
              <thead>
                <tr>
                  <th class="is-leaf">
                    属性
                  </th>
                  <th class="is-leaf">
                    值
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>
                    <div class="cell">核心数</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.sys">{{ server.sys.cpuNum }}</div>
                  </td>
                </tr>
                <!-- <tr>
                  <td><div class="cell">用户使用率</div></td>
                  <td><div class="cell" v-if="server.cpu">{{ server.cpu.used }}%</div></td>
                </tr> -->
                <tr>
                  <td>
                    <div class="cell">cpu使用率</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.cpu">{{ server.cpu.cpuRate }}</div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </el-card>
      </el-col>

      <el-col :lg="12" class="card-box">
        <el-card>
          <div slot="header"><span>内存</span></div>
          <div class="el-table el-table--enable-row-hover el-table--medium">
            <table cellspacing="0" style="width: 100%;">
              <thead>
                <tr>
                  <th class="is-leaf">
                    属性
                  </th>
                  <th class="is-leaf">
                    内存
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>
                    <div class="cell">总内存</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.cpu">{{ server.cpu.totalRAM }}</div>
                  </td>
                </tr>
                <tr>
                  <td>
                    <div class="cell">内存使用率</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.cpu">{{ server.cpu.ramRate}}</div>
                  </td>
                </tr>
                <!-- <tr>
                  <td><div class="cell">运行时间</div></td>
                  <td><div class="cell" v-if="server.cpu">{{ server.cpu.runTime }}</div></td>
                </tr> -->

              </tbody>
            </table>
          </div>
        </el-card>
      </el-col>

      <el-col :lg="24" class="card-box">
        <el-card>
          <div slot="header">
            <span>服务器信息</span>
          </div>
          <div class="el-table el-table--enable-row-hover el-table--medium">
            <table cellspacing="0" style="width: 100%;">
              <tbody>
                <tr>
                  <td>
                    <div class="cell">服务器名称</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.sys">{{ server.sys.computerName }}</div>
                  </td>
                  <td>
                    <div class="cell">操作系统</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.sys">{{ server.sys.osName }}</div>
                  </td>
                </tr>
                <tr>
                  <td>
                    <div class="cell">服务器IP</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.sys">{{ server.sys.serverIP }}</div>
                  </td>
                  <td>
                    <div class="cell">系统架构</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.sys">{{ server.sys.osArch }}</div>
                  </td>
                </tr>
                <tr>
                  <td>
                    <div class="cell">系统运行时长</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.cpu">{{ server.cpu.runTime }}</div>
                  </td>
                  <td>
                    <div class="cell"></div>
                  </td>
                  <td>
                    <div class="cell"></div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </el-card>
      </el-col>

      <el-col :lg="24" class="card-box">
        <el-card>
          <div slot="header">
            <span>.NET Core信息</span>
          </div>
          <div class="el-table el-table--enable-row-hover el-table--medium">
            <table cellspacing="0" style="width: 100%;">
              <tbody>
                <tr>
                  <td>
                    <div class="cell">环境变量</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.app">{{ server.app.name }}</div>
                  </td>
                  <td>
                    <div class="cell">.Net版本</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.app">{{ server.app.version }}</div>
                  </td>
                </tr>
                <tr>
                  <td>
                    <div class="cell">启动时间</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.app">{{ server.app.startTime }}</div>
                  </td>
                  <td>
                    <div class="cell">运行时长</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.app">{{ server.app.runTime }}</div>
                  </td>
                </tr>
                <tr>
                  <td>
                    <div class="cell">占用内存</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.app">{{ server.app.appRAM }}</div>
                  </td>
                  <td>
                    <div class="cell">启动地址</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.app">{{server.app.host}}</div>
                  </td>
                </tr>
                <tr>
                  <td>
                    <div class="cell">ContentRootPath</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.app">{{ server.app.rootPath }}</div>
                  </td>
                  <td>
                    <div class="cell">webPath</div>
                  </td>
                  <td>
                    <div class="cell" v-if="server.app">{{ server.app.webRootPath }}</div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </el-card>
      </el-col>
      <!--
      <el-col :span="24" class="card-box">
        <el-card>
          <div slot="header">
            <span>磁盘状态</span>
          </div>
          <div class="el-table el-table--enable-row-hover el-table--medium">
            <table cellspacing="0" style="width: 100%;">
              <thead>
                <tr>
                  <th class="is-leaf">
                    <div class="cell">盘符路径</div>
                  </th>
                  <th class="is-leaf">
                    <div class="cell">文件系统</div>
                  </th>
                  <th class="is-leaf">
                    <div class="cell">盘符类型</div>
                  </th>
                  <th class="is-leaf">
                    <div class="cell">总大小</div>
                  </th>
                  <th class="is-leaf">
                    <div class="cell">可用大小</div>
                  </th>
                  <th class="is-leaf">
                    <div class="cell">已用大小</div>
                  </th>
                  <th class="is-leaf">
                    <div class="cell">已用百分比</div>
                  </th>
                </tr>
              </thead>
              <tbody v-if="server.sysFiles">
                <tr v-for="sysFile in server.sysFiles">
                  <td>
                    <div class="cell">{{ sysFile.dirName }}</div>
                  </td>
                  <td>
                    <div class="cell">{{ sysFile.sysTypeName }}</div>
                  </td>
                  <td>
                    <div class="cell">{{ sysFile.typeName }}</div>
                  </td>
                  <td>
                    <div class="cell">{{ sysFile.total }}</div>
                  </td>
                  <td>
                    <div class="cell">{{ sysFile.free }}</div>
                  </td>
                  <td>
                    <div class="cell">{{ sysFile.used }}</div>
                  </td>
                  <td>
                    <div class="cell" :class="{'text-danger': sysFile.usage > 80}">{{ sysFile.usage }}%</div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </el-card>
      </el-col> -->
    </el-row>
  </div>
</template>

<script>
import { getServer } from "@/api/monitor/server";

export default {
  name: "Server",
  data() {
    return {
      // 加载层信息
      loading: [],
      // 服务器信息
      server: [],
      intervalId: null,
    };
  },
  created() {
    this.getList();
    this.openLoading();
    this.dataRefreh();
  },
  methods: {
    /** 查询服务器信息 */
    getList() {
      getServer()
        .then((response) => {
          this.server = response.data;
          this.loading.close();
        })
        .catch((err) => {
          this.loading.close();
        });
    },
    // 打开加载层
    openLoading() {
      this.loading = this.$loading({
        lock: true,
        text: "拼命读取中",
        spinner: "el-icon-loading",
        background: "rgba(0, 0, 0, 0.7)",
      });
    },
    dataRefreh() {
      if (this.intervalId != null) {
        return;
      }
      this.intervalId = setInterval(() => {
        this.getList();
      }, 5000);
    },
    /**
     * 停止定时器
     */
    clear() {
      clearInterval(this.intervalId);
      this.intervalId = null;
    },
  },
  destroyed() {
    this.clear();
  },
};
</script>
<style scoped>
table tr {
  height: 30px;
}
.is-leaf {
  text-align: left;
  padding: 0 10px;
}
</style>