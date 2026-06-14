# 用户直属主管接口开发文档（前端适配 / AI 友好）

适用范围：DTSoftServerApp “用户管理”接口（`UserController`）中与 **直属主管** 相关的字段与维护方式。

---

## 1. 快速结论（给前端同学）

- 基础路径：`/api/User/{Action}`
- 传参方式：当前 `UserController` 使用 `[FromForm]`，因此前端按 `multipart/form-data` 或 `application/x-www-form-urlencoded` 方式提交字段（不要用 JSON body）。
- 鉴权：除头像上传/获取外，均需要 `Authorization: Bearer <JWT>`
- 新增字段：`SupervisorAcc`（直属主管账号）
- 查询返回新增字段：
  - `GetUserList`：`SupervisorAcc`、`SupervisorDisplayName`
  - `GetUserDetailByAccount`：`SupervisorAcc`、`SupervisorDisplayName`
- 维护入口：
  - `CreateUser`：可选传 `SupervisorAcc`
  - `ModifyUserInfo`：可选传 `SupervisorAcc`（传空/不传均可表示“不修改或清空”，见 3.2）

---

## 2. 数据模型

### 2.1 直属主管关系表

表名：`sys_user_supervisor`

- `ItemId`：`bigint`，雪花 ID
- `UserAcc`：`varchar(50)`/`nvarchar(50)`，用户账号（下属），唯一（一个用户最多一条直属主管记录）
- `SupervisorAcc`：`varchar(50)`/`nvarchar(50)`，直属主管账号（上级）

约束：
- `UserAcc`、`SupervisorAcc` 均外键引用 `sys_user(Account)`
- `UserAcc` 唯一索引：`UX_sys_user_supervisor_useracc`

---

## 3. 接口对接

### 3.1 获取用户列表：返回直属主管信息

接口：`POST /api/User/GetUserList`

请求（表单字段）：

- `DepartmentId`：必填，部门 ID（不传则返回空列表）
- `PageNum`、`PageSize`、`Keyword`：同现有逻辑

响应 `data[]` 每行新增字段：

```json
{
  "Account": "zhangsan",
  "DisplayName": "张三",
  "DepartmentId": 1001,
  "SupervisorAcc": "lisi",
  "SupervisorDisplayName": "李四"
}
```

### 3.2 获取用户详情：返回直属主管信息

接口：`POST /api/User/GetUserDetailByAccount`

请求（表单字段）：
- `account`：必填，用户账号

响应新增字段：
- `SupervisorAcc`
- `SupervisorDisplayName`

### 3.3 创建用户：可维护直属主管

接口：`POST /api/User/CreateUser`

请求（表单字段，摘录）：
- `Account`：必填
- `PassWord`：必填
- `DepartmentId`：可选
- `SupervisorAcc`：可选（直属主管账号）

业务规则：
- `SupervisorAcc` 为空：不设置直属主管
- `SupervisorAcc` 非空：必须是存在的用户账号，且不能等于 `Account`
- 自动防止形成循环主管链（A 的主管是 B，B 的主管又指回 A 这种情况会被拒绝）

### 3.4 修改用户：可维护/清空直属主管

接口：`POST /api/User/ModifyUserInfo`

请求（表单字段，摘录）：
- `Account`：必填
- `SupervisorAcc`：可选

建议前端约定（避免歧义）：
- **保持不变**：不要传 `SupervisorAcc` 字段（后端会据此判断“不更新直属主管”）
- **清空直属主管**：传 `SupervisorAcc` 为空字符串（或显式传 `null` 等价方式由前端实现）
- **设置直属主管**：传具体账号

---

## 4. 常见错误与提示

- `直属主管不能是自己`
- `直属主管账号不存在：{SupervisorAcc}`
- `设置失败：会形成循环的主管链`

---

## 5. 与流程引擎的联动（StarterManager）

当工作流节点处理人类型为 `StarterManager` 时：

- 系统会 **优先** 读取 `sys_user_supervisor` 中发起人的 `SupervisorAcc` 作为“直属上级”。
- 若未维护直属主管，才会回退到旧逻辑（同部门 `Position` 含“经理”的用户）。
