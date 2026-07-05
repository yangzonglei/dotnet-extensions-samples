using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Samples.Cache.Models;
using Yzl.Extensions.Samples.Cache.Services;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Cache.Controllers;

/// <summary>
/// 第六章：异步缓存
/// </summary>
[ApiController]
[TestDashboardInfo("📖 第六章：异步缓存", Order = 6, Badge = "6")]
[Route("api/samples")]
public class AsyncCacheController : ControllerBase
{
    private readonly AsyncCacheService _async;

    public AsyncCacheController(AsyncCacheService async)
    {
        _async = async;
    }

    /// <summary>
    /// 【6.1】异步查询用户
    /// </summary>
    [Description("【6.1】异步查询用户")]
    [HttpGet("async/{id}")]
    public async Task<IActionResult> AsyncGet(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = await _async.GetUserAsync(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _async.CallCount,
            operation = "异步 Cacheable",
            note = "异步方法同样支持缓存，使用方式与同步方法完全一致"
        });
    }

    /// <summary>
    /// 【6.2】异步更新用户
    /// </summary>
    [Description("【6.2】异步更新用户（CachePut）")]
    [HttpPost("async/update")]
    public async Task<IActionResult> AsyncUpdate([FromForm] int id, [FromForm] string? name = null,
        [FromForm] int age = 0, [FromForm] string? email = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = await _async.UpdateUserAsync(new UserDto
        {
            Id = id, Name = name ?? "", Age = age, Email = email ?? ""
        });
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _async.CallCount,
            operation = "异步 CachePut"
        });
    }

    /// <summary>
    /// 【6.3】异步获取全部用户
    /// </summary>
    [Description("【6.3】异步获取全部用户（缓存集合）")]
    [HttpGet("async/all")]
    public async Task<IActionResult> AsyncGetAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var users = await _async.GetAllUsersAsync();
        sw.Stop();
        return Ok(new
        {
            data = users,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _async.CallCount,
            operation = "异步批量查询（缓存集合）",
            note = "返回 List<T> 的异步方法同样支持缓存"
        });
    }

    /// <summary>
    /// 查看 AsyncCache 实际方法执行次数
    /// </summary>
    [Description("📊 GetUserAsync 执行次数")]
    [HttpGet("async/call-count")]
    public IActionResult AsyncCallCount()
    {
        return Ok(new
        {
            method = "GetUserAsync",
            executedTimes = _async.CallCount,
            note = "刷新 async/{id} 端点后查看此端点，数值不变则证明缓存生效"
        });
    }
}
