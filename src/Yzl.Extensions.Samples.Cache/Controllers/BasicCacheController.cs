using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Samples.Cache.Models;
using Yzl.Extensions.Samples.Cache.Services;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Cache.Controllers;

/// <summary>
/// 第一章：基础 Cacheable 用法
/// </summary>
[ApiController]
[TestDashboardInfo("📖 第一章：基础 Cacheable 用法", Order = 1, Badge = "1")]
[Route("api/samples")]
public class BasicCacheController : ControllerBase
{
    private readonly BasicCacheService _basicCache;

    public BasicCacheController(BasicCacheService basicCache)
    {
        _basicCache = basicCache;
    }

    /// <summary>
    /// 【1.1】基础缓存 — 按 ID 查询用户
    /// </summary>
    [Description("【1.1】基础缓存 — 按 ID 查询用户，TTL=60s")]
    [HttpGet("basic/{id}")]
    public IActionResult GetUser(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _basicCache.GetUser(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _basicCache.GetUserCallCount,
            message = user != null
                ? $"第 {_basicCache.GetUserCallCount} 次实际调用（非缓存命中）。耗时：{sw.ElapsedMilliseconds}ms"
                : "用户不存在",
            cacheKey = $"users:{id}",
            ttl = "60秒（固定TTL）"
        });
    }

    /// <summary>
    /// 【1.2】短 TTL 缓存（10 秒过期）
    /// </summary>
    [Description("【1.2】短 TTL 缓存（10 秒过期）")]
    [HttpGet("basic/short-ttl/{id}")]
    public IActionResult GetUserShortTtl(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _basicCache.GetUserShortTtl(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _basicCache.GetUserCallCount,
            ttl = "10秒",
            note = "10秒内反复请求会命中缓存；10秒后缓存过期，再次执行方法体"
        });
    }

    /// <summary>
    /// 【1.3】按名称查询用户（字符串缓存键）
    /// </summary>
    [Description("【1.3】按名称查询用户（字符串缓存键）")]
    [HttpGet("basic/by-name")]
    public IActionResult GetUserByName([FromQuery] string name = "Alice")
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _basicCache.GetUserByName(name);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _basicCache.GetUserCallCount,
            cacheKey = $"users:name:{name}",
            note = "字符串作为缓存键，不区分大小写"
        });
    }

    /// <summary>
    /// 【1.4】按年龄范围查询（组合键）
    /// </summary>
    [Description("【1.4】按年龄范围查询（组合键）")]
    [HttpGet("basic/age-range")]
    public IActionResult GetUsersByAgeRange([FromQuery] int minAge, [FromQuery] int maxAge)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var users = _basicCache.GetUsersByAgeRange(minAge, maxAge);
        sw.Stop();
        return Ok(new
        {
            data = users,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _basicCache.GetUserCallCount,
            cacheKey = $"users:age-range:{minAge}:{maxAge}",
            note = "多参数组合键，不同参数组合对应不同缓存"
        });
    }

    /// <summary>
    /// 【1.5】简写属性兼容性 — ttl 简写语法
    /// </summary>
    [Description("【1.5】简写属性兼容性 — ttl 简写语法")]
    [HttpGet("basic/legacy/{id}")]
    public IActionResult GetUserLegacy(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _basicCache.GetUserLegacy(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _basicCache.GetUserLegacyCallCount,
            cacheKey = $"users:legacy:{id}",
            ttl = "60（ttlSeconds）",
            note = "验证 ttlSeconds 简写属性的兼容性"
        });
    }

    /// <summary>
    /// 【1.6】简写属性兼容性 — slidingTtl 简写语法
    /// </summary>
    [Description("【1.6】简写属性兼容性 — slidingTtl 简写语法")]
    [HttpGet("basic/legacy-sliding/{id}")]
    public IActionResult GetUserLegacySliding(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _basicCache.GetUserLegacySliding(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _basicCache.GetUserLegacyCallCount,
            slidingTtl = "300（slidingTtl）",
            note = "验证 slidingTtl 简写属性的兼容性"
        });
    }
}
