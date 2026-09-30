using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Core.Filters;
using Yzl.Extensions.Samples.Cache.Models;
using Yzl.Extensions.Samples.Cache.Services;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Cache.Controllers;

/// <summary>
/// 第二章：CachePut 和 CacheEvict（缓存生命周期）
/// </summary>
[ApiController]
[TestDashboardInfo("📖 第二章：CachePut & CacheEvict", Order = 2, Badge = "2")]
[Route("api/samples/lifecycle")]
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
    [HttpGet("{id}")]
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
    [HttpPost("update")]
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
    [HttpPost("delete")]
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
    [HttpGet("refresh/{id}")]
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

    /// <summary>
    /// 【2.5】更新用户（[FromBody] 复杂类型 + CachePut）
    ///
    /// 与 2.2 的 [FromForm] 版本语义完全相同、写入同一个 lifecycle 缓存区域，
    /// 唯一区别是参数来自 JSON 请求体 —— 用于验证复杂类型模型的 JSON 反序列化绑定。
    /// </summary>
    [Description("【2.5】更新用户（[FromBody] 复杂类型 + CachePut）")]
    [HttpPost("update-body")]
    [HttpRequestLog]
    public IActionResult LifecycleUpdateBody([FromBody] UserDto user)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var updated = _lifecycle.UpdateUser(user);
        sw.Stop();
        return Ok(new
        {
            data = updated,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _lifecycle.CallCount,
            binding = "[FromBody] → application/json",
            cacheKey = $"lifecycle:{user.Id}",
            operation = "CachePut（[FromBody] 复杂类型）",
            note = "整个 UserDto 由 JSON 请求体反序列化而来；写入的缓存与 2.1 的查询共用同一区域，"
                 + "随后 GET /api/samples/lifecycle/{id} 应直接命中（elapsedMs ≈ 0）"
        });
    }

    /// <summary>
    /// 【2.6】删除用户（[FromBody] 简单类型 + CacheEvict）
    ///
    /// 与 2.3 语义相同，但 id 来自 JSON 请求体（裸 JSON 值而非对象），
    /// 用于验证简单类型也能走 [FromBody] 绑定。
    /// </summary>
    [Description("【2.6】删除用户（[FromBody] 简单类型 + CacheEvict）")]
    [HttpPost("delete-body")]
    public IActionResult LifecycleDeleteBody([FromBody] int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _lifecycle.DeleteUser(id);
        sw.Stop();
        return Ok(new
        {
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _lifecycle.CallCount,
            binding = "[FromBody] → application/json（请求体是一个裸 JSON 数字，如 1）",
            operation = "CacheEvict（[FromBody] 简单类型）",
            clearedCacheKey = $"lifecycle:{id}",
            note = "从数据源删除数据，同时驱逐缓存中的对应条目"
        });
    }
}
