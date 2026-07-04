# 登录用户名密码加密前端适配文档（Vue）

适用范围：DTSoftServerApp 授权认证接口（`AuthController`）登录用户名、密码传输加密。

## 1. 快速结论

- 获取登录加密公钥：`GET /api/Auth/login-encryption-key`
- 登录：`POST /api/Auth/login`
- 登录请求仍然使用 JSON body。
- 登录时 `Username`、`Password` 必须传加密后的 Base64 密文。
- 登录时必须额外提交 `EncryptionKeyId`。
- 加密算法：`RSA-OAEP` + `SHA-256`。
- 验证码字段继续按现有逻辑提交：`CaptchaId`、`CaptchaCode`。

## 2. 获取登录加密公钥

接口：`GET /api/Auth/login-encryption-key`

响应示例：

```json
{
  "Code": 200,
  "Message": "获取登录加密公钥成功",
  "Data": {
    "KeyId": "2qpsg1pzEDqQm2A99ks5sA",
    "Algorithm": "RSA-OAEP-256",
    "PublicKey": "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8A...",
    "PublicKeyPem": "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkq...\n-----END PUBLIC KEY-----\n"
  }
}
```

字段说明：

- `KeyId`：当前公钥 ID，登录时通过 `EncryptionKeyId` 原样传回。
- `Algorithm`：当前固定为 `RSA-OAEP-256`。
- `PublicKey`：`SubjectPublicKeyInfo` DER 格式的 Base64，适合浏览器 Web Crypto 导入。
- `PublicKeyPem`：PEM 格式公钥，前端 Web Crypto 通常不用这个字段。

## 3. 登录请求变化

接口：`POST /api/Auth/login`

请求体示例：

```json
{
  "Username": "VnHUWc74WwTt6w8f...Base64CipherText",
  "Password": "J8o1N8kS6G6xR7s...Base64CipherText",
  "EncryptionKeyId": "2qpsg1pzEDqQm2A99ks5sA",
  "CaptchaId": "0c3f2c4a8c5f46b2a0d6e9c4b5a1f234",
  "CaptchaCode": "A7K2"
}
```

注意：

- `Username`：原用户名加密后的 Base64 密文。
- `Password`：原密码加密后的 Base64 密文。
- `EncryptionKeyId`：获取公钥接口返回的 `Data.KeyId`。
- `CaptchaId`、`CaptchaCode`：沿用现有验证码适配逻辑。
- 登录成功响应不变，仍从 `Data.Token` 取 JWT。

常见新增失败响应：

```json
{
  "Code": 400,
  "Message": "登录参数解密失败，请刷新页面后重试"
}
```

出现该错误时，前端重新获取登录加密公钥后再登录。

## 4. Vue 加密示例

```js
import { ref } from 'vue'
import request from '@/utils/request'

const loginPublicKey = ref(null)

function base64ToArrayBuffer(base64) {
  const binary = window.atob(base64)
  const bytes = new Uint8Array(binary.length)

  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i)
  }

  return bytes.buffer
}

function arrayBufferToBase64(buffer) {
  const bytes = new Uint8Array(buffer)
  let binary = ''

  for (let i = 0; i < bytes.length; i += 1) {
    binary += String.fromCharCode(bytes[i])
  }

  return window.btoa(binary)
}

export async function loadLoginPublicKey() {
  const res = await request.get('/api/Auth/login-encryption-key')
  const key = await window.crypto.subtle.importKey(
    'spki',
    base64ToArrayBuffer(res.Data.PublicKey),
    {
      name: 'RSA-OAEP',
      hash: 'SHA-256'
    },
    false,
    ['encrypt']
  )

  loginPublicKey.value = {
    keyId: res.Data.KeyId,
    key
  }
}

export async function encryptLoginText(text) {
  if (!loginPublicKey.value) {
    await loadLoginPublicKey()
  }

  const plainBytes = new TextEncoder().encode(text)
  const cipherBuffer = await window.crypto.subtle.encrypt(
    {
      name: 'RSA-OAEP'
    },
    loginPublicKey.value.key,
    plainBytes
  )

  return arrayBufferToBase64(cipherBuffer)
}

export function getLoginEncryptionKeyId() {
  return loginPublicKey.value?.keyId
}
```

登录提交示例：

```js
async function handleLogin() {
  const encryptedUsername = await encryptLoginText(form.Username)
  const encryptedPassword = await encryptLoginText(form.Password)

  const res = await request.post('/api/Auth/login', {
    Username: encryptedUsername,
    Password: encryptedPassword,
    EncryptionKeyId: getLoginEncryptionKeyId(),
    CaptchaId: form.CaptchaId,
    CaptchaCode: form.CaptchaCode
  })

  localStorage.setItem('token', res.Data.Token)
}
```

## 5. 前端处理建议

- 进入登录页时调用 `loadLoginPublicKey()`。
- 如果登录失败信息为 `登录参数解密失败，请刷新页面后重试`，重新获取公钥后再提交。
- 如果后端服务重启，旧公钥会失效，前端重新进入登录页或重新获取公钥即可恢复。
- 如果前端有请求拦截器，请确保公钥接口和登录接口不强制携带 `Authorization`。
- 生产环境仍然必须使用 HTTPS；RSA 加密只保护登录请求体中的用户名、密码字段，不替代 HTTPS。
