
import Cookies from 'js-cookie'

const TokenKey = 'ZR-Token'

export function getToken() {
  return Cookies.get(TokenKey)
}

export function setToken(token) {
  console.log('set token=' + token)
  return Cookies.set(TokenKey, token)
}

export function removeToken() {
  return Cookies.remove(TokenKey)
}
