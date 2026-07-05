using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Samples.Cache.Models;
using Yzl.Extensions.Samples.Cache.Services;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Cache.Controllers;

/// <summary>
/// 第三章：SpEL 表达式
/// </summary>
[ApiController]
[TestDashboardInfo("📖 第三章：SpEL 键表达式", Order = 3, Badge = "3")]
[Route("api/samples")]
public class SpelKeyController : ControllerBase
{
    private readonly SpelKeyService _spelKey;

    public SpelKeyController(SpelKeyService spelKey)
    {
        _spelKey = spelKey;
    }

    /// <summary>
    /// 【3.1】SpEL 嵌套属性访问
    /// </summary>
    [Description("【3.1】SpEL 嵌套属性访问 #qo.UserId:#qo.Keyword")]
    [HttpGet("spel/query")]
    public IActionResult SpelQuery([FromQuery] int userId = 1, [FromQuery] string keyword = "default")
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _spelKey.GetUserByQuery(new QueryQo { UserId = userId, Keyword = keyword });
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _spelKey.CallCount,
            cacheKey = $"spel:query:{userId}:{keyword}",
            spelExpression = "#qo.UserId:#qo.Keyword"
        });
    }

    /// <summary>
    /// 【3.2】SpEL 字典键访问
    /// </summary>
    [Description("【3.2】SpEL 字典键访问 #cfg.site_name:#cfg.version")]
    [HttpGet("spel/config")]
    public IActionResult SpelConfig()
    {
        var cfg = new Dictionary<string, object>
        {
            ["site_name"] = "MyApp",
            ["version"] = "2.0.0"
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var config = _spelKey.GetConfig(cfg);
        sw.Stop();
        return Ok(new
        {
            data = config,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _spelKey.CallCount,
            cacheKey = $"spel:config:MyApp:2.0.0",
            spelExpression = "#cfg.site_name:#cfg.version"
        });
    }

    /// <summary>
    /// 【3.3】SpEL 位置参数 #p0, #p1
    /// </summary>
    [Description("【3.3】SpEL 位置参数 #p0")]
    [HttpGet("spel/positional/{id}")]
    public IActionResult SpelPositional(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _spelKey.GetByIdPositional(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _spelKey.CallCount,
            spelExpression = "#p0（位置参数，等价于 #id）"
        });
    }

    /// <summary>
    /// 【3.4】SpEL 默认方法名作为 cacheName
    /// </summary>
    [Description("【3.4】SpEL 默认方法名作为 cacheName")]
    [HttpGet("spel/default-name/{id}")]
    public IActionResult SpelDefaultName(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _spelKey.GetUserByDefaultName(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _spelKey.CallCount,
            cacheName = "自动使用方法全限定名",
            note = "未指定 cacheName 时，框架会使用 '命名空间.类名.方法名' 作为缓存区域"
        });
    }

    /// <summary>
    /// 【3.5】SpEL + Redis + 滑动过期组合
    /// </summary>
    [Description("【3.5】SpEL + Redis + 滑动过期组合")]
    [HttpGet("spel/sliding-redis/{id}")]
    public IActionResult SpelSlidingRedis(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _spelKey.GetUserSlidingRedis(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _spelKey.CallCount,
            cacheType = "Redis + 滑动过期",
            note = "SpEL 表达式 + Redis 后端 + slidingTtl 组合，需要配置 Redis 连接串"
        });
    }
}
