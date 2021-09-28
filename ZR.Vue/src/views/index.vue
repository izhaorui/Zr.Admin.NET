<template>
  <div class="app-container home">
    <el-row :gutter="20">
      <el-col :span="10">
        <el-card>
          <div slot="header" class="clearfix">
            <span>系统公告</span>
          </div>
          <el-row>
            <p>👀欢迎各位大佬拉取分支开发新功能</p>
            <p>🎃各位大佬轻点点，服务器低配</p>
            <p>🎉免费开源地址
              <!-- <el-link type="primary" href="https://gitee.com/izory/ZrAdminNetCore" target="_blank">
                https://gitee.com/izory/ZrAdminNetCore
              </el-link> -->
            </p>
            <p>
              <el-button type="primary" size="mini" icon="el-icon-cloudy" plain @click="goTarget('https://gitee.com/izory/ZrAdminNetCore')">访问码云</el-button>
              <el-button type="primary" size="mini" icon="el-icon-cloudy" plain @click="goTarget('https://github.com/izhaorui/ZrAdmin.NET')">Github</el-button>
            </p>
            <h3>如果觉得不错欢迎给个⭐Star⭐收藏一下 ，这样作者才有继续免费下去的动力，谢谢！</h3>
          </el-row>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card>
          <div slot="header" class="clearfix">
            <span>技术选型</span>
          </div>
          <el-col :span="10">
            <h4>后端技术</h4>
            <ul>
              <li>NET5</li>
              <li>JWT</li>
              <li>SqlSugar</li>
              <li>Quartz.Net</li>
              <li>MySql</li>
              <li>Mapster</li>
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
        </el-card>
      </el-col>
      <el-col :span="8">
        <el-card>
          <div slot="header" class="clearfix">
            <span>捐赠支持</span>
          </div>
          <div class="body">
            <span class="tip">如果觉得有用你可以请作者喝杯咖啡☕表示鼓励</span>
            <img src="http://ss.izhaorui.cn/pay.jpg" alt="donate" width="100%" />
          </div>
        </el-card>
      </el-col>
    </el-row>
    <el-divider />
  </div>
</template>

<script>
export default {
  name: "index",
  data() {
    return {
      // 版本号
      version: "1.0.0",
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

