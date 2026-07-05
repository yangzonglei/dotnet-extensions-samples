# dotnet-extensions-samples

[Yzl.Extensions](https://github.com/yangzonglei/dotnet-extensions) 基础设施包的示例项目集合。

## 项目定位

- **开发者验证**：配合 `dotnet-extensions` 源码进行功能调试与集成测试
- **用法演示**：对外展示各 NuGet 包的用法，作为开源使用者的参考

## 构建方式

```bash
# NuGet 模式（默认）：使用已发布的 NuGet 包
dotnet build eng/build.proj

# 源码模式：引用本地 dotnet-extensions/src 源码（需 ../dotnet-extensions 存在）
dotnet build eng/build.proj -p:UseProjectReference=true
```

> `dotnet-extensions` 和 `dotnet-extensions-samples` 需为同级目录：
> ```
> dotnet/
> ├── dotnet-extensions/           # 源码仓库
> └── dotnet-extensions-samples/   # 示例仓库（当前）
> ```

---

## 项目结构

```
src/
├── Samples.Api/                                # 16600 - OpenFeign 后端 API（所有需要服务端接口的测试 API 项目）
├── Yzl.Extensions.Samples.Actuator/            # 16601 - Actuator 端点演示（健康检查 / 指标 / 环境 / 日志管理 / 缓存 / Bean）
├── Yzl.Extensions.Samples.OpenFeign/           # 16602 - OpenFeign Runtime-proxy 客户端
├── Yzl.Extensions.Samples.OpenFeign.AOT/       # 16603 - OpenFeign AOT 客户端
├── Yzl.Extensions.Samples.OpenFeign.Net48/     # 16604 - OpenFeign .NET 4.8 客户端
├── Yzl.Extensions.Samples.Cache/               # 16605 - 缓存框架演示
├── Yzl.Extensions.Samples.SpringBoot.Admin.Net/# 16606 - Spring Boot Admin 客户端
├── Yzl.Extensions.Samples.Mcp.Service/         # 16607 - MCP 服务端
├── Yzl.Extensions.Samples.Mcp.WebClient/       # 16608 - MCP Web 聊天客户端
├── Yzl.Extensions.Samples.Mcp.Client/          # 无端口 - MCP 命令行客户端
└── Samples.Models/                    # 无端口 - 共享模型
```

---

## 各项目说明

### Samples.Api

端口 **16600** — 所有需要服务端接口的测试 API 项目。

**启动：**
```bash
dotnet run --project src/Samples.Api
# 监听 http://localhost:16600
```

**端点：**
| 路由 | 说明 |
|------|------|
| `/api/users/{id}` | 用户查询（GET） |
| `/api/users/query` | 带 Query 参数查询 |
| `/api/users/map` | QueryMap 回显 |
| `/api/users/{id}/wrapped` | 返回 ResponseResult 包装格式 |
| `/api/users/{id}/status-result` | 返回 StatusResponseResult 格式 |
| `/api/headers` | 请求头接收 |
| `/api/timeout` | 3秒延迟（测试超时熔断） |
| `/api/sse/stream` | SSE 服务端推送 |
| `/api/download/files/abc.doc` | 文件下载 |

---

### Yzl.Extensions.Samples.Actuator

端口 **16601** — 演示 [Yzl.Extensions.Actuator](https://github.com/yangzonglei/dotnet-extensions) 的 Actuator 端点能力。

**启动：**
```bash
dotnet run --project src/Yzl.Extensions.Samples.Actuator
# 监听 http://localhost:16601
```

**功能：**
| 端点 | 说明 |
|------|------|
| `/actuator` | Actuator 根端点 — HAL 链接列表 |
| `/actuator/health` | 健康检查（Ping + 磁盘 + 自定义组件） |
| `/actuator/info` | 应用信息（构建 / 环境 / 自定义信息） |
| `/actuator/metrics` | 指标（CPU / 内存 / 线程 / GC） |
| `/actuator/metrics/{name}` | 指定指标详情 |
| `/actuator/env` | 环境配置属性 |
| `/actuator/loggers` | 日志级别查看与运行时修改 |
| `/actuator/beans` | DI 容器中注册的所有服务 |
| `/actuator/caches` | 缓存管理（查看 / 清理） |
| `/actuator/mappings` | 路由映射 |
| `/actuator/conditions` | 条件评估报告 |
| `/actuator/metadata` | 应用元数据 |
| `/actuator/httptrace` | HTTP 请求追踪记录 |

**自定义扩展：**
- `CustomHealthContributor` — 自定义健康检查（数据库 / Redis / 外部 API 模拟）
- `CustomInfoContributor` — 自定义信息片段（团队 / 版本 / 功能列表）
- `CustomActuatorEndpoint` — 自定义端点 `/actuator/custom`

**测试面板：**
访问 [/dashboard](http://localhost:16601/dashboard) 在可视界面中测试所有 Actuator 端点（通过代理 Controller 自动发现）。

---

### Yzl.Extensions.Samples.OpenFeign

端口 **16602** — 演示 [Yzl.Extensions.Http.OpenFeign](https://www.nuget.org/packages/Yzl.Extensions.Http.OpenFeign) 声明式 HTTP 客户端（Runtime-proxy 模式）。

**涉及 NuGet 包：** `Yzl.Extensions.Http.OpenFeign`、`NLog.Web.AspNetCore`

**启动：**（先启动 Samples.Api）
```bash
# 终端 1：启动 API
dotnet run --project src/Samples.Api

# 终端 2：启动客户端
dotnet run --project src/Yzl.Extensions.Samples.OpenFeign
# 监听 http://localhost:16602
```

**演示特性：**
| 特性 | 说明 |
|------|------|
| CRUD 操作 | GET/POST/PUT/DELETE/PATCH/HEAD |
| 参数绑定 | `[PathVariable]` / `[RequestParam]` / `[QueryMap]` / `[RequestBody]` / `[RequestHeader]` |
| 响应解包 | `RawFormat`、`ResponseResult<T>`、自定义 `IFeignResponseResolver` |
| 熔断降级 | `fallback` 参数指定降级类 |
| 超时控制 | `timeout` 属性 |
| SSE 流式 | `[Sse]` + `IAsyncEnumerable` / `ISseStream` |
| 文件下载 | `Stream` / `byte[]` 返回类型 |
| 请求头注入 | `IFeignRequestHeaderProvider` 全局注入 |

---

### Yzl.Extensions.Samples.OpenFeign.AOT

端口 **16603** — 演示 [Yzl.Extensions.Http.OpenFeign.AOT](https://www.nuget.org/packages/Yzl.Extensions.Http.OpenFeign.AOT) 声明式 HTTP 客户端（AOT/源码生成器模式）。

**涉及 NuGet 包：** `Yzl.Extensions.Http.OpenFeign.AOT`、`NLog.Web.AspNetCore`

**启动：**（先启动 Samples.Api）
```bash
# 终端 1：启动 API
dotnet run --project src/Samples.Api

# 终端 2：启动 AOT 客户端
dotnet run --project src/Yzl.Extensions.Samples.OpenFeign.AOT
# 监听 http://localhost:16603
```

**与 Runtime-proxy 客户端的区别：**
- 使用 `AddOpenFeignAot()` + `AddGeneratedFeignClients()` 注册
- 编译时通过 Source Generator 生成代理代码，适合 Native AOT 部署
- 不依赖 Castle DynamicProxy

---

### Yzl.Extensions.Samples.OpenFeign.Net48

端口 **16604** — .NET Framework 4.8 控制台应用程序，演示 [Yzl.Extensions.Http.OpenFeign.Net48](https://www.nuget.org/packages/Yzl.Extensions.Http.OpenFeign.Net48) 在传统 .NET Framework 中的用法。

> ⚠️ .NET Framework 4.8 仅在 Windows 上支持构建和运行。

---

### Yzl.Extensions.Samples.Cache

端口 **16605** — 演示 [Yzl.Extensions.Cache](https://www.nuget.org/packages/Yzl.Extensions.Cache) 缓存框架的全部特性。

**涉及 NuGet 包：** `Yzl.Extensions.Cache`

**启动：**
```bash
dotnet run --project src/Yzl.Extensions.Samples.Cache
# 访问 http://localhost:16605/api/samples
```

**演示特性：**
| 特性 | Service 文件 |
|------|-------------|
| 基础 Cacheable | `Services/BasicCacheService.cs` |
| CachePut / CacheEvict 生命周期 | `Services/CacheLifecycleService.cs` |
| 批量清除缓存 | `Services/CacheEvictAllService.cs` |
| SpEL 表达式键 | `Services/SpelKeyService.cs` |
| Condition / Unless 条件缓存 | `Services/ConditionalService.cs` |
| CacheConfig 类级别配置继承 | `Services/ConfigInheritanceService.cs` |
| 异步方法缓存 | `Services/AsyncCacheService.cs` |
| 滑动过期策略 | `Services/SlidingExpirationService.cs` |
| Redis 后端缓存 | `Services/RedisCacheService.cs` |

---

### Yzl.Extensions.Samples.SpringBoot.Admin.Net

端口 **16606** — 演示 [Yzl.Extensions.SpringBoot.Admin.Client.Net](https://www.nuget.org/packages/Yzl.Extensions.SpringBoot.Admin.Client.Net) + Actuator 监控端点。

**涉及 NuGet 包：** `Yzl.Extensions.SpringBoot.Admin.Client.Net`、`Yzl.Extensions.Core`、`NLog.Web.AspNetCore`

**启动：**
```bash
dotnet run --project src/Yzl.Extensions.Samples.SpringBoot.Admin.Net
# 监听 http://localhost:16606
```

**特性：**
- 向 Spring Boot Admin Server 注册客户端（需先运行 SBA Server）
- Actuator 端点：`/actuator/health`、`/actuator/info`、`/actuator/metrics`、`/actuator/loggers`、`/actuator/beans`
- HTTP Trace 中间件（`/actuator/httptrace`）
- NLog 运行时日志级别动态调整

---

### Yzl.Extensions.Samples.Mcp.Service

端口 **16607** — MCP（Model Context Protocol）服务端，演示 [Yzl.Extensions.AI.Mcp](https://github.com/yangzonglei/dotnet-extensions) 的使用。

**涉及 NuGet 包：** `Yzl.Extensions.AI.Mcp`、`Yzl.Extensions.Http.OpenFeign`、`Microsoft.AspNetCore.Authentication.JwtBearer`

**启动：**
```bash
dotnet run --project src/Yzl.Extensions.Samples.Mcp.Service
# 监听 http://localhost:16607
# MCP 端点: /mcp（需 JWT Bearer 认证）
# Token 获取: POST /api/auth/token
```

**注册的 MCP 工具：**
| 工具 | 说明 |
|------|------|
| `calculator/add` | 两数相加 |
| `calculator/subtract` | 两数相减 |
| `calculator/echo` | 重复输入信息 |
| `get_current_time` | 获取当前服务器时间 |
| `days_until` | 计算到指定日期的天数 |
| `is_weekend` | 判断日期是否为周末 |
| `age` | 获取年龄问候语 |
| `GetById` / `GetByIdAsync` | 通过 OpenFeign 调用外部 API 获取用户数据 |

---

### Yzl.Extensions.Samples.Mcp.WebClient

端口 **16608** — Web 聊天客户端，演示如何通过 LLM（Claude / OpenAI 兼容 API）自动调用 MCP 工具完成用户请求。

**涉及 NuGet 包：** `ModelContextProtocol.Core`

**启动：**（先启动 MCP Service）
```bash
# 终端 1：启动 MCP 服务端
dotnet run --project src/Yzl.Extensions.Samples.Mcp.Service

# 终端 2：启动 Web 客户端
dotnet run --project src/Yzl.Extensions.Samples.Mcp.WebClient
# 访问 http://localhost:16608
```

**API 端点：**
| 端点 | 说明 |
|------|------|
| `GET /api/tools` | 获取 MCP 服务器可用工具列表 |
| `POST /api/tools/call` | 手动调用指定 MCP 工具 |
| `POST /api/chat` | SSE 流式智能聊天（自动工具调用） |
| `GET /api/sessions/{sessionId}` | 获取聊天会话历史 |
| `DELETE /api/sessions/{sessionId}` | 清除会话历史 |

**支持两种 LLM 后端：**
- Claude API（默认）
- OpenAI 兼容格式（DeepSeek、通义千问、GLM-4 等）

通过 `appsettings.json` 中的 `LlmProvider` 配置切换。

---

### Samples.Models

共享数据模型，被其他示例项目引用。包含：

| 模型 | 说明 |
|------|------|
| `UserDto` | 用户 DTO（record） |
| `CreateUserRequest` / `UpdateUserRequest` | 创建/更新用户请求 |
| `ResponseResult<T>` | 通用 API 响应包装 |
| `StatusResponseResult<T>` | 自定义状态响应格式 |
| `SseEventDto` / `RandomChineseSseDto` | SSE 流式事件 DTO |
| `BaseEntity` | 实体基类 |

---

### Yzl.Extensions.Samples.Mcp.Client

控制台应用程序，演示如何通过 MCP 协议连接 MCP 服务端并交互式调用工具。

**启动：**
```bash
# 先启动 MCP Service
dotnet run --project src/Yzl.Extensions.Samples.Mcp.Service

# 再启动客户端
dotnet run --project src/Yzl.Extensions.Samples.Mcp.Client
```

支持命令行参数：
```bash
dotnet run --project src/Yzl.Extensions.Samples.Mcp.Client -- [mcpUrl] [tokenEndpoint] [clientId] [clientSecret]
```

---

## 端口汇总

| 项目                   | 端口 |
|----------------------|------|
| Samples.Api          | `16600` |
| Actuator             | `16601` |
| OpenFeign (Proxy)    | `16602` |
| OpenFeign.AOT        | `16603` |
| OpenFeign.Net48      | `16604` |
| Cache                | `16605` |
| SpringBoot.Admin.Net | `16606` |
| Mcp.Service          | `16607` |
| Mcp.WebClient        | `16608` |
