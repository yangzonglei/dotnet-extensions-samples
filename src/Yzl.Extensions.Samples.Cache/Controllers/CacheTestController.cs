using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Core.Filters;
using Yzl.Extensions.Samples.Cache.Services;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Cache.Controllers;

/// <summary>
/// 第十一章：OpenFeign + Cacheable（远程调用结果缓存）
///
/// 这是唯一一条「跨进程」的缓存用例，完整链路：
///
///   CacheTestController（本文件）
///     └─→ FeignCacheService            ← 纯转发，不带缓存注解
///           └─→ ICacheDemoFeignClient  ← [Cacheable] 标在接口方法上（+ OpenFeign 动态代理）
///                 └─→ Samples.Api 的 TestController.GetByIdSlow（睡 10 秒）
///
/// 验证方法：连续刷新同一个 id
///   第 1 次 → elapsedMs ≈ 10000（真实远程调用 + 服务端睡 10 秒）
///   第 2 次 → elapsedMs ≈ 0（缓存命中，HTTP 请求根本没发出去）
///
/// 注意判据只认 elapsedMs：缓存层在 Feign 接口上，命中时
/// FeignCacheService 的方法体仍会执行，serviceCallCount 因此不再等于
/// 「HTTP 请求次数」，不能用来判断命中。
/// </summary>
[ApiController]
[TestDashboardInfo("📖 第十一章：OpenFeign + Cacheable", Order = 11, Badge = "11")]
[Route("api/samples/feign")]
public class CacheTestController : ControllerBase
{
    private readonly FeignCacheService _feign;

    public CacheTestController(FeignCacheService feign)
    {
        _feign = feign;
    }

    /// <summary>
    /// 【11.1】远程查询用户（Cacheable 生效验证）
    ///
    /// 服务端 Samples.Api 会先睡 10 秒再返回，
    /// 因此耗时就是「缓存是否命中」最直白的证据。
    /// </summary>
    [Description("【11.1】远程查询用户（OpenFeign + Cacheable）")]
    [HttpGet("{id}")]
    [HttpRequestLog]
    public async Task<IActionResult> GetViaFeign(long id)
    {
        var callsBefore = _feign.CallCount;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = await _feign.GetUserViaFeignAsync(id);
        sw.Stop();

        var isHit = sw.ElapsedMilliseconds < 1000;

        // 只作链路探针：缓存层在 ICacheDemoFeignClient 接口方法上，
        // 命中时被拦截的是更下层的 HTTP 调用，FeignCacheService 的方法体
        // 照常执行 —— 所以这个差值恒为 1，不能用来判断缓存是否命中。
        var executedThisCall = _feign.CallCount - callsBefore;

        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            serviceCallCount = _feign.CallCount,
            methodExecutedThisCall = executedThisCall,
            // CacheKeyGenerator.GenerateCacheKey = cacheName + ":" + SpEL 结果；
            // AppendSeparator 发现 cacheName 末位不是 ':' 才补一个冒号，故只有一个冒号。
            cacheKey = $"feign:users:{id}",
            cacheHit = isHit,
            operation = "OpenFeign 远程调用 + Cacheable",
            chain = "Controller → FeignCacheService → ICacheDemoFeignClient([Cacheable] 在此) → Samples.Api(/api/test/users/{id}/slow)",
            note = isHit
                ? "缓存命中：未发出 HTTP 请求，直接返回缓存值（elapsedMs ≈ 0）"
                : "缓存未命中：真实远程调用，服务端睡了 10 秒（elapsedMs ≈ 10000）；"
                  + "结果已写入缓存，再刷新一次应立即返回"
        });
    }

    /// <summary>
    /// 【11.2】远程 ping（对照组，不走缓存）
    ///
    /// 瞬时返回，用来先确认 Samples.Api 在运行。
    /// 若此端点失败，说明下游不可达，11.1 的耗时便不能用来判断缓存。
    /// </summary>
    [Description("【11.2】远程 ping（不走缓存，检查下游可用性）")]
    [HttpGet("ping")]
    public async Task<IActionResult> RemotePing()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var pong = await _feign.RemotePingAsync();
        sw.Stop();

        return Ok(new
        {
            remote = pong,
            elapsedMs = sw.ElapsedMilliseconds,
            serviceCallCount = _feign.CallCount,
            operation = "OpenFeign 远程调用（无缓存）",
            note = "对照组：同一客户端上的瞬时接口，每次都是真实远程调用，不进缓存"
        });
    }

    /// <summary>
    /// 【11.3】查看远程方法实际执行次数（诊断）
    ///
    /// ⚠ 本端点只能说明「请求进到了 FeignCacheService 这一层」，
    /// 不能证明缓存命中 —— 缓存层在 ICacheDemoFeignClient 的接口方法上。
    /// 判断命中请用 feign/{id} 的 elapsedMs。
    /// </summary>
    [Description("📊 远程方法实际执行次数")]
    [HttpGet("call-count")]
    public IActionResult FeignCallCount()
    {
        return Ok(new
        {
            method = "GetUserViaFeignAsync",
            executedTimes = _feign.CallCount,
            cacheName = "feign:users",
            note = "本计数是「请求穿到 FeignCacheService 的次数」，命中缓存时也会增长，"
                   + "不能用来判断缓存是否生效；请以 feign/{id} 的 elapsedMs 为准"
        });
    }
}
