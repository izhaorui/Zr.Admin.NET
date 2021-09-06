import request from '@/utils/request'

// 查询生成表数据
export function listTable(query) {
  return request({
    url: '/tool/gen/list',
    method: 'get',
    params: query
  })
}
// 查询db数据库列表
export function listDbTable(query) {
  return request({
    url: '/tool/gen/db/list',
    method: 'get',
    params: query
  })
}

// 查询表详细信息
export function getGenTable(tableId) {
  return request({
    url: '/tool/gen/' + tableId,
    method: 'get'
  })
}

// 修改代码生成信息
export function updateGenTable(data) {
  return request({
    url: '/tool/gen',
    method: 'put',
    data: data
  })
}

// 导入表
export function importTable(data) {
  return request({
    url: '/tool/gen/importTable',
    method: 'post',
    params: data
  })
}

// 预览生成代码
export function previewTable(tableId) {
  return request({
    url: '/tool/gen/preview/' + tableId,
    method: 'get'
  })
}

// 删除表数据
export function delTable(tableId) {
  return request({
    url: '/tool/gen/' + tableId,
    method: 'delete'
  })
}

// 生成代码（自定义路径）
export function genCode(tableName) {
  return request({
    url: '/tool/gen/genCode/' + tableName,
    method: 'get'
  })
}

// 同步数据库
export function synchDb(tableName) {
  return request({
    url: '/tool/gen/synchDb/' + tableName,
    method: 'get'
  })
}

/**新的代码生成 */

/**
   * 创建数据库连接
   */
 export function createGetDBConn(data) {
  return request({
    url: 'CodeGenerator/CreateDBConn',
    method: 'post',
    data: data,
  })
}
/**
   * 获取数据库
   */
export function codeGetDBList() {
  return request({
    url: 'CodeGenerator/GetListDataBase',
    method: 'get',
  })
}
/**
   * 获取数据库表
   */
export function codeGetTableList(data) {
  return request({
    url: 'CodeGenerator/FindListTable',
    method: 'get',
    params: data,
  })
}
/**
   * 生成代码
   */
export async function codeGenerator(data) {
  return await request({
    url: 'CodeGenerator/Generate',
    method: 'get',
    params: data,
    timeout: 0,
  })
}
/**
 *
* 数据库解密
*/
export function dbtoolsConnStrDecrypt(data) {
  return request({
    url: 'DbTools/ConnStrDecrypt',
    method: 'post',
    params: data,
  })
}
/**
   * 数据库加密
   */
export function dbtoolsConnStrEncrypt(data) {
  return request({
    url: 'DbTools/ConnStrEncrypt',
    method: 'post',
    params: data,
  })
}
