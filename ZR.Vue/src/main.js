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

import './assets/icons' // icon
import './permission' // permission control
import { getDicts } from "@/api/system/dict/data";
import { getConfigKey } from "@/api/system/config";
import { parseTime, resetForm, addDateRange, selectDictLabel, selectDictLabels, download, handleTree } from "@/utils/ruoyi";
//分页组件
import Pagination from "@/components/Pagination";
//自定义表格工具扩展
import RightToolbar from "@/components/RightToolbar"
// 富文本组件
import Editor from "@/components/Editor";
// 字典标签组件
import DictTag from '@/components/DictTag'
// 字典数据组件
// import DictData from '@/components/DictData'

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

Vue.prototype.msgSuccess = function(msg) {
  this.$message({ showClose: true, message: msg, type: "success" });
}

Vue.prototype.msgError = function(msg) {
  this.$message({ showClose: true, message: msg, type: "error" });
}

Vue.prototype.msgInfo = function(msg) {
  this.$message.info(msg);
}

// 全局组件挂载
Vue.component('Pagination', Pagination)
Vue.component('RightToolbar', RightToolbar)
Vue.component('DictTag', DictTag)
Vue.component('Editor', Editor)
Vue.use(permission)

Vue.use(Element, {
  size: Cookies.get('size') || 'medium' // set element-ui default size
})

Vue.config.productionTip = false

new Vue({
  el: '#app',
  router,
  store,
  render: h => h(App)
})
console.log('后端地址:' + process.env.VUE_APP_BASE_API)