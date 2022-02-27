const state = {
  onlineNum: 0
}
const mutations = {
  SET_ONLINEUSER_NUM: (state, num) => {
    state.onlineNum = num
  },
}

const actions = {
  //更新在线人数
  changeOnlineNum({ commit }, data) {
    commit('SET_ONLINEUSER_NUM', data)
  },
}

export default {
  namespaced: true,
  state,
  mutations,
  actions
}