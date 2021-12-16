import hasRole from './hasRole'
import hasPermi from './hasPermi'
import clipboard from '../module/clipboard'

const install = function(Vue) {
  Vue.directive('hasRole', hasRole)
  Vue.directive('hasPermi', hasPermi)
	Vue.directive('clipboard', clipboard)
}

if (window.Vue) {
  window['hasRole'] = hasRole
  window['hasPermi'] = hasPermi
  Vue.use(install); // eslint-disable-line
}

export default install
