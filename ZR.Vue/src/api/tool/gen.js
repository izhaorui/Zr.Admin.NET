import request from '@/utils/request'

// 预览生成代码
// export function previewTable(tableId) {
//   return request({
//     url: '/tool/gen/preview/' + tableId,
//     method: 'get'
//   })
// }

/**
   * 创建数据库连接
   */
// export function createGetDBConn(data) {
//   return request({
//     url: 'tool/gen/CreateDBConn',
//     method: 'post',
//     data: data,
//   })
// }
/**
   * 获取数据库
   */
export function codeGetDBList() {
  return request({
    url: 'tool/gen/getDbList',
    method: 'get',
  })
}
/**
   * 获取数据库表
   */
export function listDbTable(data) {
  return request({
    url: 'tool/gen/getTableList',
    method: 'get',
    params: data,
  })
}
/**
   * 生成代码
   */
export async function codeGenerator(data) {
  return await request({
    url: 'tool/gen/genCode',
    method: 'post',
    data: data,
  })
}

/**
 * 获取表格列信息
 * @param {*} data
 * @returns
 */
export function queryColumnInfo(tableId) {
  return request({
    url: 'tool/gen/Column/' + tableId,
    method: 'GET',
  })
}


// 查询表详细信息
export function getGenTable(params) {
  return request({
    url: 'tool/gen/listGenTable',
    method: 'get',
    params: params
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
// 删除表数据
export function delTable(tableId) {
  return request({
    url: '/tool/gen/' + tableId,
    method: 'delete'
  })
}

// 修改代码生成表信息
export function updateGenTable(data) {
  return request({
    url: '/tool/gen/',
    method: 'put',
    data: data
  })
}

// 预览生成代码
export function previewTable(tableId) {
  return request({
    url: '/tool/gen/preview/' + tableId,
    method: 'get'
  })
}

// /**
//  *
// * 数据库解密
// */
// export function dbtoolsConnStrDecrypt(data) {
//   return request({
//     url: 'DbTools/ConnStrDecrypt',
//     method: 'post',
//     params: data,
//   })
// }
// /**
//    * 数据库加密
//    */
// export function dbtoolsConnStrEncrypt(data) {
//   return request({
//     url: 'DbTools/ConnStrEncrypt',
//     method: 'post',
//     params: data,
//   })
// }
