using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Sentinel.Middleware;

namespace Yzl.Extensions.Samples.Sentinel.Controllers;

/// <summary>Sentinel 限流/熔断演示页。</summary>
public class HomeController : Controller
{
    /// <summary>首页：Sentinel 特性导航。</summary>
    public IActionResult Index() => View();

    /// <summary>
    /// QPS 限流演示。规则 spring.cloud.sentinel.rules.flow 里 resource=/Home/RateLimitDemo、grade=1（QPS）、count=2：
    /// 浏览器快速刷新本页超过 2 QPS 即命中 429，渲染 Views/Shared/RateLimit.cshtml（URL 不变）。
    /// </summary>
    public IActionResult RateLimitDemo()
    {
        ViewBag.Time = DateTime.Now.ToString("HH:mm:ss.fff");
        return View();
    }

    /// <summary>
    /// 慢请求熔断演示。规则 spring.cloud.sentinel.rules.degrade 里 resource=/Home/Slow、grade=0（慢比例）、
    /// count=300（RT 阈值毫秒）、slowRatioThreshold=0.5：
    /// 本页故意耗时 400ms，当 1s 窗口内慢请求（RT&gt;300ms）比例 ≥ 50% 且请求数 ≥ 5 时熔断 10s，
    /// 期间所有请求返回 429。
    /// </summary>
    public async Task<IActionResult> Slow(CancellationToken cancellationToken)
    {
        await Task.Delay(400, cancellationToken);
        ViewBag.Time = DateTime.Now.ToString("HH:mm:ss.fff");
        return View();
    }

    /// <summary>
    /// SentinelResource 特性手动覆盖资源 key 演示。
    /// 标注 [SentinelResource("home:shared")] 后，本动作不再用 URL /Home/SharedResource 当资源 key，
    /// 而是统一归一到 <c>home:shared</c>（<see cref="SentinelResourceAttribute"/> 优先级 &gt; 路由模板 &gt; 请求路径）；
    /// 同一资源可被多个入口复用，共享同一套限流/熔断规则（对标 Java @SentinelResource(value="xxx")）。
    /// 限流规则匹配 Nacos data-id 里 resource=home:shared、grade=1、count=1：快速刷新本页超 1 QPS 即命中 429。
    /// </summary>
    [SentinelResource("home:shared")]
    public IActionResult SharedResource()
    {
        ViewBag.Time = DateTime.Now.ToString("HH:mm:ss.fff");
        return View();
    }
}
