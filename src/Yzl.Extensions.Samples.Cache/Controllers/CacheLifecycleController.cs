using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Samples.Cache.Models;
using Yzl.Extensions.Samples.Cache.Services;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Cache.Controllers;

/// <summary>
/// 第二章：CachePut 和 CacheEvict（缓存生命周期）
/// </summary>
[ApiController]
[TestDashboardInfo("📖 第二章：CachePut & CacheEvict", Order = 2, Badge = "2")]
[Route("api/samples")]
public class CacheLifecycleController : ControllerBase
{
    private readonly CacheLifecycleService _lifecycle;

    public CacheLifecycleController(CacheLifecycleService lifecycle)
    {
        _lifecycle = lifecycle;
    }

    /// <summary>
    /// 【2.1】查询用户（Cacheable）
    /// </summary>
    [Description("【2.1】查询用户（Cacheable）")]
    [HttpGet("lifecycle/{id}")]
    public IActionResult LifecycleGet(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _lifecycle.GetUser(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _lifecycle.CallCount,
            operation = "Cacheable（查询）"
        });
    }

    /// <summary>
    /// 【2.2】更新用户（CachePut）— 方法始终执行，结果写入缓存
    /// </summary>
    [Description("【2.2】更新用户（CachePut）")]
    [HttpPost("lifecycle/update")]
    public IActionResult LifecycleUpdate([FromForm] int id, [FromForm] string? name = null,
        [FromForm] int age = 0, [FromForm] string? email = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _lifecycle.UpdateUser(new UserDto
        {
            Id = id, Name = name ?? "", Age = age, Email = email ?? ""
        });
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _lifecycle.CallCount,
            operation = "CachePut（更新并写入缓存）",
            note = "CachePut 始终执行方法体，并将结果写入缓存"
        });
    }

    /// <summary>
    /// 【2.3】删除用户（CacheEvict）
    /// </summary>
    [Description("【2.3】删除用户（CacheEvict）")]
    [HttpPost("lifecycle/delete")]
    public IActionResult LifecycleDelete([FromForm] int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _lifecycle.DeleteUser(id);
        sw.Stop();
        return Ok(new
        {
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _lifecycle.CallCount,
            operation = "CacheEvict（删除并清除缓存）",
            clearedCacheKey = $"lifecycle:{id}",
            note = "从数据源删除数据，同时驱逐缓存中的对应条目"
        });
    }

    /// <summary>
    /// 【2.4】刷新缓存（CachePut + 重新加载）
    /// </summary>
    [Description("【2.4】刷新缓存（CachePut + 重新加载）")]
    [HttpGet("lifecycle/refresh/{id}")]
    public IActionResult LifecycleRefresh(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _lifecycle.RefreshUser(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _lifecycle.CallCount,
            operation = "CachePut（强制刷新缓存）",
            note = "从数据库重新加载数据并更新缓存"
        });
    }
}
