using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Sentinel.Middleware;

namespace Yzl.Extensions.Samples.Sentinel.Controllers;

/// <summary>
/// 熔断降级演示 API（全局异常处理接管降级，不走 [SentinelResource] 的 Fallback 属性）。
///
/// 设计对比：
/// - DegradeDemoController（页面）走 <c>Fallback</c> 静态方法降级；
/// - 本控制器**故意不配置 <see cref="SentinelResourceAttribute.Fallback"/>**：
///   业务抛异常 → SentinelMiddleware 未命中 Fallback → 原样 <c>throw</c> →
///   <see cref="ApiExceptionHandler"/>（全局异常中间件）捕获 → 返回结构化 JSON 降级响应。
///   熔断器对异常计数照常（<c>SentinelMiddleware</c> 的 finally 里 <c>entry.Exit(error)</c> 上报异常）。
///
/// 需要人工在 Nacos <c>degrade-config</c> 添加一条 grade=2（异常数）规则，如：
///   {"resource":"degrade:api:error-count","grade":2,"count":2,"timeWindow":10,"minRequestAmount":1,"statIntervalMs":1000}
/// 连续 2 次抛异常即熔断 10s；熔断期间请求被 SentinelMiddleware 拦截（不经本 action），
/// 由 <see cref="ApiExceptionHandler"/> 检测 <see cref="SentinelBlockException"/> 并返回 429 JSON。
/// </summary>
[ApiController]
[Route("api/degrade")]
[SentinelResource("degrade:api:error-count")]
public class DegradeApiController : ControllerBase
{
    /// <summary>
    /// 模拟业务故障：<c>?fail=true</c> 抛异常（计入熔断 errorCount）。
    /// 未配置 Fallback → 异常抛给全局异常处理 <see cref="ApiExceptionHandler"/> 降级。
    /// </summary>
    [HttpGet("fail")]
    public IActionResult Fail([FromQuery] bool fail)
    {
        if (fail)
        {
            throw new InvalidOperationException("模拟 API 业务异常（?fail=true），由全局异常处理降级");
        }

        return Ok(new
        {
            code = 200,
            message = "ok",
            resource = "degrade:api:error-count",
            time = DateTime.Now.ToString("HH:mm:ss.fff"),
        });
    }
}
