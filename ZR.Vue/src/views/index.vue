<template>
  <div class="app-container home">
    <el-row :gutter="20">
      <el-col :sm="24" :lg="24">
        <blockquote class="text-warning" style="font-size: 14px">
          领取七牛云通用云产品优惠券
          <br />
          <el-link href="https://s.qiniu.com/FzEfay" type="primary" target="_blank">
            https://s.qiniu.com/FzEfay</el-link>
          <br />

          领取腾讯云通用云产品新用户专属大礼包2860优惠券，每种代金券限量500张，先到先得。
          <br />
          <el-link href="https://curl.qcloud.com/5J4nag8D" type="primary" target="_blank">https://curl.qcloud.com/5J4nag8D</el-link>
          <br />

          腾讯云限时秒杀活动<br>
          <el-link href="https://curl.qcloud.com/4yEoRquq" type="primary" target="_blank">https://curl.qcloud.com/4yEoRquq</el-link>
					<br />

          华为特惠专区，多款产品限时特价
          <el-link
            href="https://activity.huaweicloud.com/discount_area_v5/index.html?fromacct=a53709d1-149d-49f4-9b89-bf62bd96ef65&utm_source=aGlkX3N0dnZkMWNxejBnOTJ5OA===&utm_medium=cps&utm_campaign=201905"
            type="primary" target="_blank">☛☛点我进入☚☚</el-link>
					<h4 class="text-danger">
            云产品通用红包，可叠加官网常规优惠使用。(仅限新用户)
          </h4>
        </blockquote>
      </el-col>
    </el-row>
    <el-row :gutter="20">
      <el-col :lg="16" :sm="24">
        <h2>ZRAdmin.NET后台管理框架</h2>
        <p>ZRAdmin.NET借鉴了很多开源项目的优点，让你开发Web管理系统更简单，所以我也把它给开源了（前端 <code>vue页面</code>主要参考若依，在此表示感谢.)</p>
        <p>代码完全免费开源，易读易懂、界面简洁美观，给你的项目多一种选择与参考。</p>
        <p>
          <b>当前版本:</b> <span>v{{ version }}</span>
          <el-link class="ml10" type="primary" size="mini" icon="el-icon-document" plain
            @click="goTarget('http://www.izhaorui.cn/doc/changelog.html#' + version)">更新日志
          </el-link>
        </p>
        <p>
          <el-button type="primary" size="mini" icon="el-icon-cloudy" plain @click="goTarget('https://gitee.com/izory/ZrAdminNetCore')">访问码云
          </el-button>
          <el-button type="primary" size="mini" icon="el-icon-cloudy" plain @click="goTarget('https://github.com/izhaorui/ZrAdmin.NET')">Github
          </el-button>
        </p>
        <p>
        </p>
        <h3>如果觉得不错欢迎给个⭐Star⭐收藏一下 ，这样作者才有继续免费下去的动力，谢谢！</h3>
      </el-col>
      <el-col :sm="24" :lg="8">
        <el-row>
          <h2>技术选型</h2>
          <el-col :span="10">
            <h4>后端技术</h4>
            <ul>
              <li>NET5</li>
              <li>JWT</li>
              <li>SqlSugar</li>
              <li>Quartz.Net</li>
              <li>MySql</li>
              <li>Mapster</li>
              <li>Epplus</li>
              <li>Signalr</li>
              <li>...</li>
            </ul>
          </el-col>
          <el-col :span="10">
            <h4>前端技术</h4>
            <ul>
              <li>Vue</li>
              <li>Vuex</li>
              <li>Element-ui</li>
              <li>Axios</li>
              <li>Sass</li>
              <li>Quill</li>
              <li>...</li>
            </ul>
          </el-col>
        </el-row>
      </el-col>
    </el-row>
    <el-divider />
    <el-row :gutter="20" class="mt10">
      <el-col :sm="24" :lg="8">
        <el-card>
          <div slot="header" class="clearfix">
            <span>联系信息</span>
          </div>
          <div class="body">
            <p><i class="el-icon-s-promotion"></i>官网：
              <el-link href="http://www.izhaorui.cn/doc" target="_blank">http://www.izhaorui.cn/doc</el-link>
            </p>
          </div>
          <div class="body">
            <p><i class="el-icon-user-solid"></i>QQ群：<a
                href="https://qm.qq.com/cgi-bin/qm/qr?k=Y__-fTGo_K2UIo3nWz7QnvS8LoRfPWKm&authKey=/ldXxiuolv80PF4yC8VtLk/TvAYbIhm2LKP8YVHCxAk+x2I+iqPAM1H/IsxQ+0gC&noverify=0"
                target="_black">191349103</a></p>
          </div>
        </el-card>
      </el-col>
      <el-col :sm="24" :lg="8">
        <el-card>
          <div slot="header" class="clearfix">
            <span>捐赠支持</span>
          </div>
          <div class="body">
            <div style="color:red">打赏作者喝杯咖啡表示鼓励</div>
            <img src="@/assets/image/reward.jpg" alt="donate" width="100%" />
          </div>
        </el-card>
      </el-col>
      <el-col :span="10">
        <!-- <el-card>
          <div slot="header" class="clearfix">
            <span></span>
          </div>
        </el-card> -->
      </el-col>
    </el-row>
    <el-divider />
  </div>
</template>

<script>
const defaultSettings = require("@/settings.js");
export default {
  name: "index",
  data() {
    return {
      // 版本号
      version: defaultSettings.version,
    };
  },
  methods: {
    goTarget(href) {
      window.open(href, "_blank");
    },
  },
};
</script>

<style scoped lang="scss">
.tip {
  color: red;
  display: inline-block;
  height: 30px;
  line-height: 30px;
}
.home {
  blockquote {
    padding: 10px 20px;
    margin: 0 0 20px;
    font-size: 17.5px;
    border-left: 5px solid #eee;
  }
  hr {
    margin-top: 20px;
    margin-bottom: 20px;
    border: 0;
    border-top: 1px solid #eee;
  }
  .col-item {
    margin-bottom: 20px;
  }

  ul {
    padding: 0;
    margin: 0;
  }

  font-family: "open sans", "Helvetica Neue", Helvetica, Arial, sans-serif;
  font-size: 13px;
  color: #676a6c;
  overflow-x: hidden;

  ul {
    list-style-type: none;
  }

  h4 {
    margin-top: 0px;
  }

  h2 {
    margin-top: 10px;
    font-size: 26px;
    font-weight: 100;
  }

  p {
    margin-top: 10px;

    b {
      font-weight: 700;
    }
  }

  .update-log {
    ol {
      display: block;
      list-style-type: decimal;
      margin-block-start: 1em;
      margin-block-end: 1em;
      margin-inline-start: 0;
      margin-inline-end: 0;
      padding-inline-start: 40px;
    }
  }
}
</style>

