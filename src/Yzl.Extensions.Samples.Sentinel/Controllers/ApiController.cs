using Microsoft.AspNetCore.Mvc;

namespace Yzl.Extensions.Samples.Sentinel.Controllers;

/// <summary>Sentinel 限流演示 API（429 走 JSON 分支）。</summary>
[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    /// <summary>
    /// API 限流演示。规则 spring.cloud.sentinel.rules.flow 里 resource=/api/ratelimit、grade=1、count=1：
    /// 快速调用超过 1 QPS 即返回 429 JSON（[ApiController] 恒走 JSON 分支——即使浏览器直接导航本 URL
    /// 也返回 JSON，不会返回 HTML 页面；响应体来自 UseSentinelBlockExceptionHandler 的 JsonTemplate 配置）。
    /// </summary>
    [HttpGet("ratelimit")]
    public IActionResult RateLimit()
    {
        return Ok(new
        {
            code = 200,
            message = "ok",
            time = DateTime.Now.ToString("HH:mm:ss.fff"),
        });
    }
}
