import JSEncrypt from 'jsencrypt/bin/jsencrypt.min'

// 密钥对生成 http://web.chacuo.net/netrsakeypair

const publicKey = 'MFwwDQYJKoZIhvcNAQEBBQADSwAwSAJBALj0zjON+EVdBsnMcR4Uj+jOYgp5ZipftQZ1utW8KvVioz+RSaotF1JHt59q9SC/mZcWWpbpcEqQ3WyyyCC33msCAwEAAQ=='

const privateKey =
  'MIIBVAIBADANBgkqhkiG9w0BAQEFAASCAT4wggE6AgEAAkEAuPTOM434RV0GycxxHhSP6M5iCnlmKl+1BnW61bwq9WKjP5FJqi0XUke3n2r1IL+ZlxZalulwSpDdbLLIILfeawIDAQABAkB5PYAtq1KjpWddwPYlkbUEFsWNuCaQgExZ/7KJiN9gGjo/UfUZ3W39Orb8PITIYf1NbasReqgddAcsfJNyoDWBAiEA7K89DyTmbjNSmekdD3rejRDdMzzXYtcbo69ZjHoowMUCIQDIDN8eg6PcWk4kiRcRYcNEfriUJR7Fg07ellSPv821bwIhAJA5TEyxIJUgQwI0cZfgOELfdtrlBR5ek6IPlNKsEa89AiBbMVroexPQWC41A3VLjChKagXUKpO7b98dIqRLnyCz6wIgP3qpvnO4IOxY7f5XarfCVyIHZJAMt/R1f16P5OkKv+A='

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

