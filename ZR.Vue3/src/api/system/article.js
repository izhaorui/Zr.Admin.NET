import request from '@/utils/request'

// 查询列表
export function listArticle(query) {
  return request({
    url: '/Article/list',
    method: 'get',
    params: query
  })
}
// 查询最新列表
export function listNewArticle(query) {
  return request({
    url: '/Article/newList',
    method: 'get',
    params: query
  })
}

// 查询详细
export function getArticle(Id) {
  return request({
    url: '/Article/' + Id,
    method: 'get',
  })
}

// 新增
export function addArticle(data) {
  return request({
    url: '/Article/add',
    method: 'post',
    data: data,
  })
}

// 修改
export function updateArticle(data) {
  return request({
    url: '/Article/edit',
    method: 'put',
    data: data
  })
}

// 删除菜单
export function delArticle(id) {
  return request({
    url: '/Article/' + id,
    method: 'delete'
  })
}
// 查询菜单目录
export function listArticleCategory() {
  return request({
    url: '/Article/CategoryList',
    method: 'get'
  })
}
// 查询菜单目录树
export function listArticleCategoryTree(){
  return request({
    url: '/Article/CategoryTreeList',
    menubar: 'get'
  })
}
