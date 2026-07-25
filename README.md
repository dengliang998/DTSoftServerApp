# DTSoft Server App

DTSoft Server App 是一个基于 ASP.NET Core 的后台服务项目，提供组织、用户、角色、菜单、附件、系统配置、日志、API Key 和动态应用接口等能力。项目使用 JWT 认证、Entity Framework Core、多数据库 Provider、Scalar OpenAPI 文档、Serilog 日志和可选 Redis 缓存。

## 技术栈

- .NET `net10.0`
- ASP.NET Core Web API
- Entity Framework Core
- Scalar OpenAPI
- Serilog
- Yitter.IdGenerator
- MiniExcel
- ServiceStack.Redis
- 支持数据库：`MySql`、`SqlServer`、`Oracle`、`PostgreSql`

## 项目结构

```text
.
├── DTSoftServerApp/          # Web API 启动项目、控制器、中间件、配置
├── DTSoft.AppService/        # 业务应用服务
├── DTSoft.Core/              # 数据库、缓存、工具类、HTTP 辅助能力
├── DTSoft.Models/            # 实体、参数 DTO、枚举
└── DTSoftServerApp.slnx      # 解决方案文件
```

## 核心能力

### 认证与访问控制

- `Auth` 提供登录加密公钥、验证码、账号密码登录和 JWT Token 签发。
- 登录密码使用前端 RSA-OAEP-SHA256 加密传输，后端按配置进行密码哈希校验。
- `ApiKeyAuth` 支持 API Key 创建、管理和换取 Token，适合外部系统集成。
- Web API 中间件负责 Token 校验、账号状态检查和操作日志采集。

### 组织、用户与权限

- `User` / `Department` 提供组织、用户、头像、直属主管、在线用户和密码维护能力。
- `Role` 提供角色维护、角色成员维护。
- `Menu` 提供菜单树、菜单授权、菜单权限检查和动态菜单入口配置。
- 权限链路以菜单和角色授权为核心，前端菜单展示和后端权限检查共用同一套配置基础。

### 系统配置与运行管理

- `SysConfig` 维护系统名称、登录背景图、浏览器 Tab 小 Logo、登录验证码开关和后台主题配置。
- 系统配置会写入缓存，减少登录页和前端初始化时的重复读取。
- 系统初始化可在启动时创建数据库结构、管理员账号、基础角色、菜单和授权。
- 系统运行信息接口可返回应用版本、运行时、服务器、内存和数据库连接状态。

### 附件、日志与基础服务

- `File` 提供附件上传、下载、列表、删除和静态文件访问。
- `Log` 提供操作日志查询，配合中间件记录业务请求。
- 数据库访问由 EF Core 承载，支持 `MySql`、`SqlServer`、`Oracle`、`PostgreSql` Provider。
- 缓存可使用内存缓存或 Redis，文件存储路径通过配置控制。

### 动态应用

- `DynamicApp` / `DynamicApi` 提供动态业务模型配置和运行时 CRUD 接口。
- 支持按配置生成列表、查询、详情、表单、导入、导出等接口能力。
- 动态表访问通过统一服务层处理数据库连接、字段配置、查询条件和数据写入。

### 动态 WebAPI 插件

- 宿主启动时可扫描 `UserDll` 插件目录，加载外部 DLL 中的 Web API Controller。
- 插件可通过 `DTSoft.Plugin.Abstractions` 获取 `IPluginContext`，访问当前用户、请求上下文、配置、数据库和宿主开放的应用服务。
- 插件可实现 `IDynamicWebApiPlugin` 注册自己的服务，也可实现 `IPluginEntityModelConfiguration` 注册插件实体模型。
- 插件路由由插件 Controller 自行声明，典型路径如 `/api/plugin/user/Demo/Me`。
- 插件适合交付后扩展专用业务接口，详细开发方式见 [docs/DynamicWebApiPlugins.md](docs/DynamicWebApiPlugins.md)。

## 快速开始

### 1. 准备环境

安装 .NET 10 SDK，并准备一个可访问的数据库。默认配置使用 PostgreSQL，数据库名为 `DTSoftDB`。

如需使用 Redis，将 `Cache:Provider` 配置为 `Redis` 并填写 `Cache:Redis` 连接信息；本地开发可以先使用默认的 `Memory`。

### 2. 配置应用

主要配置文件位于 [DTSoftServerApp/appsettings.json](DTSoftServerApp/appsettings.json)。

常用配置项：

| 配置项 | 说明 |
| --- | --- |
| `Application:Initialization:RunOnStartup` | 启动时自动检查并初始化数据库 |
| `urls` | 应用监听地址，当前配置为 `http://*:8000` |
| `ApiDocumentation:Enabled` | 是否启用 `/apidoc` 接口文档 |
| `Authentication:Jwt:SigningKey` / `Authentication:Jwt:Issuer` / `Authentication:Jwt:Audience` | JWT 签名和校验配置 |
| `Security:PasswordHashing:Iterations` | 密码哈希 PBKDF2 迭代次数 |
| `Database:Provider` | 数据库类型：`MySql`、`SqlServer`、`Oracle`、`PostgreSql` |
| `ConnectionStrings:Default` | 数据库连接字符串 |
| `Cache:Provider` | 缓存实现：`Memory` 或 `Redis` |
| `Cache:Redis:Host` / `Cache:Redis:Port` / `Cache:Redis:Password` | Redis 连接配置 |
| `Storage:RootPath` / `Storage:Attachments:Directory` / `Storage:Users:Directory` | 文件和附件存储路径 |
| `DynamicWebApi:Enabled` / `DynamicWebApi:PluginDirectory` | 是否启用动态 WebAPI 插件，以及插件 DLL 扫描目录 |

建议在本地或部署环境通过环境变量覆盖敏感配置，不要把真实数据库密码、Redis 密码和生产 JWT 密钥提交到仓库。例如：

```bash
export Database__Provider=PostgreSql
export ConnectionStrings__Default='Host=localhost;Port=5432;Database=DTSoftDB;Username=postgres;Password=your_password;Pooling=true;Maximum Pool Size=512;Minimum Pool Size=5;SSL Mode=Disable'
export Authentication__Jwt__SigningKey='replace_with_a_long_random_secret'
```

### 3. 还原、构建、运行

```bash
dotnet restore DTSoftServerApp.slnx
dotnet build DTSoftServerApp.slnx
dotnet run --project DTSoftServerApp/DTSoftServerApp.csproj
```

使用 `dotnet run` 的开发配置时，`launchSettings.json` 中的默认 HTTP 地址是：

```text
http://localhost:5190
```

配置文件中的 `urls` 是：

```text
http://localhost:8000
```

如果需要按 `appsettings.json` 的端口运行，可以禁用 launch profile 或显式指定地址。

```bash
dotnet run --project DTSoftServerApp/DTSoftServerApp.csproj --no-launch-profile -- --urls http://localhost:8000
```

### 4. 访问接口文档

当 `ApiDocumentation:Enabled` 为 `true`，或运行环境为 `Development` 时，启动后访问：

```text
http://localhost:5190/apidoc
```

或：

```text
http://localhost:8000/apidoc
```

实际地址取决于当前启动端口。

## 数据库初始化

当 `Application:Initialization:RunOnStartup` 为 `true` 时，应用启动会调用系统初始化逻辑：

- 使用 EF Core `EnsureCreated()` 创建数据库表结构
- 初始化管理员账号：`admin`
- 初始化管理员默认密码：`admin123`
- 初始化基础角色、菜单和菜单授权

首次启动前需要保证数据库服务可连接，并且连接账号有创建数据库或创建表的权限。

## 认证示例

登录接口：

```http
GET /api/Auth/login-encryption-key
GET /api/Auth/captcha
POST /api/Auth/login
Content-Type: application/json

{
  "Username": "<RSA-OAEP-SHA256 加密后的用户名 Base64>",
  "Password": "<RSA-OAEP-SHA256 加密后的密码 Base64>",
  "EncryptionKeyId": "<公钥 KeyId>",
  "CaptchaId": "<验证码 ID>",
  "CaptchaCode": "<验证码>"
}
```

登录前端需要先获取登录加密公钥，按 [DTSoftServerApp/Docs/Auth.LoginEncryption.Vue.md](DTSoftServerApp/Docs/Auth.LoginEncryption.Vue.md) 适配。验证码继续按 [DTSoftServerApp/Docs/Auth.Captcha.Vue.md](DTSoftServerApp/Docs/Auth.Captcha.Vue.md) 的现有逻辑提交。成功后响应中会返回 `Data.Token`。调用需要认证的接口时添加请求头：

```http
Authorization: Bearer <token>
```

## 接口约定

- 大部分业务控制器路由格式为：`/api/{Controller}/{Action}`
- 登录接口为：`POST /api/Auth/login`
- API Key 登录接口为：`POST /api/ApiKeyAuth/login`
- 动态 CRUD 接口格式为：`/api/{modelName}`、`/api/{modelName}/{id}`、`/api/{modelName}/import`、`/api/{modelName}/export`
- JSON 序列化保留 PascalCase，不使用默认 camelCase
- 用户、角色、部门、菜单、附件等部分接口使用 `FromForm`
- Auth、API Key、动态配置等部分接口使用 JSON body

更完整的接口参数以 `/apidoc` 为准。直属主管相关接口说明见 [DTSoftServerApp/Docs/User.Supervisor.API.md](DTSoftServerApp/DTSoftServerApp/Docs/User.Supervisor.API.md)。

## 系统配置与上传限制

系统配置接口位于 `SysConfig` 模块：

- `GET /api/SysConfig/GetSystemInfo`：获取系统名称、登录背景图、登录验证码开关、Tab 小 Logo 和主题配置。
- `POST /api/SysConfig/SetSystemInfo`：使用 `multipart/form-data` 保存系统配置。

为保证登录页加载速度并避免前端缓存超限，系统配置上传会做服务端校验：

| 字段 | 用途 | 大小限制 | 支持格式 |
| --- | --- | --- | --- |
| `LoginImg` | 登录页背景图 | `1MB` | `JPG`、`PNG`、`WebP` |
| `BrowserLogo` | 浏览器 Tab 小 Logo | `256KB` | `JPG`、`PNG`、`WebP`、`ICO`、`SVG` |

校验失败时接口返回 `success=false`，并通过 `Msg` 返回具体原因。前端系统设置页也有同样的上传限制，但服务端校验仍然是最终约束。

## 日志与文件

- Serilog 配置文件：[DTSoftServerApp/serilog.json](DTSoftServerApp/DTSoftServerApp/serilog.json)
- 日志默认写入：`DTSoftServerApp/Logs/log-*.txt`
- 附件默认根目录：`Attachment`
- 静态文件通过 `UseStaticFiles()` 提供访问

## 部署提示

发布示例：

```bash
dotnet publish DTSoftServerApp/DTSoftServerApp.csproj -c Release -o publish
```
