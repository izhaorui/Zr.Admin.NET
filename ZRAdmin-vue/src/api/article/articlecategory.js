import request from '@/utils/request'

/**
* 文章目录分页查询
* @param {查询条件} data
*/
export function listArticleCategory(query) {
  return request({
    url: 'article/ArticleCategory/list',
    method: 'get',
    params: query,
  })
}


/**
* 新增文章目录
* @param data
*/
export function addArticleCategory(data) {
  return request({
    url: 'article/ArticleCategory',
    method: 'post',
    data: data,
  })
}

/**
* 修改文章目录
* @param data
*/
export function updateArticleCategory(data) {
  return request({
    url: 'article/ArticleCategory',
    method: 'PUT',
    data: data,
  })
}

/**
* 获取文章目录详情
* @param {Id}
*/
export function getArticleCategory(id) {
  return request({
    url: 'article/ArticleCategory/' + id,
    method: 'get'
  })
}

/**
* 删除文章目录
* @param {主键} pid
*/
export function delArticleCategory(pid) {
  return request({
    url: 'article/ArticleCategory/' + pid,
    method: 'delete'
  })
}

// 导出文章目录
export function exportArticleCategory(query) {
  return request({
    url: 'article/ArticleCategory/export',
    method: 'get',
    params: query
  })
}

