import { login, logOut, getInfo } from '@/api/system/login'
import { getToken, setToken, removeToken } from '@/utils/auth'

const user = {
  state: {
    userInfo: '',
    token: getToken(),
    name: '',
    avatar: '',
    roles: [],
    permissions: []
  },

  mutations: {
    SET_TOKEN: (state, token) => {
      state.token = token
    },
    SET_NAME: (state, name) => {
      state.name = name
    },
    SET_AVATAR: (state, avatar) => {
      state.avatar = avatar
    },
    SET_ROLES: (state, roles) => {
      state.roles = roles
    },
    SET_PERMISSIONS: (state, permissions) => {
      state.permissions = permissions
    },
    SET_USERINFO: (state, value) => {
      state.userInfo = value
    }
  },

  actions: {
    // 登录
    Login({ commit }, userInfo) {
      const username = userInfo.username.trim()
      const password = userInfo.password
      const code = userInfo.code
      const uuid = userInfo.uuid
      return new Promise((resolve, reject) => {
        login(username, password, code, uuid).then(res => {
          if (res.code == 200) {
            //提交上面的mutaions方法
            setToken(res.data)
            commit('SET_TOKEN', res.data)
            resolve()//then处理
          } else {
            reject(res)//catch处理
          }
        })
      })
    },

    // 获取用户信息
    GetInfo({ commit, state }) {
      return new Promise((resolve, reject) => {
        getInfo(state.token).then(res => {
          if (!res || res.code != 200) {
            return reject(res.msg)
          }

          const data = res.data
          const avatar = data.user.avatar == "" ? require("@/assets/image/profile.jpg") : data.user.avatar;

          if (data.roles && data.roles.length > 0) { // 验证返回的roles是否是一个非空数组
            commit('SET_ROLES', data.roles)
            commit('SET_PERMISSIONS', data.permissions)
          } else {
            commit('SET_ROLES', ['ROLE_DEFAULT'])
          }

          commit('SET_NAME', data.user.nickName)
          commit('SET_AVATAR', avatar)
          commit('SET_USERINFO', data.user)//新加
          resolve(res)
        }).catch(error => {
          reject(error)
        })
      })
    },

    // 退出系统
    LogOut({ commit, state }) {
      return new Promise((resolve, reject) => {
        logOut(state.token).then(() => {
          removeToken()// 必须先移除token
          commit('SET_TOKEN', '')
          commit('SET_ROLES', [])
          commit('SET_PERMISSIONS', [])
          resolve()
        }).catch(error => {
          reject(error)
        })
      })
    },

    // 前端 登出
    FedLogOut({ commit }) {
      return new Promise(resolve => {
        commit('SET_TOKEN', '')
        removeToken()
        resolve()
      })
    }
  }
}

export default user
