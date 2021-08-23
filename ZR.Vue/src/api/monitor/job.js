import request from '@/utils/request'

export function queryTasks(data) {
  return request({
    url: '/system/tasks/list',
    method: 'get',
    data
  })
}

export function getTasks(id) {
  return request({
    url: '/system/tasks/get?id=' + id,
    method: 'get'
  })
}

export function getAllTasks() {
  return request({
    url: '/system/tasks/getAll',
    method: 'get'
  })
}

export function createTasks(data) {
  return request({
    url: '/system/tasks/create',
    method: 'post',
    data
  })
}

export function updateTasks(data) {
  return request({
    url: '/system/tasks/update',
    method: 'post',
    data
  })
}

export function deleteTasks(id) {
  return request({
    url: '/system/tasks/delete?id=' + id,
    method: 'delete'
  })
}

export function startTasks(id) {
  return request({
    url: '/system/tasks/start?id=' + id,
    method: 'get'
  })
}

export function stopTasks(id) {
  return request({
    url: '/system/tasks/stop?id=' + id,
    method: 'get'
  })
}

export function runTasks(id) {
  return request({
    url: '/system/tasks/run?id=' + id,
    method: 'get'
  })
}

export default { queryTasks, getTasks, getAllTasks, createTasks, updateTasks, deleteTasks, startTasks, stopTasks, runTasks }
