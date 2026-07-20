using System.Collections.Concurrent;
using System.Diagnostics;
using Yzl.Extensions.Http.OpenFeign;
using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;
using Yzl.Extensions.Http.OpenFeign.Internal;
using Yzl.Extensions.Http.OpenFeign.Serializer;

namespace Yzl.Extensions.Samples.OpenFeign.Benchmark;

[FeignClient(name: "benchmark-api", url: "http://127.0.0.1:17999")]
public interface IBenchmarkFeignClient
{
    [Get("/api/ping")]
    string Ping();

    [Get("/api/timeout")]
    string Timeout();
}

public class PerfStats
{
    public ConcurrentBag<long> LatencyMs = new();
    public long Total, Success, Fail;
    public void Ok(long ms) { Interlocked.Increment(ref Total); Interlocked.Increment(ref Success); LatencyMs.Add(ms); }
    public void RecordFail(long ms) { Interlocked.Increment(ref Total); Interlocked.Increment(ref Fail); LatencyMs.Add(ms); }
    public void Print(string label)
    {
        var s = LatencyMs.OrderBy(x => x).ToList();
        var p = (int pct) => s.Any() ? s[(int)(s.Count * pct / 100.0)] : 0;
        Console.WriteLine($"\n## {label}");
        Console.WriteLine($"  请求={Total}  成功={Success}  失败={Fail} ({(Total > 0 ? (Fail * 100.0 / Total).ToString("F1") : "0")}%)");
        Console.WriteLine($"  延迟(ms): P50={p(50)}  P95={p(95)}  P99={p(99)}  avg={(s.Any() ? s.Average() : 0):F0}  max={(s.Any() ? s.Max() : 0)}");
    }
}

public static class Program
{
    public static async Task Main(string[] args)
    {
#if USING_LOCAL
        var version = "修改版(本地源码)";
#else
        var version = "NuGet";
#endif
        Console.WriteLine($"===== OpenFeign 性能对比: {version} =====");

        // 启动内嵌 API
        var app = StartApi();
        await WaitForApi();

        // 构建 Feign 客户端
        var client = BuildClient();

        // 预热
        for (int i = 0; i < 5; i++) { client.Ping(); }

        // 场景 1: 突发 Ping
        await RunBurst("突发 Ping × 1000, 并发 500", client, 1000, 500);
        // 场景 2: 高频 Ping
        await RunBurst("高频 Ping × 2000, 并发 300", client, 2000, 300);
        // 场景 3: 超时
        await RunTimeout("超时 Timeout × 30, 并发 10", client, 30, 10);

        Console.WriteLine($"\n===== {version} 测试完成 =====\n");
    }

    private static async Task RunBurst(string name, IBenchmarkFeignClient c, int n, int concurrency)
    {
        var sw = Stopwatch.StartNew();
        var stats = new PerfStats();
        await Parallel.ForAsync(0, n, new ParallelOptions { MaxDegreeOfParallelism = concurrency },
            async (i, ct) =>
            {
                var start = Stopwatch.GetTimestamp();
                try { c.Ping(); stats.Ok((long)Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
                catch (Exception ex) { stats.RecordFail((long)Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
                await Task.CompletedTask;
            });
        sw.Stop();
        stats.Print($"{name}  [{n / sw.Elapsed.TotalSeconds:F0} req/s]");
    }

    private static async Task RunTimeout(string name, IBenchmarkFeignClient c, int n, int concurrency)
    {
        var sw = Stopwatch.StartNew();
        var stats = new PerfStats();
        await Parallel.ForAsync(0, n, new ParallelOptions { MaxDegreeOfParallelism = concurrency },
            async (i, ct) =>
            {
                var start = Stopwatch.GetTimestamp();
                try { c.Timeout(); stats.Ok((long)Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
                catch (Exception ex) { stats.RecordFail((long)Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
                await Task.CompletedTask;
            });
        sw.Stop();
        stats.Print($"{name}  [{n / sw.Elapsed.TotalSeconds:F0} req/s]");
    }

    private static WebApplication StartApi()
    {
        var b = WebApplication.CreateBuilder();
        b.WebHost.UseUrls("http://127.0.0.1:17999");
        b.Logging.ClearProviders();
        var app = b.Build();
        app.MapGet("/api/ping", () => "pong");
        app.MapGet("/api/timeout", () => { Thread.Sleep(6000); return "done"; });
        app.Start();
        return app;
    }

    private static async Task WaitForApi()
    {
        using var hc = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (int i = 0; i < 20; i++)
        {
            try { var r = await hc.GetAsync("http://127.0.0.1:17999/api/ping"); if (r.IsSuccessStatusCode) return; } catch { }
            await Task.Delay(200);
        }
    }

    private static IBenchmarkFeignClient BuildClient()
    {
        var s = new ServiceCollection();
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["spring:feign:default:timeout"] = "3000",
                ["spring:feign:default:retry:enabled"] = "true",
                ["spring:feign:default:retry:maxAttempts"] = "2",
                ["spring:feign:default:retry:delayMs"] = "500",
                ["spring:feign:default:retry:maxDelay"] = "2000",
                ["spring:feign:default:syncTimeoutMs"] = "15000",
                ["spring:feign:httpClient:pool:maxConnectionsPerServer"] = "300",
                ["spring:feign:httpClient:handlerLifetime"] = "00:30:00",
            })
            .Build();
        s.AddLogging(b => b.ClearProviders());
        s.AddSingleton<IConfiguration>(cfg);
        s.AddFeignStarter(cfg, o => o.SerializerType = typeof(SystemTextJsonFeignSerializer));
        s.AddFeignClient<IBenchmarkFeignClient>();
        return s.BuildServiceProvider().GetRequiredService<IBenchmarkFeignClient>();
    }
}
