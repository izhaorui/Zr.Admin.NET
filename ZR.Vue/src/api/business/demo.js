import request from '@/utils/request'

/**
* 演示分页查询
* @param {查询条件} data
*/
export function listDemo(query) {
  return request({
    url: 'business/Demo/list',
    method: 'get',
    params: query,
  })
}


/**
* 新增演示
* @param data
*/
export function addDemo(data) {
  return request({
    url: 'business/Demo',
    method: 'post',
    data: data,
  })
}

/**
* 修改演示
* @param data
*/
export function updateDemo(data) {
  return request({
    url: 'business/Demo',
    method: 'PUT',
    data: data,
  })
}

/**
* 获取演示详情
* @param {Id}
*/
export function getDemo(id) {
  return request({
    url: 'business/Demo/' + id,
    method: 'get'
  })
}

/**
* 删除演示
* @param {主键} pid
*/
export function delDemo(pid) {
  return request({
    url: 'business/Demo/' + pid,
    method: 'delete'
  })
}

// 导出演示
export function exportDemo(query) {
  return request({
    url: 'business/Demo/export',
    method: 'get',
    params: query
  })
}

//排序
export function changeSort(data) {
  return request({
    url: 'business/Demo/ChangeSort',
    method: 'get',
    params: data
  })
}
