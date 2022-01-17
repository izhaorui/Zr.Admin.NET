import request from '@/utils/request'

export function upload(data) {
  return request({
    url: '/common/UploadFile',
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
    url: '/common/SendEmail',
    method: 'POST',
    data: data,
  })
}
