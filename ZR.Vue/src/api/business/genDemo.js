import request from '@/utils/request'
import QS from 'qs'
import { downFile } from '@/utils/request'

/**
 * 演示分页查询
 * @param {查询条件} data
 */
export function listGenDemo(query) {
  return request({
    url: 'business/GenDemo/list',
    method: 'get',
    params: query,
    paramsSerializer: function (params) {
      return QS.stringify(params, { indices: false })
    }
  })
}

/**
 * 演示tree查询
 * @param {查询条件} data
 */
export function treelistGenDemo(query) {
  return request({
    url: 'business/GenDemo/treelist',
    method: 'get',
    params: query,
  })
}
/**
 * 新增演示
 * @param data
 */
export function addGenDemo(data) {
  return request({
    url: 'business/GenDemo',
    method: 'post',
    data: data,
  })
}
/**
 * 修改演示
 * @param data
 */
export function updateGenDemo(data) {
  return request({
    url: 'business/GenDemo',
    method: 'PUT',
    data: data,
  })
}
/**
 * 获取演示详情
 * @param {Id}
 */
export function getGenDemo(id) {
  return request({
    url: 'business/GenDemo/' + id,
    method: 'get'
  })
}

/**
 * 删除演示
 * @param {主键} pid
 */
export function delGenDemo(pid) {
  return request({
    url: 'business/GenDemo/delete/' + pid,
    method: 'POST'
  })
}
// 清空演示
export function clearGenDemo() {
  return request({
    url: 'business/GenDemo/clean',
    method: 'POST'
  })
}
// 导出演示
export async function exportGenDemo(query) {
  await downFile('business/GenDemo/export', { ...query })
}
