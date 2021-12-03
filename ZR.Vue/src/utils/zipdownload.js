import axios from 'axios'
import request from '@/utils/request'
import { getToken } from '@/utils/auth'

const mimeMap = {
  xlsx: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  zip: 'application/zip'
}

const baseUrl = process.env.VUE_APP_BASE_API
export function downLoadZip(str, filename) {
  var url = baseUrl + str
  request({
    method: 'get',
    url: str,
    responseType: 'blob',
    headers: { 'Authorization': 'Bearer ' + getToken() }
  }).then(res => {
    resolveBlob(res, mimeMap.zip)
  })
}
export function downLoadExcel(str, filename) {
  var url = baseUrl + str
  axios({
    method: 'get',
    url: url,
    responseType: 'blob',
    headers: { 'Authorization': 'Bearer ' + getToken() }
  }).then(res => {
    resolveExcel(res, filename)
  })
}
/**
 * 解析blob响应内容并下载
 * @param {*} res blob响应内容
 * @param {String} mimeType MIME类型
 */
export function resolveBlob(res, mimeType) {
  const aLink = document.createElement('a')
  var blob = new Blob([res.data], { type: mimeType })

  // //从response的headers中获取filename, 后端response.setHeader("Content-disposition", "attachment; filename=xxxx.docx") 设置的文件名;
  var patt = new RegExp('filename=([^;]+\\.[^\\.;]+);*')
  var contentDisposition = decodeURI(res.headers['Content-disposition'])
  var result = patt.exec(contentDisposition)
  var fileName = result[1]
  fileName = fileName.replace(/\"/g, '')
  aLink.href = URL.createObjectURL(blob)
  aLink.setAttribute('download', fileName) // 设置下载文件名称
  document.body.appendChild(aLink)
  aLink.click()
  document.body.appendChild(aLink)
}
/**
 * 解析blob响应内容并下载
 * @param {*} res blob响应内容
 * @param {String} fileName 文件名
 */
export function resolveExcel(res, fileName) {
  const aLink = document.createElement('a')
  var blob = new Blob([res.data], { type: 'application/vnd.ms-excel' })

  fileName = fileName.replace(/\"/g, '')
  aLink.href = URL.createObjectURL(blob)
  aLink.setAttribute('download', fileName) // 设置下载文件名称
  document.body.appendChild(aLink)
  aLink.click()
  document.body.appendChild(aLink)
  // 5.释放这个临时的对象url
  // window.URL.revokeObjectURL(aLink.href);
}

/**
 * 下载文件调用
 * @param 接口返回数据 文件名
 */
 export function downloadFile(resUrl, fileName) {
  if (!resUrl) {
    return
  }
  // 创建下载链接
  const url = resUrl
  const link = document.createElement('a')
  link.style.display = 'none'
  link.href = url
  link.setAttribute('download', fileName)// 文件名
  link.setAttribute('target', "_black")
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link) // 下载完成移除元素
  window.URL.revokeObjectURL(url) // 释放掉blob对象
}
