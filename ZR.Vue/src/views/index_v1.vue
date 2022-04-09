<template>
  <div class="dashboard-editor-container home">
    <!-- 用户信息 -->
    <el-row :gutter="15">
      <el-col :md="24" :lg="16" :xl="16" class="mb10">
        <el-card shadow="hover">
          <div slot="header">
            <span>我的工作台</span>
          </div>
          <div class="user-item">
            <div class="user-item-left">
              <img :src="userInfo.avatar" />
            </div>

            <div class="user-item-right overflow">
              <el-row>
                <el-col :xs="24" :md="24" class="right-title mb20 one-text-overflow">
                  {{ userInfo.welcomeMessage }}，{{ userInfo.nickName }}，{{ userInfo.welcomeContent }}</el-col>
                <el-col :xs="24" :sm="24" :md="24">
                  <el-col :xs="24" :md="8" class="right-l-v">
                    <div class="right-label">昵称：</div>
                    <div class="right-value">{{ userInfo.nickName }}</div>
                  </el-col>
                  <el-col :xs="24" :md="16" class="right-l-v">
                    <div class="right-label">身份：</div>
                    <div class="right-value">
                      <span v-for="item in userInfo.roles" :key="item.roleId">{{ item.roleName }}</span>
                    </div>
                  </el-col>
                </el-col>
                <el-col :md="24" class="mt10">
                  <el-col :xs="24" :sm="12" :md="8" class="right-l-v">
                    <div class="right-label one-text-overflow">IP：</div>
                    <div class="right-value one-text-overflow">{{ userInfo.loginIP }}</div>
                  </el-col>
                  <el-col :xs="24" :sm="12" :md="16" class="right-l-v">
                    <div class="right-label one-text-overflow">时间：</div>
                    <div class="right-value one-text-overflow">{{ currentTime }}</div>
                  </el-col>
                </el-col>
                <el-col :lg="24" class="mt10">
                  <el-button size="small" icon="el-icon-edit-outline">
                    <router-link to="/user/profile">修改信息</router-link>
                  </el-button>
                  <!-- <el-button size="small" icon="el-icon-position" type="primary">发布活动</el-button> -->
                </el-col>
              </el-row>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :md="24" :lg="8" :xl="8" class="mb10">
        <el-card shadow="hover">
          <div slot="header">
            <span>最新文章</span>
            <el-button class="home-card-more" type="text" @click="onOpenGitee">更多</el-button>
          </div>
          <div class="info">
            <Scroll :data="newArticleList" class="info-scroll" :class-option="optionSingleHeight">
              <ul class="info-ul">
                <li v-for="(v, k) in newArticleList" :key="k" class="info-item">
                  <div class="info-item-left" v-text="v.title"></div>
                  <div class="info-item-right" v-text="parseTime(v.updateTime, '{m}/{d}')"></div>
                </li>
              </ul>
            </Scroll>
          </div>
        </el-card>
      </el-col>
    </el-row>
    <panel-group @handleSetLineChartData="handleSetLineChartData" />

    <el-row style="background:#fff;padding:16px 16px 0;margin-bottom:32px;">
      <line-chart :chart-data="lineChartData" />
    </el-row>

    <el-row :gutter="32">
      <el-col :xs="24" :sm="24" :lg="8">
        <div class="chart-wrapper">
          <raddar-chart />
        </div>
      </el-col>
      <el-col :xs="24" :sm="24" :lg="8">
        <div class="chart-wrapper">
          <pie-chart />
        </div>
      </el-col>
      <el-col :xs="24" :sm="24" :lg="8">
        <div class="chart-wrapper">
          <bar-chart />
        </div>
      </el-col>
    </el-row>
  </div>
</template>

<script>
import PanelGroup from './dashboard/PanelGroup'
import LineChart from './dashboard/LineChart'
import RaddarChart from './dashboard/RaddarChart'
import PieChart from './dashboard/PieChart'
import BarChart from './dashboard/BarChart'
import Scroll from 'vue-seamless-scroll'
import { listNewArticle } from '@/api/system/article.js'

const lineChartData = {
  newVisitis: {
    expectedData: [100, 120, 161, 134, 105, 160, 165],
    actualData: [120, 82, 91, 154, 162, 140, 145]
  },
  messages: {
    expectedData: [200, 192, 120, 144, 160, 130, 140],
    actualData: [180, 160, 151, 106, 145, 150, 130]
  },
  purchases: {
    expectedData: [80, 100, 121, 104, 105, 90, 100],
    actualData: [120, 90, 100, 138, 142, 130, 130]
  },
  shoppings: {
    expectedData: [130, 140, 141, 142, 145, 150, 160],
    actualData: [120, 82, 91, 154, 162, 140, 130]
  }
}
export default {
  name: 'Index',
  components: {
    PanelGroup,
    LineChart,
    RaddarChart,
    PieChart,
    BarChart,
    Scroll
  },
  computed: {
    photo() {
      return this.$store.getters.photo
    },
    userInfo() {
      return this.$store.getters.userinfo
    },
    currentTime() {
      return this.parseTime(new Date())
    },
    optionSingleHeight() {
      return {
        step: 0.2, // 数值越大速度滚动越快
        limitMoveNum: 2, // 开始无缝滚动的数据量 this.dataList.length
        hoverStop: true, // 是否开启鼠标悬停stop
        direction: 1, // 0向下 1向上 2向左 3向右
        openWatch: true, // 开启数据实时监控刷新dom
        singleHeight: 0, // 单步运动停止的高度(默认值0是无缝不停止的滚动) direction => 0/1
        singleWidth: 0, // 单步运动停止的宽度(默认值0是无缝不停止的滚动) direction => 2/3
        waitTime: 1000 // 单步运动停止的时间(默认值1000ms)
      }
    }
  },
  data() {
    return {
      lineChartData: lineChartData.newVisitis,
      newArticleList: []
    }
  },
  created() {
    listNewArticle().then((res) => {
      this.newArticleList = res.data
    })
  },
  methods: {
    handleSetLineChartData(type) {
      this.lineChartData = lineChartData[type]
    },
    onOpenGitee() {}
  }
}
</script>

<style lang="scss" scoped>
.home {
  width: 100%;
  overflow: hidden;
  .home-card-more {
    float: right;
    padding: 3px 0;
    font-size: 13px;
  }
  .home-card-time {
    float: right;
    font-size: 13px;
    width: 130px;
    margin-top: -4px;
  }
  .user-item {
    height: 198px;
    display: flex;
    align-items: center;
    .user-item-left {
      width: 100px;
      height: 130px;
      border-radius: 4px;
      overflow: hidden;
      img {
        width: 100%;
        height: 100%;
      }
    }
    .user-item-right {
      flex: 1;
      padding: 15px;
      .right-title {
        font-size: 20px;
      }
      .right-l-v {
        font-size: 13px;
        display: flex;
        .right-label {
          color: gray;
          width: 40px;
        }
        .right-value {
          flex: 1;
        }
      }
    }
  }
  .info {
    height: 198px;
    .info-scroll {
      height: 100%;
      overflow: hidden;
      .info-ul {
        list-style: none;
        padding: 0;
        .info-item {
          display: flex;
          font-size: 13px;
          color: gray;
          height: 28px;
          line-height: 28px;
          &:hover {
            color: var(--color-primary);
            cursor: pointer;
          }
          .info-item-left {
            flex: 1;
            flex-shrink: 0;
            text-overflow: ellipsis;
            white-space: nowrap;
            overflow: hidden;
          }
          .info-item-right {
            width: 60px;
            text-align: right;
          }
        }
      }
    }
  }
  .home-recommend-row {
    .home-recommend {
      position: relative;
      height: 100px;
      color: #ffffff;
      border-radius: 4px;
      overflow: hidden;
      cursor: pointer;
      &:hover {
        i {
          right: 0px !important;
          bottom: 0px !important;
          transition: all ease 0.3s;
        }
      }
      i {
        position: absolute;
        right: -10px;
        bottom: -10px;
        font-size: 70px;
        transform: rotate(-30deg);
        transition: all ease 0.3s;
      }
      .home-recommend-auto {
        padding: 15px;
        position: absolute;
        left: 0;
        top: 5%;
        .home-recommend-msg {
          font-size: 12px;
          margin-top: 10px;
        }
      }
    }
  }
  .charts {
    width: 100%;
    height: 282.6px;
    display: flex;
    padding: 12px 15px;
    .charts-left {
      flex: 1;
      height: 100%;
    }
    .charts-right {
      flex: 1;
      height: 100%;
    }
  }
  .home-charts {
    height: 282.6px;
    .home-charts-item {
      background-color: #f5f5f5;
      padding: 19px 15px;
      border-radius: 2px;
      display: flex;
      align-items: center;
      margin-bottom: 12px;
      cursor: pointer;
      &:last-of-type {
        margin-bottom: 0;
      }
      &:hover {
        .home-charts-item-right {
          i {
            transform: rotate(45deg);
            transition: all ease 0.3s;
          }
        }
      }
      .home-charts-item-left {
        flex: 1;
        .home-charts-item-title {
          font-size: 13px;
        }
        .home-charts-item-num {
          font-size: 20px;
          margin-top: 5px;
        }
      }
      .home-charts-item-right {
        i {
          font-size: 20px;
          padding: 8px;
          border-radius: 100%;
          transition: all ease 0.3s;
        }
      }
    }
  }
}

.dashboard-editor-container {
  padding: 18px;
  background-color: rgb(240, 242, 245);
  position: relative;

  .chart-wrapper {
    background: #fff;
    padding: 16px 16px 0;
    margin-bottom: 32px;
  }
}

@media (max-width: 1024px) {
  .chart-wrapper {
    padding: 8px;
  }
}
</style>
