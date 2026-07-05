# 系统日志筛选接口前端适配文档（Vue）

适用范围：DTSoftServerApp 日志管理接口（`LogController`）系统日志列表筛选。

## 1. 快速结论

- 接口：`POST /api/Log/GetLogActionList`
- 传参方式：`[FromForm]`，前端使用 `multipart/form-data` 或 `application/x-www-form-urlencoded`。
- 鉴权：需要 `Authorization: Bearer <JWT>`。
- 分页字段：`PageNum`、`PageSize`。
- 新增筛选字段：`LogDateStart`、`LogDateEnd`、`UserAcc`、`ClientIP`、`ActionName`、`Param`、`Result`。
- `Keyword` 可作为综合模糊搜索，覆盖操作用户、IP、接口名称、请求参数、返回结果。
- 所有文本筛选均为模糊搜索；`UserAcc` 同时匹配用户账号和用户显示名。

## 2. 请求参数

接口：`POST /api/Log/GetLogActionList`

请求头：

```http
Authorization: Bearer <JWT>
Content-Type: multipart/form-data
```

表单字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `PageNum` | number | 是 | 当前页，从 1 开始 |
| `PageSize` | number | 是 | 每页数量 |
| `LogDateStart` | string | 否 | 日志开始时间，建议格式 `yyyy-MM-dd HH:mm:ss` |
| `LogDateEnd` | string | 否 | 日志结束时间，建议格式 `yyyy-MM-dd HH:mm:ss` |
| `UserAcc` | string | 否 | 操作用户，模糊匹配账号或显示名 |
| `ClientIP` | string | 否 | IP 地址，模糊匹配 |
| `ActionName` | string | 否 | 接口名称，模糊匹配 |
| `Param` | string | 否 | 请求参数，模糊匹配 |
| `Result` | string | 否 | 返回结果，模糊匹配 |
| `Keyword` | string | 否 | 综合搜索，匹配操作用户、IP、接口名称、请求参数、返回结果 |

时间说明：

- `LogDateStart` 使用 `>=`。
- `LogDateEnd` 使用 `<=`。
- 如果 `LogDateEnd` 只传日期，例如 `2026-07-05`，后端会按 `2026-07-05 23:59:59` 所在整天处理。

## 3. 响应字段

响应结构保持不变：

```json
{
  "success": true,
  "StateCode": 0,
  "Total": 100,
  "data": [
    {
      "LogDate": "2026-07-05 10:30:00",
      "UserAcc": "张三",
      "ActionName": "/api/User/GetUserList",
      "ClientIP": "192.168.1.10",
      "Param": "{\"PageNum\":1,\"PageSize\":20}",
      "RequestType": "POST",
      "Result": "{\"success\":true}"
    }
  ]
}
```

字段说明：

- `Total`：总记录数，用于分页。
- `data[].LogDate`：日志时间，格式 `yyyy-MM-dd HH:mm:ss`。
- `data[].UserAcc`：优先返回用户显示名；用户不存在时返回账号。
- `data[].ActionName`：接口名称或请求路径。
- `data[].ClientIP`：客户端 IP。
- `data[].Param`：请求参数。
- `data[].RequestType`：请求类型。
- `data[].Result`：返回结果。

## 4. Vue 调用示例

### 4.1 API 封装

```js
// api/systemLog.js
import request from '@/utils/request'

export function getLogActionList(params) {
  const formData = new FormData()

  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      formData.append(key, value)
    }
  })

  return request.post('/api/Log/GetLogActionList', formData)
}
```

### 4.2 页面查询参数

```js
import { reactive } from 'vue'
import { getLogActionList } from '@/api/systemLog'

const query = reactive({
  PageNum: 1,
  PageSize: 20,
  LogDateStart: '',
  LogDateEnd: '',
  UserAcc: '',
  ClientIP: '',
  ActionName: '',
  Param: '',
  Result: '',
  Keyword: ''
})

async function loadLogs() {
  const res = await getLogActionList(query)
  tableData.value = res.data ?? []
  total.value = res.Total ?? 0
}
```

### 4.3 日期范围适配

如果使用 Element Plus：

```vue
<el-date-picker
  v-model="logDateRange"
  type="datetimerange"
  value-format="YYYY-MM-DD HH:mm:ss"
  start-placeholder="开始时间"
  end-placeholder="结束时间"
/>
```

提交前把日期范围拆成后端字段：

```js
query.LogDateStart = logDateRange.value?.[0] ?? ''
query.LogDateEnd = logDateRange.value?.[1] ?? ''
query.PageNum = 1
await loadLogs()
```

## 5. 筛选表单建议

- 时间范围：绑定 `LogDateStart`、`LogDateEnd`。
- 操作用户：绑定 `UserAcc`，可输入账号或显示名。
- IP 地址：绑定 `ClientIP`。
- 接口名称：绑定 `ActionName`。
- 请求参数：绑定 `Param`。
- 返回结果：绑定 `Result`。
- 综合搜索框：绑定 `Keyword`，适合顶部快速搜索。

多个字段同时传入时，后端按 AND 组合筛选；`Keyword` 内部按 OR 匹配多个日志字段。
