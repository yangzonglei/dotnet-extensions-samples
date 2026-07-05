using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Samples.Cache.Services;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Cache.Controllers;

/// <summary>
/// 第七章：滑动过期
/// </summary>
[ApiController]
[TestDashboardInfo("📖 第七章：滑动过期", Order = 7, Badge = "7")]
[Route("api/samples")]
public class SlidingExpirationController : ControllerBase
{
    private readonly SlidingExpirationService _sliding;

    public SlidingExpirationController(SlidingExpirationService sliding)
    {
        _sliding = sliding;
    }

    /// <summary>
    /// 【7.1】固定 TTL — 对照实验
    /// </summary>
    [Description("【7.1】固定 TTL — 对照实验（10秒绝对过期）")]
    [HttpGet("sliding/fixed/{id}")]
    public IActionResult SlidingFixed(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _sliding.GetUserFixed(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _sliding.CallCount,
            ttlMode = "固定 TTL（10秒绝对过期）",
            note = "无论是否被访问，10秒后一定过期"
        });
    }

    /// <summary>
    /// 【7.2】滑动过期 — 每次访问续期 30 秒
    /// </summary>
    [Description("【7.2】滑动过期 — 每次访问续期 30 秒")]
    [HttpGet("sliding/basic/{id}")]
    public IActionResult SlidingBasic(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _sliding.GetUserSliding(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _sliding.CallCount,
            ttlMode = "滑动过期",
            slidingTtl = "30秒（每次访问续期）",
            absoluteMaxTtl = "86400秒（24小时绝对上限）",
            note = "每次访问缓存时，TTL 重置为 30 秒；超过 30 秒未访问则过期"
        });
    }
}
