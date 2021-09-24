import request from '@/utils/request'

/**
   * 代码生成测试表分页查询
   * @param {查询条件} data
   */
export function listGendemo(query) {
  return request({
    url: 'bus/Gendemo/list',
    method: 'get',
    params: query,
  })
}

/**
   * 新增代码生成测试表
   * @param data
   */
export function addGendemo(data) {
  return request({
    url: '/bus/Gendemo',
    method: 'post',
    data: data,
  })
}

/**
   * 修改代码生成测试表
   * @param data
   */
export function updateGendemo(data) {
  return request({
    url: '/bus/Gendemo',
    method: 'PUT',
    data: data,
  })
}

/**
   * 获取代码生成测试表详情
   * @param {Id} 代码生成测试表Id
   */
export function getGendemo(id) {
  return request({
    url: '/bus/Gendemo/' + id,
    method: 'get'
  })
}

/**
   * 删除
   * @param {主键} pid
   */
export function delGendemo(pid) {
  return request({
    url: '/bus/Gendemo/' + pid,
    method: 'delete'
  })
}
