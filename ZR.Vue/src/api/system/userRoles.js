import request from '@/utils/request'

// 查询角色用户
export function getRoleUsers(roleId) {
  return request({
    url: '/system/userRole/get/' + roleId,
    method: 'get'
  })
}

// 添加角色用户
export function createRoleUsers(data) {
  return request({
    url: '/system/userRole/create',
    method: 'post',
    data
  })
}
// 删除角色用户
export function deleteRoleUsers(data) {
  return request({
    url: '/system/userRole/delete',
    method: 'post',
    data
  })
}
// 查询角色未添加用户列表
export function getExcludeUsers(roleId) {
  return request({
    url: '/system/userRole/getExcludeUsers?roleId=' + roleId,
    method: 'get'
  })
}

// export default { getRoleUsers, getExcludeUsers }
