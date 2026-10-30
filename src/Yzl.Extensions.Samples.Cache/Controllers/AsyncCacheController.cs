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
[Route("api/samples/async")]
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
    [HttpGet("{id}")]
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
    [HttpPost("update")]
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
    [HttpGet("all")]
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
    /// 【6.4】异步 + 短 TTL + Unless 对照组
    ///
    /// 与 6.1 的区别只有一处：<c>GetHotUserAsync</c> 带 <c>unless = "#result == null"</c>。
    /// v0.2.0 起框架默认把 null 也写进缓存（对齐 Spring/Java），
    /// 所以这是<b>逐方法</b>退回旧行为的唯一写法 —— 不存在的 id 每次都会真实执行。
    /// 对照 <c>GET async/{id}</c>（无 unless）：同一个不存在的 id 第二次即命中。
    /// </summary>
    [Description("【6.4】异步 unless 对照（null 不缓存）")]
    [HttpGet("hot/{id}")]
    public async Task<IActionResult> AsyncHot(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = await _async.GetHotUserAsync(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _async.CallCount,
            operation = "异步 Cacheable + Unless",
            unless = "#result == null",
            note = "带 unless 时返回 null 不写缓存；去掉 unless 后同样的 id 第二次就会命中"
        });
    }

    /// <summary>
    /// 查看 AsyncCache 实际方法执行次数
    /// </summary>
    [Description("📊 GetUserAsync 执行次数")]
    [HttpGet("call-count")]
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
