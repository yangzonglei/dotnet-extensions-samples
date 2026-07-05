using System.Text;
using NLog.Web;
using Yzl.Extensions.Samples.OpenFeign.AOT.Acs;
using Yzl.Extensions.Samples.OpenFeign.AOT.FeignImpl;

using Samples.Models;
using Yzl.Extensions.Http.OpenFeign;

Console.WriteLine("""
                  ╔═══════════════════════════════════════════════════════╗
                  ║     Yzl.Extensions.Http.OpenFeign AOT 测试          ║
                  ║                                                      ║
                  ║     访问: http://localhost:16603                      ║
                  ║                                                      ║
                  ║     AOT 兼容模式下 OpenFeign 客户端演示             ║
                  ║     CRUD / HTTP 方法 / 请求体 / SSE / 下载          ║
                  ║                                                      ║
                  ║     前置依赖: Samples.Api (端口 16600)               ║
                  ╚═══════════════════════════════════════════════════════╝
                  """);

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Host.UseNLog();

builder.WebHost.UseUrls("http://localhost:16603");
builder.Services.AddEndpointsApiExplorer();
builder.Services
    .AddOpenFeignAot(builder.Configuration)
    .AddFeignResponseResolver<SampleResponseResolver>()
    .AddFeignRequestHeaderProvider<DemoHeaderProvider>()
    .AddGeneratedFeignClients();

var app = builder.Build();

app.MapGet("/", () => new
{
    message = "Yzl.Extensions.Http.OpenFeign AOT Samples Client",
    endpoints = new[]
    {
        "/demo/basic",
        "/demo/methods",
        "/demo/body",
        "/demo/advanced",
        "/demo/sse",
        "/read-all",
        "/download-async",
        "/download-sync",
        "/download-bytes"
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
});

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
});

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
});

app.MapGet("/demo/advanced", async (IDemoFeignClient client) => new
{
    headers = await client.Headers("token-from-parameter"),
    rawFormatFalse = await client.GetWrappedData(6),
    rawFormatTrue = await client.GetWrappedRaw(7),
    customResolver = await client.GetStatusResult(8),
    timeoutFallback = client.TimeoutWithFallback()
});

app.MapGet("/demo/sse", async (ISseDemoFeignClient client, CancellationToken cancellationToken) =>
{
    var events = new List<SseEventDto>();

    await foreach (var item in client.StreamAsAsyncEnumerable().WithCancellation(cancellationToken))
    {
        events.Add(item);
    }

    var streamEvents = new List<SseEventDto>();
    var stream = client.StreamAsSseStream();
    await stream.SubscribeAsync(item =>
    {
        streamEvents.Add(item);
        return Task.CompletedTask;
    }, cancellationToken);

    return new
    {
        asyncEnumerable = events,
        sseStream = streamEvents,
        stream.IsClosed
    };
});

app.MapGet("/download-async", async (IDownloadClient downloadClient, CancellationToken cancellationToken) =>
{
    await using var stream = await downloadClient.DownloadAsync();
    using var reader = new StreamReader(stream);
    var content = await reader.ReadToEndAsync(cancellationToken);

    return content;
});

app.MapGet("/download-sync", (IDownloadClient downloadClient) =>
{
    using var stream = downloadClient.Download();
    using var reader = new StreamReader(stream);
    var content = reader.ReadToEnd();

    return content;
});

app.MapGet("/download-bytes", async (IDownloadClient downloadClient) =>
{
    var bytes = await downloadClient.DownloadBytesAsync();
    var content = Encoding.UTF8.GetString(bytes);

    return content;
});

app.MapGet("/read-all", async (IDemoFeignClient demoClient, IRequestBodyDemoFeignClient bodyClient, ISseDemoFeignClient sseClient, IDownloadClient downloadClient, CancellationToken cancellationToken) =>
{
    await using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes("stream body demo"));
    await using var downloadStream = await downloadClient.DownloadAsync();
    using var downloadReader = new StreamReader(downloadStream);

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
            streamBody = await bodyClient.SendStream(bodyStream),
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
        },
        download = await downloadReader.ReadToEndAsync(cancellationToken)
    };
});

app.Run();
