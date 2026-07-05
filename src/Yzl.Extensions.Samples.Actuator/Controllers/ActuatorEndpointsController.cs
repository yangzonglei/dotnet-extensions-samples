using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Actuator.Controllers;

/// <summary>
/// Actuator 端点测试代理 — 通过 HttpClient 转发到真实的 /actuator/* 端点，
/// 让 Dashboard 能够发现和测试 Actuator 端点。
///
/// Actuator 的 Minimal API 路由注册时未附加 WithTest() 元数据，
/// 因此 Dashboard 无法自动发现它们。本 Controller 作为桥梁暴露可测试端点。
/// </summary>
[ApiController]
[Route("api/actuator-test")]
[TestDashboardInfo("🔧 Actuator 端点", Order = 3, Badge = "代理")]
public class ActuatorEndpointsController(IHttpClientFactory httpClientFactory, IConfiguration configuration) : ControllerBase
{
    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient();
        // 从配置或默认值获取本机地址
        var baseUrl = configuration.GetValue<string>("ActuatorTest:BaseUrl") ?? "http://localhost:16601";
        client.BaseAddress = new Uri(baseUrl);
        return client;
    }

    /// <summary>
    /// GET /api/actuator-test/health — 健康检查
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<object>("/actuator/health");
        return Ok(new { actuatorEndpoint = "/actuator/health", data = result });
    }

    /// <summary>
    /// GET /api/actuator-test/info — 应用信息
    /// </summary>
    [HttpGet("info")]
    public async Task<IActionResult> Info()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<object>("/actuator/info");
        return Ok(new { actuatorEndpoint = "/actuator/info", data = result });
    }

    /// <summary>
    /// GET /api/actuator-test/metrics — 指标列表
    /// </summary>
    [HttpGet("metrics")]
    public async Task<IActionResult> Metrics()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<object>("/actuator/metrics");
        return Ok(new { actuatorEndpoint = "/actuator/metrics", data = result });
    }

    /// <summary>
    /// GET /api/actuator-test/env — 环境配置
    /// </summary>
    [HttpGet("env")]
    public async Task<IActionResult> Env()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<object>("/actuator/env");
        return Ok(new { actuatorEndpoint = "/actuator/env", data = result });
    }

    /// <summary>
    /// GET /api/actuator-test/loggers — 日志级别
    /// </summary>
    [HttpGet("loggers")]
    public async Task<IActionResult> Loggers()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<object>("/actuator/loggers");
        return Ok(new { actuatorEndpoint = "/actuator/loggers", data = result });
    }

    /// <summary>
    /// GET /api/actuator-test/beans — DI 容器 Bean 列表
    /// </summary>
    [HttpGet("beans")]
    public async Task<IActionResult> Beans()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<object>("/actuator/beans");
        return Ok(new { actuatorEndpoint = "/actuator/beans", data = result });
    }

    /// <summary>
    /// GET /api/actuator-test/caches — 缓存管理
    /// </summary>
    [HttpGet("caches")]
    public async Task<IActionResult> Caches()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<object>("/actuator/caches");
        return Ok(new { actuatorEndpoint = "/actuator/caches", data = result });
    }

    /// <summary>
    /// GET /api/actuator-test/mappings — 路由映射
    /// </summary>
    [HttpGet("mappings")]
    public async Task<IActionResult> Mappings()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<object>("/actuator/mappings");
        return Ok(new { actuatorEndpoint = "/actuator/mappings", data = result });
    }

    /// <summary>
    /// GET /api/actuator-test/conditions — 条件评估
    /// </summary>
    [HttpGet("conditions")]
    public async Task<IActionResult> Conditions()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<object>("/actuator/conditions");
        return Ok(new { actuatorEndpoint = "/actuator/conditions", data = result });
    }

    /// <summary>
    /// GET /api/actuator-test/metadata — 应用元数据
    /// </summary>
    [HttpGet("metadata")]
    public async Task<IActionResult> Metadata()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<object>("/actuator/metadata");
        return Ok(new { actuatorEndpoint = "/actuator/metadata", data = result });
    }

    /// <summary>
    /// GET /api/actuator-test/httptrace — HTTP 请求追踪
    /// </summary>
    [HttpGet("httptrace")]
    public async Task<IActionResult> Httptrace()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<object>("/actuator/httptrace");
        return Ok(new { actuatorEndpoint = "/actuator/httptrace", data = result });
    }

    /// <summary>
    /// GET /api/actuator-test/custom — 自定义端点
    /// </summary>
    [HttpGet("custom")]
    public async Task<IActionResult> Custom()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<object>("/actuator/custom");
        return Ok(new { actuatorEndpoint = "/actuator/custom", data = result });
    }

    /// <summary>
    /// GET /api/actuator-test — 根端点入口
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Root()
    {
        var client = CreateClient();
        var result = await client.GetFromJsonAsync<object>("/actuator");
        return Ok(new { actuatorEndpoint = "/actuator", data = result });
    }
}
