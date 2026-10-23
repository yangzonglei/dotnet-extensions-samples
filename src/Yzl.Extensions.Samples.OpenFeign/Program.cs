using System.Text;
using NLog.Web;
using Yzl.Extensions.Http.OpenFeign;
using Yzl.Extensions.Http.OpenFeign.Serializer;
using Yzl.Extensions.Samples.OpenFeign.Acs;
using Yzl.Extensions.Samples.OpenFeign.Fallbacks;
using Samples.Models;
using Yzl.Extensions.Samples.TestDashboard;

Console.WriteLine("""
                  ╔═══════════════════════════════════════════════════════╗
                  ║       Yzl.Extensions.Http.OpenFeign 测试            ║
                  ║                                                      ║
                  ║     访问: http://localhost:16602                      ║
                  ║                                                      ║
                  ║     OpenFeign 动态代理客户端演示                     ║
                  ║     CRUD / HTTP 方法 / 请求体 / SSE / 下载          ║
                  ║                                                      ║
                  ║     前置依赖: Samples.Api (端口 16600)               ║
                  ╚═══════════════════════════════════════════════════════╝
                  """);

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.WebHost.UseUrls("http://localhost:16602");
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddFeignStarter(builder.Configuration, options => { options.SerializerType = typeof(SystemTextJsonFeignSerializer); });

var app = builder.Build();

app.MapControllers();

app.MapGet("/", () => new
{
    message = "Yzl.Extensions.Http.OpenFeign 测试",
    description = "OpenFeign 动态代理客户端演示 — CRUD / HTTP 方法 / 请求体 / SSE / 下载",
    dashboard = "/dashboard",
    dependsOn = "Samples.Api (端口 16600)"
});

app.MapTestDashboard(new TestDashboardOptions
{
    Groups = new()
    {
        ["basic"] = ("📦 CRUD 基础操作", "/demo/basic"),
        ["methods"] = ("🔧 HTTP 方法演示", "/demo/methods"),
        ["body"] = ("📝 请求体类型演示", "/demo/body"),
        ["advanced"] = ("⚡ 高级特性演示", "/demo/advanced"),
        ["fallback"] = ("🛟 降级演示 (fallback / fallbackFactory)", "/demo/fallback/*"),
        ["sse"] = ("📡 SSE 流演示", "/demo/sse"),
        ["download"] = ("⬇️ 文件下载演示", "/download-*"),
        ["feign-api-test"] = ("🔬 扩展测试 (ITestApiFeignClient)", "/feign-api-test"),
        ["readall"] = ("🎮 组合测试 (read-all)", "/read-all"),
        // "Test" 由 OpenFeign.Controllers.TestController 的 [TestDashboardInfo] 自动发现
    }
});

app.MapGet("/demo/basic", async (IDemoFeignClient client) => new
{
    get = await client.GetById(1),
    getSync = client.GetByIdSync(2),
    query = await client.Query(3, "Tom"),
    queryMap = await client.QueryMap(new Dictionary<string, string>
    {
        ["age"] = "18",
        ["city"] = "Shanghai"
    }),
    create = await client.Create(new CreateUserRequest("Jerry", 20, "Hangzhou")),
    update = await client.Update(4, new UpdateUserRequest("Bob", 30, "Shenzhen")),
    delete = await client.Delete(4)
})
.WithTest("basic", "CRUD 全流程", order: 1);

app.MapGet("/demo/methods", async (IDemoFeignClient client) =>
{
    await client.Head();

    return new
    {
        patch = await client.Patch(5, new Dictionary<string, object?>
        {
            ["name"] = "Patch User",
            ["city"] = "Beijing"
        }),
        head = "completed",
        options = await client.Options(),
        trace = await client.Trace()
    };
})
.WithTest("methods", "HEAD/OPTIONS/TRACE/PATCH", order: 2);

app.MapGet("/demo/body", async (IRequestBodyDemoFeignClient client) =>
{
    await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("stream body demo"));

    return new
    {
        objectBody = await client.SendObject(new CreateUserRequest("Body User", 28, "Guangzhou")),
        stringBody = await client.SendString("plain string body"),
        bytesBody = await client.SendBytes(Encoding.UTF8.GetBytes("byte array body")),
        streamBody = await client.SendStream(stream),
        httpContentBody = await client.SendHttpContent(IRequestBodyDemoFeignClient.CreateJsonContent("{\"name\":\"http-content\"}"))
    };
})
.WithTest("body", "5 种请求体类型", order: 3);

app.MapGet("/demo/advanced", async (IDemoFeignClient client) => new
{
    headers = await client.Headers("token-from-parameter"),
    rawFormatFalse = await client.GetWrappedData(6),
    rawFormatTrue = await client.GetWrappedRaw(7),
    customResolver = await client.GetStatusResult(8),
    timeoutFallback = client.TimeoutWithFallback()
})
.WithTest("advanced", "Headers / RawFormat / 自定义解析器 / 超时", order: 4);

// 【12.1】Task 返回值降级（fallback）
//
// 上游 /api/timeout 会睡 3 秒，本客户端超时 2 秒 —— 降级必然触发，
// 因而不依赖「上游挂掉」这种前置条件，比 Cache 样例更容易演示。
//
// 修复前：Task 返回值失败时异常会逃出拦截器的 try/catch，
//         调用方拿到的是 FeignClientException，而不是降级值。
app.MapGet("/demo/fallback/task", async (IDemoFeignClient client) =>
{
    var asyncResult = await client.TimeoutWithFallbackAsync();
    var syncResult = client.TimeoutWithFallback();

    return new
    {
        syncResult,
        asyncResult,
        syncDegraded = syncResult == "fallback timeout",
        asyncDegraded = asyncResult == "fallback timeout (async)",
        operation = "OpenFeign 超时降级（fallback）",
        chain = "Program.cs → IDemoFeignClient(fallback) → Samples.Api(/api/timeout, 睡 3s / 客户端超时 2s)",
        note = "同步与异步两条路径都应由 DemoFeignClientFallback 兜底；"
             + "若 asyncResult 抛 FeignClientException，说明异步降级未生效"
    };
})
.WithTest("fallback", "Task 返回值降级（fallback）", order: 9);

// 【12.2】降级工厂（fallbackFactory，带失败原因）
//
// 另一个 Feign 客户端 IDemoFeignClientFactory，声明的是 fallbackFactory 而不是 fallback。
// 两者互斥，所以必须单独开接口。
app.MapGet("/demo/fallback/factory", async (IDemoFeignClientFactory client) =>
{
    var asyncResult = await client.TimeoutWithFallbackAsync();
    var syncResult = client.TimeoutWithFallback();

    return new
    {
        syncResult,
        asyncResult,
        degraded = syncResult == "factory fallback" && asyncResult == "factory fallback (async)",
        // 工厂在 Create(cause) 里拿到了失败原因，静态留存给本端点展示
        degradeReason = DemoFeignClientFallbackFactory.LastCause,
        operation = "OpenFeign 超时降级（fallbackFactory）",
        chain = "Program.cs → IDemoFeignClientFactory(fallbackFactory) → Samples.Api(/api/timeout, 睡 3s / 客户端超时 2s)",
        note = "同步与异步都会走工厂；degradeReason 是 Create(cause) 收到异常链的最内层"
    };
})
.WithTest("fallback", "降级工厂（fallbackFactory）", order: 10);

app.MapGet("/demo/sse", async (ISseDemoFeignClient client, HttpContext context, CancellationToken cancellationToken) =>
{
    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    var jsonOptions = new System.Text.Json.JsonSerializerOptions
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // Phase 1: IAsyncEnumerable 流式输出
    await context.Response.WriteAsync("event: phase\ndata: {\"phase\":\"asyncEnumerable\"}\n\n", cancellationToken);
    await context.Response.Body.FlushAsync(cancellationToken);

    await foreach (var item in client.StreamAsAsyncEnumerable().WithCancellation(cancellationToken))
    {
        var json = System.Text.Json.JsonSerializer.Serialize(item, jsonOptions);
        await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
    }

    // Phase 2: ISseStream 流式输出
    await context.Response.WriteAsync("event: phase\ndata: {\"phase\":\"sseStream\"}\n\n", cancellationToken);
    await context.Response.Body.FlushAsync(cancellationToken);

    var sseStream = client.StreamAsSseStream();
    await sseStream.SubscribeAsync(async item =>
    {
        var json = System.Text.Json.JsonSerializer.Serialize(item, jsonOptions);
        await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
    }, cancellationToken);

    // 完成信号
    await context.Response.WriteAsync("event: done\ndata: {\"done\":true,\"isClosed\":true}\n\n", cancellationToken);
    await context.Response.Body.FlushAsync(cancellationToken);
})
.WithTest("sse", "SSE 流 (IAsyncEnumerable + ISseStream)", isSse: true, order: 5);

app.MapGet("download-async", async (IDownloadClient downloadClient, CancellationToken cancellationToken) =>
{
    await using var stream = await downloadClient.DownloadAsync();
    using var reader = new StreamReader(stream);
    var content = await reader.ReadToEndAsync(cancellationToken);

    return content;
})
.WithTest("download", "异步文件下载", order: 6);

app.MapGet("download-sync", (IDownloadClient downloadClient, CancellationToken cancellationToken) =>
{
    using var stream = downloadClient.Download();
    using var reader = new StreamReader(stream);
    var content = reader.ReadToEnd();

    return content;
})
.WithTest("download", "同步文件下载", order: 6);

app.MapGet("download-bytes", async (IDownloadClient downloadClient, CancellationToken cancellationToken) =>
{
    var bytes = await downloadClient.DownloadBytesAsync();
    var content = System.Text.Encoding.UTF8.GetString(bytes);

    return content;
})
.WithTest("download", "字节数组下载", order: 6);

app.MapGet("/read-all", async (IDemoFeignClient demoClient, IRequestBodyDemoFeignClient bodyClient, ISseDemoFeignClient sseClient, CancellationToken cancellationToken) =>
{
    await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("stream body demo"));

    var events = new List<SseEventDto>();
    await foreach (var item in sseClient.StreamAsAsyncEnumerable().WithCancellation(cancellationToken))
    {
        events.Add(item);
    }

    var streamEvents = new List<SseEventDto>();
    var sseStream = sseClient.StreamAsSseStream();
    await sseStream.SubscribeAsync(item =>
    {
        streamEvents.Add(item);
        return Task.CompletedTask;
    }, cancellationToken);

    await demoClient.Head();

    return new
    {
        basic = new
        {
            get = await demoClient.GetById(1),
            getSync = demoClient.GetByIdSync(2),
            query = await demoClient.Query(3, "Tom"),
            queryMap = await demoClient.QueryMap(new Dictionary<string, string>
            {
                ["age"] = "18",
                ["city"] = "Shanghai"
            }),
            create = await demoClient.Create(new CreateUserRequest("Jerry", 20, "Hangzhou")),
            update = await demoClient.Update(4, new UpdateUserRequest("Bob", 30, "Shenzhen")),
            delete = await demoClient.Delete(4)
        },
        methods = new
        {
            patch = await demoClient.Patch(5, new Dictionary<string, object?>
            {
                ["name"] = "Patch User",
                ["city"] = "Beijing"
            }),
            head = "completed",
            options = await demoClient.Options(),
            trace = await demoClient.Trace()
        },
        body = new
        {
            objectBody = await bodyClient.SendObject(new CreateUserRequest("Body User", 28, "Guangzhou")),
            stringBody = await bodyClient.SendString("plain string body"),
            bytesBody = await bodyClient.SendBytes(Encoding.UTF8.GetBytes("byte array body")),
            streamBody = await bodyClient.SendStream(stream),
            httpContentBody = await bodyClient.SendHttpContent(IRequestBodyDemoFeignClient.CreateJsonContent("{\"name\":\"http-content\"}"))
        },
        advanced = new
        {
            headers = await demoClient.Headers("token-from-parameter"),
            rawFormatFalse = await demoClient.GetWrappedData(6),
            rawFormatTrue = await demoClient.GetWrappedRaw(7),
            customResolver = await demoClient.GetStatusResult(8),
            timeoutFallback = demoClient.TimeoutWithFallback()
        },
        sse = new
        {
            asyncEnumerable = events,
            sseStream = streamEvents,
            sseStream.IsClosed
        }
    };
})
.WithTest("readall", "所有演示一次性执行", order: 8);

app.MapGet("/feign-api-test", async (ITestApiFeignClient client) => new
{
    ping = await client.Ping(),
    user = await client.GetById(1),
    query = await client.Query(1, "Tom"),
    map = await client.QueryMap(new Dictionary<string, string> {{"age", "18"}, {"city", "bj"}}),
    create = await client.Create(new CreateUserRequest("Jerry", 20)),
    update = await client.Update(1, new UpdateUserRequest("Bob", 30)),
    header = await client.Headers("token-123"),
    delete = await client.Delete(1),
    timeout = client.TimeOut(),
    sync = new StreamReader(client.Download()).ReadToEnd(),
    async = await new StreamReader(await client.DownloadAsync()).ReadToEndAsync(),
    bytes = System.Text.Encoding.UTF8.GetString(await client.DownloadBytesAsync())
})
.WithTest("feign-api-test", "ITestApiFeignClient 全功能", order: 7);

app.Run();

