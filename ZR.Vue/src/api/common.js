import request from '@/utils/request'

export function upload(data) {
  return request({
    url: '/upload/saveFile',
    method: 'POST',
    data: data,
    headers: { "Content-Type": "multipart/form-data" },
  })
}

/**
 * 发送邮件
 * @param {*} data
 * @returns
 */
export function sendEmail(data) {
  return request({
    url: '/home/SendEmail',
    method: 'POST',
    data: data,
  })
}
