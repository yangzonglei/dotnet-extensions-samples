using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Samples.Cache.Models;
using Yzl.Extensions.Samples.Cache.Services;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Cache.Controllers;

/// <summary>
/// 第八章：Redis 缓存
/// </summary>
[ApiController]
[TestDashboardInfo("📖 第八章：Redis 缓存", Order = 8, Badge = "8")]
[Route("api/samples")]
public class RedisCacheController : ControllerBase
{
    private readonly RedisCacheService _redis;

    public RedisCacheController(RedisCacheService redis)
    {
        _redis = redis;
    }

    /// <summary>
    /// 【8.1】Redis 查询用户
    /// </summary>
    [Description("【8.1】Redis 查询用户")]
    [HttpGet("redis/{id}")]
    public IActionResult RedisGet(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _redis.GetUser(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _redis.CallCount,
            cacheType = "Redis",
            cacheKey = $"redis:users:{id}",
            note = "数据存储在 Redis 中，多实例共享缓存。可通过 redis-cli 查看"
        });
    }

    /// <summary>
    /// 【8.2】Redis + 滑动过期
    /// </summary>
    [Description("【8.2】Redis + 滑动过期")]
    [HttpGet("redis/sliding/{id}")]
    public IActionResult RedisSliding(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _redis.GetUserSliding(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _redis.CallCount,
            cacheType = "Redis + 滑动过期",
            slidingTtl = "300秒",
            absoluteMaxTtl = "86400秒（24小时）"
        });
    }

    /// <summary>
    /// 【8.3】Redis CachePut
    /// </summary>
    [Description("【8.3】Redis CachePut")]
    [HttpPost("redis/update")]
    public IActionResult RedisUpdate([FromForm] int id, [FromForm] string? name = null,
        [FromForm] int age = 0)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _redis.UpdateUser(new UserDto
        {
            Id = id, Name = name ?? "", Age = age, Email = $"{name ?? ""}@redis.com"
        });
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            cacheType = "Redis CachePut",
            note = "更新数据库后同步更新 Redis 缓存，多实例共享最新数据"
        });
    }

    /// <summary>
    /// 【8.4】Memory 缓存（与 Redis 对比）
    /// </summary>
    [Description("【8.4】Memory 缓存（与 Redis 对比）")]
    [HttpGet("redis/memory/{id}")]
    public IActionResult RedisMemory(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _redis.GetUserFromMemory(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _redis.CallCount,
            cacheType = "Memory（进程内缓存）",
            note = "数据存储在应用程序进程内存中，访问速度最快但多实例不共享"
        });
    }
}
