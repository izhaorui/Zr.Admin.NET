import request from '@/utils/request'

export function upload(data) {
  return request({
    url: '/upload/saveFile',
    method: 'POST',
    data: data,
    headers: { "Content-Type": "multipart/form-data" },
  })
}
