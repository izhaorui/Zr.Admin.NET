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

          <!-- 阿里云服务器折扣区
          <el-link href="http://www.izhaorui.cn" type="primary" target="_blank">>☛☛点我进入☚☚</el-link>
          <br/> -->

          腾讯云限时秒杀活动
          <el-link href="https://curl.qcloud.com/4yEoRquq" type="primary" target="_blank">>☛☛点我进入☚☚</el-link><br />
          <h4 class="text-danger">
            云产品通用红包，可叠加官网常规优惠使用。(仅限新用户)
          </h4>
        </blockquote>
      </el-col>
    </el-row>
    <el-row :gutter="20">
      <el-col :span="14">
        <el-card>
          <div slot="header" class="clearfix">
            <span>框架介绍</span>
          </div>
          <el-row>
            <p>ZrAdmin.NET借鉴了很多开源项目的优点，让你开发Web管理系统更简单，所以我也把它给开源了（前端 <code>vue页面</code>主要参考若依，在此表示感谢.)</p>
            <p>代码完全免费开源，易读易懂、界面简洁美观，给你的项目多一种选择与参考。</p>
            <p>
              <b>当前版本:</b> <span>v{{ version }}</span>
            </p>
            <p>
              <el-button type="primary" size="mini" icon="el-icon-cloudy" plain @click="goTarget('https://gitee.com/izory/ZrAdminNetCore')">访问码云
              </el-button>
              <el-button type="primary" size="mini" icon="el-icon-cloudy" plain @click="goTarget('https://github.com/izhaorui/ZrAdmin.NET')">Github
              </el-button>
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
							<li>Epplus</li>
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
      <el-col :span="4">
        <el-card>
          <div slot="header" class="clearfix">
            <span>联系信息</span>
          </div>
          <div class="body">
            <span><i class="el-icon-user-solid"></i>QQ群：191349103</span>
          </div>
        </el-card>
      </el-col>
    </el-row>
    <el-row :gutter="20">
      <el-col :span="14">
        <el-card>
          <div slot="header" class="clearfix">
            <span>捐赠支持</span>
          </div>
          <div class="body">
            <span class="tip">如果觉得有用你可以请作者喝杯咖啡☕表示鼓励</span>
            <img src="@/assets/image/reward.jpg" alt="donate" width="80%" />
          </div>
        </el-card>
      </el-col>
      <el-col :span="10">
        <el-card>
          <div slot="header" class="clearfix">
            <span>更新日志</span>
          </div>
          <el-collapse accordion>
						<el-collapse-item title="v3.7.0-preview - 2021-11-28">
              <ol>
                <li>fix 代码生成编辑表信息选择上级菜单bug</li>
                <li>新增下拉框组件代码生成查询模块</li>
								<li>优化代码生成模板</li>
								<li>优化后端分页统一返回json</li>
								<li>修改代码生成form表单显示</li>
								<li>新增代码生成单选按钮动态绑定数据</li>
								<li>代码生成查询新增一键生成、可支持input、select、时间</li>
								<li>修改代码生成数据库连接字符串</li>
								<li>优化数据仓储</li>
              </ol>
            </el-collapse-item>
            <el-collapse-item title="v3.6.0 - 2021-11-21">
              <ol>
                <li>新增加系统表导出功能</li>
                <li>仓储新增加常用方法</li>
                <li>删除Npoi.Mapper包，新增加EPPlus 导出Excel包</li>
                <li>SqlSugarCoreNoDriver 升级至5.0.43</li>
                <li>代码生成新增加富文本组件</li>
                <li>发送邮件文本该用富文本工具</li>
                <li>部门管理新增加展开折叠</li>
                <li>fix 任务日志路由地址配置错误</li>
                <li>fix 部门删除功能</li>
                <li>其他功能优化</li>
              </ol>
            </el-collapse-item>
            <el-collapse-item title="v3.5.0 - 2021-11-18">
              <ol>
                <li>fix 代码生成提示错误信息</li>
                <li>fix sqlserver导入数据sys_dept表列不匹配问题 </li>
                <li>登录验证码替换为数字加字母静态组合 </li>
                <li>fix 登录验证码刷新后输入不了问题 </li>
                <li>替换node-sass为sass，sass-loader升级为10.1.0</li>
              </ol>
            </el-collapse-item>
            <el-collapse-item title="v3.4.0 - 2021-11-06">
              <ol>
                <li>页签TagsView新增关闭左侧、右侧功能&添加图标</li>
                <li>新增菜单导航显示风格TopNav（false为左侧导航菜单，true为顶部导航菜单）</li>
              </ol>
            </el-collapse-item>

            <el-collapse-item title="v3.3.0 - 2021-10-29">
              <ol>
                <li>接口日志新增加是否保存请求、返回参数</li>
                <li>重命名一些方法名</li>
                <li>fix 代码生成编辑、插入列未选中任何字段报错bug</li>
              </ol>
            </el-collapse-item>
          </el-collapse>
        </el-card>
      </el-col>
    </el-row>
    <el-divider />
  </div>
</template>

<script>
const defaultSettings = require('@/settings.js')
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

