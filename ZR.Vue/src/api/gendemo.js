import request from '@/utils/request'

/**
* 代码生成演示分页查询
* @param {查询条件} data
*/
export function listGendemo(query) {
  return request({
    url: 'business/Gendemo/list',
    method: 'get',
    params: query,
  })
}

/**
* 新增代码生成演示
* @param data
*/
export function addGendemo(data) {
  return request({
    url: 'business/Gendemo',
    method: 'post',
    data: data,
  })
}

/**
* 修改代码生成演示
* @param data
*/
export function updateGendemo(data) {
  return request({
    url: 'business/Gendemo',
    method: 'PUT',
    data: data,
  })
}

/**
* 获取代码生成演示详情
* @param {Id} 代码生成演示Id
*/
export function getGendemo(id) {
  return request({
    url: 'business/Gendemo/' + id,
    method: 'get'
  })
}

/**
* 删除
* @param {主键} pid
*/
export function delGendemo(pid) {
  return request({
    url: 'business/Gendemo/' + pid,
    method: 'delete'
  })
}
// 导出
export function exportGendemo(query) {
  return request({
    url: 'business/Gendemo/export',
    method: 'get',
    params: query
  })
}