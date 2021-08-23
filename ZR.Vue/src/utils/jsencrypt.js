import JSEncrypt from 'jsencrypt/bin/jsencrypt'

// 密钥对生成 http://web.chacuo.net/netrsakeypair

const publicKey = '-----BEGIN PUBLIC KEY-----MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDlJjf2lmK4zrpiATJSYfjkP2YhZ3SzKO0kqgX3Hg9o4NrgRgg9Abxw9vwZL7Weg22cJmahkx6waRYo8wXDqa2adJPntp60SAwWXou4cQTzHlySGQAC2U4CX54iHsKgqY7ifD2gmqJAkDnQkSXl4OKDTISnZcmlwIwo3Tj8jb9TWwIDAQAB-----END PUBLIC KEY-----';

const privateKey = '-----BEGIN ENCRYPTED PRIVATE KEY-----MIICxjBABgkqhkiG9w0BBQ0wMzAbBgkqhkiG9w0BBQwwDgQIV5E4J9jebHICAggAMBQGCCqGSIb3DQMHBAhdEG1zo87qTASCAoB8kR8KXgHIOQJIppp/2qGb+I8qcx/++Mb3yCAceHRMFZyczqlbKlwaDWzUVS3PQwbktb4s2coJt1uq/YL8CxAP/EJyMWsqiJxSl+4Mql0cHPhociwC7avGLtB+J2+BJU/ytF4u+/sSRGeSddoqx2glmGtYuF9U' +
'qv3jVElfUUOr3iq0aGSQRjCguVHk8fW1vy6QYi9ipPT0/BjWu4NutRLr9TI+Sy/4' +
'F9e6PeIKefT8ZX/q6vO0C1kVoG3GT1EjQfIF5XjmqGPUX7AR4J8R+uplDakHKZdH' +
'l6tqH+nJNQQH2RuFzYOWmUU2BiMEuU6ortJc8HkOy7Ca4FVJywnNxjOqzikI8DFW' +
'M9NuhMOwUCBae4582qW0zlc+ZGY1uWAoan4f3kmNdfeLU0xzk1yeR8KkprDwRk0m' +
'6jNSSOkBlNPerCcYm+pIaOXw+NQaaU4RQwHfNZnOWjteSuOmcszXuzdniuclHLzG' +
'zEwTolwUX9ez1sZCCmrjJpYMG4b9NQait5gx77Ogg7ymxBKnK89Gfuh3v3xvV6Td' +
'01r1GLd+OWPaI2kxAqruD/ZsMwXMpOLAc9ljz6cgtqlmvEk6tpI+WpGCaJBZXUA6' +
'/dv7KQeiWFQF5Oao3kX0s0wW3s2B4A2QFDE9HZHAw12nlsST8nmh+8zTxQNXhBMx' +
'B/UBkFND4UseKwYT4k+L0MmYcyExCagDiFa7k2/08D8DSbCVSWBXTH6xKWPMvFwr' +
'z4f5znLZFdbXYHFp0LyAw4HJ3LeTj0q8f2hNMG7lWZKYI7XZfmvoBkhODdOt2wTv' +
'U/T0f/ibcUf9Q9YXZQQkv1NOwmDtAB1KvHIkh2LALc/BY4qEoUyEVtLg' +
'-----END ENCRYPTED PRIVATE KEY-----'

// 加密
export function encrypt(txt) {
  const encryptor = new JSEncrypt()
  encryptor.setPublicKey(publicKey) // 设置公钥
  return encryptor.encrypt(txt) // 对数据进行加密
}

// 解密
export function decrypt(txt) {
  const encryptor = new JSEncrypt()
  encryptor.setPrivateKey(privateKey) // 设置私钥
  return encryptor.decrypt(txt) // 对数据进行解密
}

