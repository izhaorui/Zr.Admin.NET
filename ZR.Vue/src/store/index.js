import Vue from 'vue'
import Vuex from 'vuex'
import app from './modules/app'
import user from './modules/user'
import tagsView from './modules/tagsView'
import permission from './modules/permission'
import settings from './modules/settings'
import socket from './modules/socket'
import getters from './getters'

Vue.use(Vuex)

/**
 * 当前用户数据
 */
const state = {
  appVersionInfo: ''
}
const store = new Vuex.Store({
  modules: {
    app,
    user,
    tagsView,
    permission,
    settings,
		socket
  },
  state: state,//这里放全局参数
  getters
})

export default store
