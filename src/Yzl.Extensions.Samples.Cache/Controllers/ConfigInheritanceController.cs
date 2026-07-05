using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Samples.Cache.Models;
using Yzl.Extensions.Samples.Cache.Services;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Cache.Controllers;

/// <summary>
/// 第五章：CacheConfig 继承
/// </summary>
[ApiController]
[TestDashboardInfo("📖 第五章：CacheConfig 继承", Order = 5, Badge = "5")]
[Route("api/samples")]
public class ConfigInheritanceController : ControllerBase
{
    private readonly ConfigInheritanceService _config;

    public ConfigInheritanceController(ConfigInheritanceService config)
    {
        _config = config;
    }

    /// <summary>
    /// 【5.1】完全继承 CacheConfig
    /// </summary>
    [Description("【5.1】完全继承 CacheConfig")]
    [HttpGet("config/default/{id}")]
    public IActionResult ConfigDefault(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _config.GetDefault(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _config.CallCount,
            inheritedCacheName = "config-demo（从 [CacheConfig] 继承）",
            inheritedTtl = "120秒（从 [CacheConfig] 继承）"
        });
    }

    /// <summary>
    /// 【5.2】部分覆盖 — 自定义 cacheName
    /// </summary>
    [Description("【5.2】部分覆盖 — 自定义 cacheName")]
    [HttpGet("config/custom-name/{id}")]
    public IActionResult ConfigCustomName(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _config.GetWithCustomCacheName(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _config.CallCount,
            cacheName = "custom-name（覆盖）",
            ttl = "120秒（继承）"
        });
    }

    /// <summary>
    /// 【5.3】部分覆盖 — 自定义 TTL
    /// </summary>
    [Description("【5.3】部分覆盖 — 自定义 TTL")]
    [HttpGet("config/custom-ttl/{id}")]
    public IActionResult ConfigCustomTtl(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _config.GetWithCustomTtl(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _config.CallCount,
            cacheName = "config-demo（继承）",
            ttl = "30秒（覆盖默认值 120秒）"
        });
    }

    /// <summary>
    /// 【5.4】完全覆盖
    /// </summary>
    [Description("【5.4】完全覆盖所有配置")]
    [HttpGet("config/fully-custom/{id}")]
    public IActionResult ConfigFullyCustom(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _config.GetFullyCustom(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _config.CallCount,
            cacheName = "fully-custom",
            ttl = "600秒",
            slidingTtl = "60秒",
            note = "方法级别配置完全覆盖了 [CacheConfig] 默认值"
        });
    }
}
