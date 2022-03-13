import Vue from 'vue'

import Cookies from 'js-cookie'

import Element from 'element-ui'
import 'normalize.css/normalize.css' // a modern alternative to CSS resets
import './assets/styles/element-variables.scss'
import '@/assets/styles/index.scss' // global css

import App from './App'
import store from './store'
import router from './router'
import permission from './directive/permission'
import plugins from './plugins' // plugins
import signalR from '@/utils/signalR'

import './assets/icons' // icon
import './permission' // permission control
import { getDicts } from "@/api/system/dict/data";
import { getConfigKey } from "@/api/system/config";
import { parseTime, resetForm, addDateRange, selectDictLabel, selectDictLabels, download, handleTree, handleDict } from "@/utils/ruoyi";
//分页组件
import Pagination from "@/components/Pagination";
//自定义表格工具扩展
import RightToolbar from "@/components/RightToolbar"
// 富文本组件
import Editor from "@/components/Editor";
// 字典标签组件
import DictTag from '@/components/DictTag'
// 上传图片
import UploadImage from '@/components/UploadImage/index';
// 上传文件
import UploadFile from '@/components/FileUpload/index';

// 全局方法挂载
Vue.prototype.getDicts = getDicts
Vue.prototype.getConfigKey = getConfigKey
Vue.prototype.parseTime = parseTime
Vue.prototype.resetForm = resetForm
Vue.prototype.addDateRange = addDateRange
Vue.prototype.selectDictLabel = selectDictLabel
Vue.prototype.selectDictLabels = selectDictLabels
Vue.prototype.download = download
Vue.prototype.handleTree = handleTree
Vue.prototype.handleDict = handleDict

Vue.prototype.msgSuccess = function (msg) {
  this.$message({ showClose: true, message: msg, type: "success" });
}

Vue.prototype.msgError = function (msg) {
  this.$message({ showClose: true, message: msg, type: "error" });
}

Vue.prototype.msgInfo = function (msg) {
  this.$message.info(msg);
}

// 全局组件挂载
Vue.component('Pagination', Pagination)
Vue.component('RightToolbar', RightToolbar)
Vue.component('DictTag', DictTag)
Vue.component('Editor', Editor)
Vue.component('UploadImage', UploadImage)
Vue.component('UploadFile', UploadFile)

Vue.use(permission)
Vue.use(plugins)
Vue.use(Element, {
  size: Cookies.get('size') || 'small' // set element-ui default size
})

Vue.config.productionTip = false

signalR.init(process.env.VUE_APP_SOCKET_API);
Vue.prototype.signalr = signalR

new Vue({
  el: '#app',
  router,
  store,
  render: h => h(App)
})