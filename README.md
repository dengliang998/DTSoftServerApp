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

主要模块：

- `Auth`：账号密码登录、JWT Token 签发
- `ApiKeyAuth`：API Key 创建、管理和换取 Token
- `User` / `Department` / `Role` / `Menu`：组织、用户、角色、菜单权限管理
- `File`：附件上传、下载、列表和删除
- `SysConfig`：系统配置和系统初始化
- `Log`：操作日志查询
- `DynamicApp` / `DynamicApi`：动态应用配置和动态表 CRUD / 导入导出接口

## 快速开始

### 1. 准备环境

安装 .NET 10 SDK，并准备一个可访问的数据库。默认配置使用 PostgreSQL，数据库名为 `DTSoftDB`。

如需使用 Redis，将 `CacheModel` 配置为 `RedisCache` 并填写 Redis 连接信息；本地开发可以先使用默认的 `MemoryCache`。

### 2. 配置应用

主要配置文件位于 [DTSoftServerApp/appsettings.json](DTSoftServerApp/DTSoftServerApp/appsettings.json)。

常用配置项：

| 配置项 | 说明 |
| --- | --- |
| `InitializeOnStartup` | 启动时自动检查并初始化数据库 |
| `urls` | 应用监听地址，当前配置为 `http://*:8000` |
| `ScalarEnabled` | 是否启用 `/apidoc` 接口文档 |
| `Jwt:Key` / `Jwt:Issuer` / `Jwt:Audience` | JWT 签名和校验配置 |
| `DBType` | 数据库类型：`MySql`、`SqlServer`、`Oracle`、`PostgreSql` |
| `ConnectionStrings:DBConnection` | 数据库连接字符串 |
| `CacheModel` | 缓存实现：`MemoryCache` 或 `RedisCache` |
| `RootPath` / `AttachmentPath` / `UserData` | 文件和附件存储路径 |

建议在本地或部署环境通过环境变量覆盖敏感配置，不要把真实数据库密码、Redis 密码和生产 JWT 密钥提交到仓库。例如：

```bash
export DBType=PostgreSql
export ConnectionStrings__DBConnection='Host=localhost;Port=5432;Database=DTSoftDB;Username=postgres;Password=your_password;Pooling=true;Maximum Pool Size=512;Minimum Pool Size=5;SSL Mode=Disable'
export Jwt__Key='replace_with_a_long_random_secret'
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

当 `ScalarEnabled` 为 `true`，或运行环境为 `Development` 时，启动后访问：

```text
http://localhost:5190/apidoc
```

或：

```text
http://localhost:8000/apidoc
```

实际地址取决于当前启动端口。

## 数据库初始化

当 `InitializeOnStartup` 为 `true` 时，应用启动会调用系统初始化逻辑：

- 使用 EF Core `EnsureCreated()` 创建数据库表结构
- 初始化管理员账号：`admin`
- 初始化管理员默认密码：`admin123`
- 初始化基础角色、菜单和菜单授权

首次启动前需要保证数据库服务可连接，并且连接账号有创建数据库或创建表的权限。

## 认证示例

登录接口：

```http
POST /api/Auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "admin123"
}
```

成功后响应中会返回 `Data.Token`。调用需要认证的接口时添加请求头：

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
