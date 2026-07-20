# Yzl.Extensions.Http.OpenFeign 更新日志

## v0.1.20 [2026/07/18]

### 🔧 稳定性与可观测性改进

**1. 连接池安全**

- **`MaxConnectionsPerServer` 默认值设为 300**
  `SocketsHttpHandler` 默认 `MaxConnectionsPerServer = int.MaxValue`，下游异常时连接数无上限增长，可能耗尽 ephemeral port。改为 300，超出限制的请求排队等待，不新建连接。

  可通过 `spring:feign:httpClient:pool:maxConnectionsPerServer` 配置覆盖。

- **`HandlerLifetime` 默认值设为 30 分钟（可配置）**
  `IHttpClientFactory` 默认 2 分钟轮转 handler，每 2 分钟触发连接断连重建，高 QPS 下会导致连接建立风暴。改为 30 分钟。

  可通过 `spring:feign:httpClient:handlerLifetime` 配置覆盖。

**2. 同步方法超时保护**

- **新增 `SyncTimeoutMs` 配置项（默认 15000ms）**
  同步 Feign 接口（非 `Task<T>`）使用 `GetAwaiter().GetResult()` 无超时等待，下游异常时线程无限阻塞，导致线程池枯竭。改为 `Task.Wait(syncTimeoutMs)` + `GetAwaiter().GetResult()`，超时后主动抛出 `FeignClientException` 释放线程。

  可通过 `spring:feign:default:syncTimeoutMs` 配置覆盖。

- **新增 `MaxConcurrentRequests` 配置项（默认 0 = 不限制）**
  支持 `SemaphoreSlim` 准入控制，超出并发上限的请求快速失败，防止下游被击穿。

  可通过 `spring:feign:default:maxConcurrentRequests` 配置覆盖。

**3. 异常可观测性**

- **异常消息分类**
  原代码将所有异常笼统包装为 `"Failed to deserialize response to {type}"`，超时、网络断开等场景下均给出误导信息。改为按类型区分：超时、HTTP 请求失败、Socket 错误、JSON 反序列化失败。

- **结构化错误日志**
  调用失败时输出两条结构化日志，包含客户端名、方法名、URL、返回类型、异常链摘要。

- **异常链摘要**
  日志附带 `[ExType1] msg → [ExType2] msg → ...` 格式的异常链摘要，无需展开 InnerException 即可定位根因。

**4. 新配置项汇总**

```yaml
spring:
  feign:
    default:
      syncTimeoutMs: 15000            # 新增，同步方法最大阻塞时间（毫秒）
      maxConcurrentRequests: 0         # 新增，最大并发请求数（0=不限制）
    httpClient:
      handlerLifetime: "00:30:00"      # 新增，Handler 生命周期
      pool:
        maxConnectionsPerServer: 300   # 新增，单主机最大 TCP 连接数
```

**5. 兼容性**

- API 完全向后兼容，无需修改业务代码
- 新配置项均有合理默认值，不配置即使用默认行为
- `MaxConnectionsPerServer` 默认从 `int.MaxValue` 改为 `300`，如果之前依赖无限连接数，需显式设回 `int.MaxValue`

---

## v0.1.19 [2026/06/29]

### 🚀 新功能

1. **`FeignOptions` 改用 `[ConfigurationProperties]` 特性配置绑定**：添加 `[ConfigurationProperties("spring:feign")]` 特性声明配置前缀，通过 `AddConfigurationPropertiesScan(configuration)` 自动扫描注册，替代手动 `services.Configure<T>()` 绑定

### 🔧 修复

1. **更新命名空间引用**：`Yzl.Extensions.Common` → `Yzl.Extensions.Core`，适配 Core 包重命名

2. **修复 `DefaultFeignResponseResolver` 对简单类型返回值的解析 Bug（自定义FeignResponseResolver也可以参考）**

   **问题现象**：当 Feign 接口方法返回 `int`、`long`、`bool` 等简单类型时，即使 API 正确返回了统一响应结构 `{"data":0,"code":0,"msg":"成功"}`，框架也无法正确提取 `data` 字段的值，而是抛出 `"Failed to deserialize response to Int32"` 异常并触发 fallback。

   **根因**：`Resolve` 方法中 `TryGetProperty("data", ...)` 未找到 `data` 字段时回退到根元素 `root`，对于简单类型会尝试将整个 JSON 对象解析为目标类型，导致序列化异常。

   **修复**：
   - `Resolve` 方法改为三阶段清晰流程：**先尝试统一响应结构检查 → 提取 `data` → 再解析目标类型**
   - 统一响应结构（含 `code` 字段）成功但无 `data` 时，返回类型默认值 `default(T)`（值类型）或 `null`（引用类型），不再回退根元素
   - 非统一响应结构的 JSON 仍保持整段反序列化行为不变

   **影响范围**：所有返回简单类型（`int`/`long`/`double`/`decimal`/`bool`等）的 Feign 接口

---

## v0.1.18 [2026/06/10]

### 更新内容（本次无新增功能，只做框架升级）

1. 新增 `Yzl.Extensions.Http.OpenFeign.Abstractions` 公共抽象项目，用于承载运行时代理和 AOT OpenFeign 共用的契约、特性、异常、序列化接口、请求头接口以及 SSE 类型
2. 将 `FeignClient`、请求映射、参数绑定、SSE 等公共特性从运行时代理包与 AOT 包中抽离到 Abstractions 项目，避免两套实现重复定义基础类型
3. 将 `IFeignClientRegistration`、`IOrdered`、`IFeignSerializer`、`IFeignRequestHeaderProvider`、`FeignClientException`、`ISseStream` 等公共接口和类型统一迁移到 Abstractions 项目，便于后续扩展和复用
4. 为运行时代理包和 AOT 包增加对 Abstractions 项目的引用，保持两种 OpenFeign 实现使用同一套公共 API
5. 调整 AOT 源生成器和示例项目的引用与命名空间，适配公共抽象层拆分后的项目结构

---

## v0.1.17 [2026/06/03]

### 更新内容

1. 新增文件下载响应支持，Feign 接口返回 `Stream` 或 `byte[]` 时直接读取二进制内容，避免下载内容被当作字符串或 JSON 解析
2. 优化 `Stream` 下载场景的 HTTP 响应生命周期管理，调用方释放返回流时同步释放底层 `HttpResponseMessage`，避免连接资源泄漏
3. 优化文件下载调试日志，下载响应不再读取和输出响应体，避免日志记录提前消费流或将二进制内容加载到内存
4. 新增文件下载测试接口与测试端点，覆盖异步 `Stream`、同步 `Stream` 和 `byte[]` 下载调用方式

---

## v0.1.16 [2026/05/11]

### 更新内容

1. 修复 GET 请求复杂类型参数序列化逻辑，避免复杂对象作为查询参数时序列化不正确
2. 优化序列化器接口与代理工厂实现，减少无用日志输出并简化 FeignClient 代理创建流程
3. 优化 SSE 处理性能，缓存 Complete 字段 getter，减少流式解析过程中的反射开销
4. 重构 PathVariablePayload，预先生成占位符，减少运行时字符串分配，降低 GC 压力
5. 重构路径变量处理逻辑，支持自定义占位符格式
6. 重构参数解析器接口和实现，引入参数解析器注册表，提升参数解析性能与扩展性
7. 支持单文件部署
8. 依赖 Yzl.Extensions.Common v0.1.4

---

## v0.1.15 [2026/05/03]

### 更新内容

1. 新增IFeignResponseResolver接口和FeignResponseAttribute，允许为Feign客户端接口或方法指定自定义响应解析逻辑

### 示例

#### FeignResponseAttribute

```csharp
[FeignResponse(typeof(ResultFeignResponseResolver))]
[Get("/api/test/users/{id}/getbyid3", RawFormat = false)]
UserDto GetByIdSync4([PathVariable("id")] long id);
```

#### IFeignResponseResolver

```csharp
using System.Text.Json;
using Yzl.Extensions.Http.OpenFeign.Execution;
using Yzl.Extensions.Http.OpenFeign.Execution.ResponseResolver;
using Yzl.Extensions.Http.OpenFeign.Serializer;

namespace Test.Yzl.Extensions.Http.OpenFeign.FeignImpl;

/// <summary>
/// {"status": 200, "result": "xxx" , "message": "xxx"}
/// </summary>
public sealed class ResultFeignResponseResolver : IFeignResponseResolver
{
    public int Order => 1;

    public bool IsGlobal => false;

    public object? Resolve(
        JsonElement root,
        Type resultType,
        FeignRequestContext context,
        IFeignSerializer serializer)
    {
        if (!root.TryGetProperty("status", out var statusElement) ||
            !statusElement.TryGetInt32(out var status) ||
            status != 200)
        {
            return null;
        }

        if (!root.TryGetProperty("result", out var resultElement))
            return null;

        if (resultElement.ValueKind == JsonValueKind.Null)
            return null;

        if (resultType == typeof(string))
        {
            return resultElement.ValueKind == JsonValueKind.String
                ? resultElement.GetString()
                : resultElement.GetRawText();
        }

        if (resultType == typeof(object))
            return resultElement.Clone();

        return serializer.Deserialize(resultElement, resultType);
    }
}
```

---

## v0.1.14

### 更新内容

1. 修复序列化问题

---

## v0.1.13 [2026/04/20]

### 更新内容

1. 添加服务器发送事件(SSE)支持

### 示例

```csharp
[FeignClient(name: "test", url: "http://localhost:17007", fallback: typeof(TestApiFeignClientFallback), timeout: 5000)]
public interface ITestApiFeignClient
{
    [Get("/api/test/timeout", timeout: 7000)]
    string TimeOut([RequestHeader] string a = "123", [RequestHeader(name: "user-token", Encoded = true)] string utk = "你好");

    [Sse(CompleteField = "completeSucc")]
    [Get("/api/RandomChinese/stream")]
    IAsyncEnumerable<RandomChineseSseDto> RandomChinese();
    
    [Sse(CompleteField = "completeSucc")]
    [Get("/api/RandomChinese/stream")]
    ISseStream<RandomChineseSseDto> RandomChinese2();
}

public class TestController(ITestApiFeignClient client) : Controller
{

    [HttpGet("stream-proxy")]
    public async Task StreamProxy(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");

        await foreach (var item in client.RandomChinese().WithCancellation(cancellationToken))
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(item);

            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpGet("stream-proxy1")]
    public async Task StreamProxy1(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");

        var options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        await foreach (var item in client.RandomChinese2().WithCancellation(cancellationToken))
        {
            var json = System.Text.Json.JsonSerializer.Serialize(item, options);

            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
```

### 其他

2. 重构FeignClient拦截器，简化创建代理对象参数

---

## v0.1.12 [2026/04/19]

### 更新内容

1. 支持为单个接口方法配置超时时间。例如：`[Get("/api/test/timeout",timeout:7000)]`

---

## v0.1.11 [2026/04/16]

### 更新内容

1. 为OpenFeign添加详细的调试日志记录功能。当项目日志级别设置为Debug时，会打印出完整的HTTP请求头、请求体和响应内容，便于开发和问题排查。

### 示例

请求接口调试日志如下：

```text
OpenFeignResponse
method: GetTagQuota
uri: http://xxx
httpStatus: OK
===== Request Headers =====
trace-id: cd1e6b1023d6a850886c5e065319aed2
User-Agent: Net.OpenFeign/1.0
traceparent: 00-cd1e6b1023d6a850886c5e065319aed2-5c982b089e1d60c4-00
===== Request Body =====
(null)
===== Response =====
{"code":"0","data":{"canCreate":true,"current":0,"limit":5,"remaining":5},"msg":"成功"}
```

---

## v0.1.10 [2026/04/05]

### 更新内容

1. FeignOptions 增加SerializerType配置，支持用户自定义序列化器，默认使用NewtonsoftJson

### 示例

```csharp
builder.Services.AddFeignStarter(builder.Configuration, options => { 
    options.SerializerType = typeof(SystemTextJsonFeignSerializer); 
});
```

---

## v0.1.9

.....