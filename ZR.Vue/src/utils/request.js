import axios from 'axios'
import {
  MessageBox,
  Message
} from 'element-ui'
import store from '@/store'
import {
  getToken
} from '@/utils/auth'
// import errorCode from '@/utils/errorCode'

// 解决后端跨域获取不到cookie问题
axios.defaults.withCredentials = true
axios.defaults.headers['Content-Type'] = 'application/json'
// 创建axios实例
const service = axios.create({
  // axios中请求配置有baseURL选项，表示请求URL公共部分
  baseURL: process.env.VUE_APP_BASE_API,
  // 超时
  timeout: 30000
})

// request拦截器
service.interceptors.request.use(config => {
  // 是否需要设置 token
  if (getToken()) {
    //将token放到请求头发送给服务器,将tokenkey放在请求头中
    config.headers['Authorization'] = 'Bearer ' + getToken();
    config.headers['userid'] = store.getters.userId;
  } else {
    // console.log(config)
  }
  return config;
}, error => {
  console.log(error)
  Promise.reject(error)
})

// 响应拦截器
service.interceptors.response.use(res => {
    if (res.status !== 200) {
      Promise.reject('network error');
      return;
    }
    // 未设置状态码则默认成功状态
    const { code, msg } = res.data;

    if (code == 401) {
      MessageBox.confirm('登录状态已过期，请重新登录', '系统提示', {
        confirmButtonText: '重新登录',
        cancelButtonText: '取消',
        type: 'warning'
      }).then(() => {
        store.dispatch('LogOut').then(() => {
          location.href = process.env.VUE_APP_ROUTER_PREFIX + 'index';
        })
      })

      return Promise.reject('无效的会话，或者会话已过期，请重新登录。')
    } else if (code == 0 || code == 110 || code == 101 || code == 403 || code == 500) {
      Message({
        message: msg,
        type: 'error'
      })
      return Promise.reject(msg)
    } else {
      //返回标准 code/msg/data字段
      return res.data;
    }
  },
  error => {
    console.log('err' + error)
    let {
      message
    } = error;
    if (message == "Network Error") {
      message = "后端接口连接异常";
    } else if (message.includes("timeout")) {
      message = "系统接口请求超时";
    } else if (message.includes("Request failed with status code")) {
      message = "系统接口" + message.substr(message.length - 3) + "异常";
    }
    Message({
      message: message,
      type: 'error',
      duration: 5 * 1000
    })
    return Promise.reject(error)
  }
)

/**
 * get方法，对应get请求
 * @param {String} url [请求的url地址]
 * @param {Object} params [请求时携带的参数]
 */
export function get(url, params) {
  return new Promise((resolve, reject) => {
    axios
      .get(url, {
        params: params
      })
      .then(res => {
        resolve(res.data)
      })
      .catch(err => {
        reject(err)
      })
  })
}

export function post(url, params) {
  return new Promise((resolve, reject) => {
    axios
      .post(url, {
        params: params
      })
      .then(res => {
        resolve(res.data)
      })
      .catch(err => {
        reject(err)
      })
  })
}

/**
 * 提交表单
 * @param {*} url
 * @param {*} data
 */
export function postForm(url, data, config) {
  return new Promise((resolve, reject) => {
    axios.post(url, data, config).then(res => {
      resolve(res.data)
    }).catch(err => {
      reject(err)
    })
  })
}
export default service