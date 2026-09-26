# OpenFeign 测试手册（Runtime-proxy + AOT）

本手册覆盖 `Yzl.Extensions.Http.OpenFeign` 的两种客户端形态，二者**共享同一套后端 API 与请求接口定义**，区别只在注册方式与代理生成时机：

| 形态 | 项目 | 端口 | 注册方式 | 代理生成 |
|------|------|------|---------|---------|
| **Runtime-proxy** | `src/Yzl.Extensions.Samples.OpenFeign` | `16602` | `AddFeignStarter(...)` | 运行时 Castle DynamicProxy |
| **AOT / 源码生成器** | `src/Yzl.Extensions.Samples.OpenFeign.AOT` | `16603` | `AddOpenFeignAot(...).AddGeneratedFeignClients()` | 编译期 Source Generator，不依赖 DynamicProxy |

后端 API 统一为 `src/Samples.Api`（端口 **16600**）。

---

## 0. 启动

```bash
# 终端 1：后端 API
dotnet run --project src/Samples.Api
# 监听 http://localhost:16600

# 终端 2：Runtime-proxy 客户端
dotnet run --project src/Yzl.Extensions.Samples.OpenFeign
# 监听 http://localhost:16602，仪表盘 /dashboard

# 终端 3（可选）：AOT 客户端
dotnet run --project src/Yzl.Extensions.Samples.OpenFeign.AOT
# 监听 http://localhost:16603
```

---

## 1. 测试接口（后端 API）

### 1.1 接口定义

后端接口位于 [Samples.Api/Controllers/TestController.cs](../../Samples.Api/Controllers/TestController.cs)：

```csharp
[ApiController]
[HttpRequestLog]
[TestDashboardInfo("🧪 综合测试", Order = 1)]
[Route("api/test")]
public sealed class TestController : ControllerBase
{
    [HttpGet("ping")]
    public string Ping() => "pong";

    [HttpGet("users/{id:long}")]
    public UserDto GetById(long id) => new(id, "Alice", 20, "Shanghai");

    [HttpGet("users/{id:long}/getbyid2")]
    public ResponseResult<UserDto> GetById2(long id)
        => new(0, new UserDto(id, "GetById2 User", 25), "success");

    [HttpGet("users/{id:long}/getbyid3")]
    public object GetById3(long id)
        => new { status = 200, result = new UserDto(id, "GetById3 User", 26), message = "ok" };

    [HttpGet("users/query")]
    public object Query([FromQuery] long id, [FromQuery] string name)
        => new { id, name, source = "RequestParam" };

    [HttpGet("users/map")]
    public IDictionary<string, string?> QueryMap()
        => Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString());

    [HttpPost("users")]
    public UserDto Create(CreateUserRequest request)
        => new(2001, request.Name, request.Age, request.City);

    [HttpPut("users/{id:long}")]
    public UserDto Update(long id, UpdateUserRequest request)
        => new(id, request.Name, request.Age, request.City);

    [HttpDelete("users/{id:long}")]
    public bool Delete(long id) => id > 0;

    [HttpGet("users/{id:long}/not-data")]
    public object NotData(long id)
        => new { code = 0, info = new UserDto(id, "NoDataField User", 30), msg = "no data field" };

    [HttpGet("headers")]
    public object Headers([FromHeader(Name = "X-Token")] string? token)
        => new { token, requestSource = "header" };

    [HttpGet("timeout")]
    public async Task<string> Timeout(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        return "timeout endpoint finished";
    }

    [HttpHead("head")]
    public IActionResult Head()
    {
        Response.Headers.Append("X-Demo-Head", "ok");
        return Ok();
    }

    [HttpGet("files/abc.doc")]
    public FileContentResult DownloadFile()
    {
        var bytes = Encoding.UTF8.GetBytes("OpenFeign file download test content.");
        return File(bytes, "application/msword", "abc.doc");
    }
}
```

### 1.2 `[HttpRequestLog]` 响应日志

`[HttpRequestLog]` 是 `Yzl.Extensions.Core.Filters` 提供的 Action Filter，会把每个请求的方法、URL、查询参数、请求头与请求体打到日志。启动 `Samples.Api` 后访问任一接口，控制台可见：

```text
info: Samples.Api.Controllers.TestController[0]
      Controller: Test, Action: Ping
      Method: GET
      Scheme: http
      Host: localhost:16600
      Path: /api/test/ping
      ContentType: [Not Provided]
      Full URL: http://localhost:16600/api/test/ping
      Headers:
        Accept: application/json
        Host: localhost:16600
        User-Agent: Spring.Net.Feign/1.0
        Authorization: [Redacted]
        traceparent: 00-412e8f9302567c6bd34bacdcf0b2f764-4d6869e1cb52b202-00
        source: PC111
        a1: a

info: Samples.Api.Controllers.TestController[0]
      Controller: Test, Action: GetById
      Method: GET
      Scheme: http
      Host: localhost:16600
      Path: /api/test/users/1
      ContentType: [Not Provided]
      Full URL: http://localhost:16600/api/test/users/1
      Headers:
        Accept: application/json
        Host: localhost:16600
        User-Agent: Spring.Net.Feign/1.0
        Authorization: [Redacted]
        traceparent: 00-412e8f9302567c6bd34bacdcf0b2f764-47c4ef3e657135ea-00
        source: PC111
        a1: a

info: Samples.Api.Controllers.TestController[0]
      Controller: Test, Action: Query
      Method: GET
      Scheme: http
      Host: localhost:16600
      Path: /api/test/users/query
      QueryString: ?id=1&name=Tom
      Query Parameters:
        id: 1
        name: Tom
      ContentType: [Not Provided]
      Full URL: http://localhost:16600/api/test/users/query?id=1&name=Tom
      Headers:
        Accept: application/json
        Host: localhost:16600
        User-Agent: Spring.Net.Feign/1.0
        Authorization: [Redacted]
        traceparent: 00-412e8f9302567c6bd34bacdcf0b2f764-969b5f4dca9eb157-00
        source: PC111
        a1: a

info: Samples.Api.Controllers.TestController[0]
      Controller: Test, Action: QueryMap
      Method: GET
      Scheme: http
      Host: localhost:16600
      Path: /api/test/users/map
      QueryString: ?age=18&city=bj
      Query Parameters:
        age: 18
        city: bj
      ContentType: [Not Provided]
      Full URL: http://localhost:16600/api/test/users/map?age=18&city=bj
      Headers:
        Accept: application/json
        Host: localhost:16600
        User-Agent: Spring.Net.Feign/1.0
        Authorization: [Redacted]
        traceparent: 00-412e8f9302567c6bd34bacdcf0b2f764-36e6bc474ae71229-00
        source: PC111
        a1: a

info: Samples.Api.Controllers.TestController[0]
      Controller: Test, Action: Create
      Method: POST
      Scheme: http
      Host: localhost:16600
      Path: /api/test/users
      ContentType: application/json
      Full URL: http://localhost:16600/api/test/users
      Headers:
        Accept: application/json
        Host: localhost:16600
        User-Agent: Spring.Net.Feign/1.0
        Authorization: [Redacted]
        Content-Type: application/json
        traceparent: 00-412e8f9302567c6bd34bacdcf0b2f764-5fa2c022cab3ac58-00
        Transfer-Encoding: chunked
        source: PC111
        a1: a
      Body: {"Name":"Jerry","Age":20}

info: Samples.Api.Controllers.TestController[0]
      Controller: Test, Action: Update
      Method: PUT
      Scheme: http
      Host: localhost:16600
      Path: /api/test/users/1
      ContentType: application/json
      Full URL: http://localhost:16600/api/test/users/1
      Headers:
        Accept: application/json
        Host: localhost:16600
        User-Agent: Spring.Net.Feign/1.0
        Authorization: [Redacted]
        Content-Type: application/json
        traceparent: 00-412e8f9302567c6bd34bacdcf0b2f764-7bb7cf7d7fc810ed-00
        Transfer-Encoding: chunked
        source: PC111
        a1: a
      Body: {"Name":"Bob","Age":30}

info: Samples.Api.Controllers.TestController[0]
      Controller: Test, Action: Headers
      Method: GET
      Scheme: http
      Host: localhost:16600
      Path: /api/test/headers
      ContentType: [Not Provided]
      Full URL: http://localhost:16600/api/test/headers
      Headers:
        Accept: application/json
        Host: localhost:16600
        User-Agent: Spring.Net.Feign/1.0
        Authorization: [Redacted]
        traceparent: 00-412e8f9302567c6bd34bacdcf0b2f764-6c0eddf3bcabb199-00
        X-Token: token-123
        source: PC111
        a1: a
```

> `Authorization` 恒为 `[Redacted]`（脱敏）；`traceparent` 来自 .NET 8 内建 `Activity`（W3C Trace Context）。

---

## 2. Feign 客户端接口

### 2.1 请求接口定义

`ITestApiFeignClient`（[src/Yzl.Extensions.Samples.OpenFeign/Acs/ITestApiFeignClient.cs](../Acs/ITestApiFeignClient.cs)）：

```csharp
[FeignClient(name: "test", url: "http://localhost:16600", fallback: typeof(TestApiFeignClientFallback), timeout: 5000)]
public interface ITestApiFeignClient
{
    [Get("/api/test/ping")]
    Task<string> Ping();

    [Get("/api/test/users/{id}")]
    Task<UserDto> GetById([PathVariable("id")] long id);

    [Get("/api/test/users/{id}")]
    UserDto GetByIdSync([PathVariable("id")] long id);

    [Get("/api/test/users/query")]
    Task<object> Query([RequestParam("id")] long id, [RequestParam("name")] string name);

    [Get("/api/test/users/map")]
    Task<object> QueryMap([QueryMap] Dictionary<string, string> map);

    [Post("/api/test/users")]
    Task<UserDto> Create([RequestBody] CreateUserRequest req);

    [Put("/api/test/users/{id}")]
    Task<UserDto> Update([PathVariable("id")] long id, [RequestBody] UpdateUserRequest req);

    [Delete("/api/test/users/{id}")]
    Task<bool> Delete([PathVariable("id")] long id);

    [Get("/api/test/headers")]
    Task<object> Headers([RequestHeader("X-Token")] string token);

    [Head("/api/test/head")]
    Task Head();

    [Get("/api/test/files/abc.doc")]
    Task<Stream> DownloadAsync();

    [Get("/api/test/files/abc.doc")]
    Stream Download();

    [Get("/api/test/files/abc.doc")]
    Task<byte[]> DownloadBytesAsync();

    [Get("/api/test/timeout", timeout: 7000)]
    string TimeOut([RequestHeader] string a = "123",
                   [RequestHeader(name: "user-token", Encoded = true)] string utk = "你好");

    [Sse(CompleteField = "completeSucc")]
    [Get("/api/RandomChinese/stream")]
    IAsyncEnumerable<RandomChineseSseDto> RandomChinese();

    [Sse(CompleteField = "completeSucc")]
    [Get("/api/RandomChinese/stream")]
    ISseStream<RandomChineseSseDto> RandomChinese2();
}
```

> AOT 版本接口定义相同，仅命名空间不同（`Yzl.Extensions.Samples.OpenFeign.AOT.Acs`）。AOT 下 `url` 使用配置占位符 `{DemoApi:BaseUrl}`。

### 2.2 注册方式对比

**Runtime-proxy**（[Program.cs](../Program.cs)）：

```csharp
builder.Services.AddFeignStarter(builder.Configuration, options =>
{
    options.SerializerType = typeof(SystemTextJsonFeignSerializer);
});
```

**AOT / 源码生成器**（[AOT/Program.cs](../../Yzl.Extensions.Samples.OpenFeign.AOT/Program.cs)）：

```csharp
builder.Services
    .AddOpenFeignAot(builder.Configuration)
    .AddFeignResponseResolver<SampleResponseResolver>()
    .AddFeignRequestHeaderProvider<DemoHeaderProvider>()
    .AddGeneratedFeignClients();   // 由 Source Generator 生成的注册方法
```

### 2.3 测试代码

```csharp
app.MapGet("/feign-api-test", async (ITestApiFeignClient client) => new
{
    ping = await client.Ping(),
    user = await client.GetById(1),
    query = await client.Query(1, "Tom"),
    map = await client.QueryMap(new Dictionary<string, string> { { "age", "18" }, { "city", "bj" } }),
    create = await client.Create(new CreateUserRequest("Jerry", 20)),
    update = await client.Update(1, new UpdateUserRequest("Bob", 30)),
    header = await client.Headers("token-123")
});
```

---

## 3. 端到端测试用例

两个客户端暴露的测试端点一致（AOT 版本无 `/feign-api-test`）：

| # | 端点 | 演示内容 | 期望 |
|---|------|---------|------|
| 1 | `GET /demo/basic` | CRUD 全流程（`GetById` / `GetByIdSync` / `Query` / `QueryMap` / `Create` / `Update` / `Delete`） | 全部返回 200，字段与后端一致 |
| 2 | `GET /demo/methods` | `HEAD` / `OPTIONS` / `TRACE` / `PATCH` | 全部成功；`head = "completed"` |
| 3 | `GET /demo/body` | 5 种请求体：对象 / 字符串 / `byte[]` / `Stream` / `HttpContent` | 5 项均回显成功 |
| 4 | `GET /demo/advanced` | `Headers` / `RawFormat=false` / `RawFormat=true` / 自定义 `IFeignResponseResolver` / 超时降级 | `timeoutFallback` 返回降级结果而非异常 |
| 5 | `GET /demo/sse` | SSE 流（`IAsyncEnumerable` + `ISseStream`） | `text/event-stream`，两阶段事件 + `done` |
| 6 | `GET /download-async` / `download-sync` / `download-bytes` | `Stream` / `byte[]` 返回类型下载 | 内容为 `OpenFeign file download test content.` |
| 7 | `GET /read-all` | 一次性跑完全部演示 | 汇总对象 |
| 8 | `GET /feign-api-test` | `ITestApiFeignClient` 全功能（含超时 / 下载） | 汇总对象 |

**测试命令：**

```bash
# 1. CRUD 基础
curl -s http://localhost:16602/demo/basic | jq

# 2. HTTP 方法
curl -s http://localhost:16602/demo/methods | jq

# 3. 请求体类型
curl -s http://localhost:16602/demo/body | jq

# 4. 高级特性（Headers / RawFormat / 自定义解析器 / 超时降级）
curl -s http://localhost:16602/demo/advanced | jq

# 5. SSE 流（逐条推送，需 -N 关闭缓冲）
curl -N http://localhost:16602/demo/sse

# 6. 文件下载
curl -s http://localhost:16602/download-async
curl -s http://localhost:16602/download-sync
curl -s http://localhost:16602/download-bytes

# 7. 组合测试
curl -s http://localhost:16602/read-all | jq

# 8. ITestApiFeignClient 全功能
curl -s http://localhost:16602/feign-api-test | jq

# AOT 客户端：端口换 16603
curl -s http://localhost:16603/demo/basic | jq
curl -s http://localhost:16603/read-all | jq
```

**关键断言：**

- `/demo/advanced` 的 `timeoutFallback`：后端 `/api/users/timeout` 延迟 3s，Feign 客户端 `timeout` 属性更短 → 触发 `fallback` 降级，**不抛异常**
- `/demo/sse` 输出首行必须是 `event: phase`，最后必须是 `event: done`
- `/feign-api-test` 的 `header` 字段应含 `X-Token: token-123`，且 `Authorization` 由 `DemoHeaderProvider` 全局注入为 `Bearer demo-token`

---

## 4. 全局请求头注入

`DemoHeaderProvider` 实现 `IFeignRequestHeaderProvider`，对**所有** Feign 请求注入请求头：

```csharp
public sealed class DemoHeaderProvider : IFeignRequestHeaderProvider
{
    public int Order => -100;

    public void Apply(IDictionary<string, string> headers)
    {
        headers.TryAdd("X-Demo-Global", "from-header-provider");
        headers.TryAdd("Authorization", "Bearer demo-token");
    }
}
```

验证：在 `Samples.Api` 的 `[HttpRequestLog]` 日志中应能看到 `X-Demo-Global`，`Authorization` 显示为 `[Redacted]`。

---

## 5. 关键文件索引

| 文件 | 作用 |
|------|------|
| [Samples.Api/Controllers/TestController.cs](../../Samples.Api/Controllers/TestController.cs) | 后端测试接口 + `[HttpRequestLog]` |
| [Acs/ITestApiFeignClient.cs](../Acs/ITestApiFeignClient.cs) | Runtime-proxy 请求接口定义 |
| [Acs/IDemoFeignClient.cs](../Acs/IDemoFeignClient.cs) | CRUD 演示接口（`/api/users`） |
| [Acs/IRequestBodyDemoFeignClient.cs](../Acs/IRequestBodyDemoFeignClient.cs) | 5 种请求体类型演示 |
| [Acs/ISseDemoFeignClient.cs](../Acs/ISseDemoFeignClient.cs) | SSE 流演示 |
| [Acs/IDownloadClient.cs](../Acs/IDownloadClient.cs) | 文件下载演示 |
| [HeaderProviders/DemoHeaderProvider.cs](../HeaderProviders/DemoHeaderProvider.cs) | 全局请求头注入 |
| [Program.cs](../Program.cs) | Runtime-proxy 注册 + 全部测试端点 |
| [AOT/Program.cs](../../Yzl.Extensions.Samples.OpenFeign.AOT/Program.cs) | AOT 注册（`AddOpenFeignAot` + `AddGeneratedFeignClients`） |
