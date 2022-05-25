import request from '@/utils/request'

// 查询菜单列表
export function listMenu(query) {
  return request({
    url: '/system/menu/list',
    method: 'get',
    params: query
  })
}
// 查询菜单列表
export function listMenuById(menuId) {
  return request({
    url: '/system/menu/list/' + menuId,
    method: 'get',
  })
}
// 查询菜单详细
export function getMenu(menuId) {
  return request({
    url: '/system/menu/' + menuId,
    method: 'get',
  })
}

// 查询菜单下拉树结构
export function treeselect() {
  return request({
    url: '/system/Menu/treeSelect',
    method: 'get'
  })
}

// 根据角色ID查询菜单下拉树结构
export function roleMenuTreeselect(roleId) {
  return request({
    url: '/system/menu/roleMenuTreeselect/' + roleId,
    method: 'get',
  })
}

// 新增菜单
export const addMenu = (data) => {
  return request({
    url: '/system/menu/add',
    method: 'put',
    data: data,
  })
}

// 修改菜单
export function updateMenu(data) {
  return request({
    url: '/system/Menu/edit',
    method: 'post',
    data: data
  })
}

// 删除菜单
export function delMenu(menuId) {
  return request({
    url: '/system/Menu/' + menuId,
    method: 'delete'
  })
}

//排序
export function changeMenuSort(data) {
  return request({
    url: '/system/Menu/ChangeSort',
    method: 'get',
    params: data
  })
}

// 获取路由
export const getRouters = (query) => {
  return request({
    url: '/getRouters',
    method: 'get',
    params: query
  })
}
