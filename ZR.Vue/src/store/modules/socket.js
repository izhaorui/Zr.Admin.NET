const state = {
  onlineNum: 0,
  noticeList: []
}
const mutations = {
  SET_ONLINEUSER_NUM: (state, num) => {
    state.onlineNum = num
  },
  SET_NOTICE_list: (state, data) => {
    state.noticeList = data;
  }
}

const actions = {
  //更新在线人数
  changeOnlineNum({ commit }, data) {
    commit('SET_ONLINEUSER_NUM', data)
  },
  // 更新系统通知
  getNoticeList({ commit }, data) {
    commit('SET_NOTICE_list', data)
  }
}

export default {
  namespaced: true,
  state,
  mutations,
  actions
}