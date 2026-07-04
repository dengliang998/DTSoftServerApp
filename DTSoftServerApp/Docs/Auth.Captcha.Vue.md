# 登录验证码接口前端适配文档（Vue）

适用范围：DTSoftServerApp 授权认证接口（`AuthController`）登录验证码能力。

## 1. 快速结论

- 获取验证码：`GET /api/Auth/captcha`
- 登录：`POST /api/Auth/login`
- 登录请求仍然使用 JSON body。
- 登录时必须提交 `CaptchaId` 和 `CaptchaCode`。
- 验证码有效期：5 分钟。
- 验证码一次性使用：无论登录成功、验证码错误、账号密码错误，都需要重新获取验证码。

## 2. 获取验证码

接口：`GET /api/Auth/captcha`

响应示例：

```json
{
  "Code": 200,
  "Message": "获取验证码成功",
  "Data": {
    "CaptchaId": "0c3f2c4a8c5f46b2a0d6e9c4b5a1f234",
    "ImageBase64": "PHN2ZyB4bWxucz0i...",
    "ImageDataUrl": "data:image/svg+xml;base64,PHN2ZyB4bWxucz0i...",
    "ExpiresInSeconds": 300
  }
}
```

字段说明：

- `CaptchaId`：验证码 ID，登录时原样传回。
- `ImageDataUrl`：可直接赋值给 `<img :src="captchaImage" />`。
- `ImageBase64`：不带 `data:image/svg+xml;base64,` 前缀，通常前端不用自己拼。
- `ExpiresInSeconds`：有效期秒数。

## 3. 登录

接口：`POST /api/Auth/login`

请求头：

```http
Content-Type: application/json
```

请求体验证码相关字段：

```json
{
  "CaptchaId": "0c3f2c4a8c5f46b2a0d6e9c4b5a1f234",
  "CaptchaCode": "A7K2"
}
```

常见失败响应：

```json
{
  "Code": 400,
  "Message": "验证码不能为空"
}
```

```json
{
  "Code": 400,
  "Message": "验证码已过期，请刷新后重试"
}
```

```json
{
  "Code": 400,
  "Message": "验证码错误"
}
```

## 4. 前端处理建议

- 页面首次进入登录页时调用 `loadCaptcha()`。
- 点击验证码图片时重新调用 `loadCaptcha()`。
- 任意登录失败后都重新调用 `loadCaptcha()`，因为验证码是一次性消费。
- `CaptchaCode` 可统一转成大写展示，但后端校验不区分大小写。
- 如果前端有请求拦截器，请确保登录和验证码接口不强制携带 `Authorization`。
