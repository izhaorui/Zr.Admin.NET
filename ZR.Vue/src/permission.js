import router from './router'
import store from './store'
import {
  Message
} from 'element-ui'
import NProgress from 'nprogress'
import 'nprogress/nprogress.css'
import {
  getToken
} from '@/utils/auth'

NProgress.configure({
  showSpinner: false
})

const whiteList = ['/login', '/auth-redirect', '/bind', '/register', '/demo']

router.beforeEach((to, from, next) => {
  NProgress.start()
  console.log(to.path);
  const hasToken = getToken()

  if (hasToken) {
    /* has token*/
    if (to.path === '/login') {
      next({
        path: '/'
      })
      NProgress.done()
    } else {
      if (store.getters.roles.length === 0) {

        // 判断当前用户是否已拉取完user_info信息
        store.dispatch('GetInfo').then(res => {
          //console.log('拉取userInfo', JSON.stringify(res))
          // 拉取user_info
          const roles = res.data.roles
          store.dispatch('GenerateRoutes', {
            roles
          }).then(accessRoutes => {

            // 测试 默认静态页面
            // store.dispatch('permission/generateRoutes', { roles }).then(accessRoutes => {
            // 根据roles权限生成可访问的路由表
            router.addRoutes(accessRoutes) // 动态添加可访问路由表
            next({
              ...to,
              replace: true
            }) // hack方法 确保addRoutes已完成
          })
          next()
        }).catch(err => {
          console.error(err)
          //这部不能少，否则会出现死循环
          store.dispatch('FedLogOut').then(() => {
            Message.error(err != undefined ? err : '登录失败')
            next({
              path: '/'
            })
          })
          next(`/login?redirect=${to.path}`)
        })
      } else {
        next()
        // 没有动态改变权限的需求可直接next() 删除下方权限判断 ↓
        // if (hasPermission(store.getters.roles, to.meta.roles)) {
        //   next()
        // } else {
        //   next({ path: '/401', replace: true, query: { noGoBack: true }})
        // }
        // 可删 ↑
      }
    }
  } else {
    // 没有token
    if (whiteList.indexOf(to.path) !== -1) {
      // 在免登录白名单，直接进入
      next()
    } else {
      next(`/login?redirect=${to.fullPath}`) // 否则全部重定向到登录页
      NProgress.done()
    }
  }
})

router.afterEach(() => {
  NProgress.done()
})
