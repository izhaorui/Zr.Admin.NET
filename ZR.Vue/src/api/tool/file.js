import request from '@/utils/request'

/**
* 文件存储分页查询
* @param {查询条件} data
*/
export function listSysfile(query) {
  return request({
    url: 'tool/file/list',
    method: 'get',
    params: query,
  })
}

/**
* 新增文件存储
* @param data
*/
export function addSysfile(data) {
  return request({
    url: 'tool/file',
    method: 'post',
    data: data,
  })
}

/**
* 修改文件存储
* @param data
*/
export function updateSysfile(data) {
  return request({
    url: 'tool/file',
    method: 'PUT',
    data: data,
  })
}

/**
* 获取文件存储详情
* @param {Id}
*/
export function getSysfile(id) {
  return request({
    url: 'tool/file/' + id,
    method: 'get'
  })
}

/**
* 删除文件存储
* @param {主键} pid
*/
export function delSysfile(pid) {
  return request({
    url: 'tool/file/' + pid,
    method: 'delete'
  })
}

// 导出文件存储
export function exportSysfile(query) {
  return request({
    url: 'tool/file/export',
    method: 'get',
    params: query
  })
}

