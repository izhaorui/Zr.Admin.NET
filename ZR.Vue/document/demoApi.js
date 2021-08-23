import request from '@/utils/request'

// 查询列表
export function listXXXX(query) {
  return request({
    url: '/system/XXXX/list',
    method: 'get',
    params: query
  })
}

// 查询详细
export function getXXXX(Id) {
  return request({
    url: '/system/XXXX/' + Id,
    method: 'get',
  })
}

// 新增
export function addXXXX (data) {
  return request({
    url: '/system/XXXX/add',
    method: 'post',
    data: data,
  })
}

// 修改
export function updateXXXX(data) {
  return request({
    url: '/system/XXXX/edit',
    method: 'put',
    data: data
  })
}

// 删除菜单
export function delXXXX(id) {
  return request({
    url: '/system/XXXX/' + id,
    method: 'delete'
  })
}

//排序
export function changeSort(data) {
  return request({
    url: '/system/XXXX/ChangeSort',
    method: 'post',
    data: data
  })
}
