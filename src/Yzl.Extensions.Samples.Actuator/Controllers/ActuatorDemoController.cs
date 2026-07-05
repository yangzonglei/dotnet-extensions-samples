using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Actuator.Controllers;

/// <summary>
/// 演示 Actuator 自定义端点与自定义 Contributor 的测试 Controller
/// </summary>
[ApiController]
[Route("api/actuator-demo")]
[TestDashboardInfo("⚡ Actuator 演示端点", Order = 2, Badge = "自定义")]
public class ActuatorDemoController : ControllerBase
{
    /// <summary>
    /// 演示自定义 IHealthContributor 的效果
    /// </summary>
    [HttpGet("custom-health")]
    public IActionResult GetCustomHealth()
    {
        return Ok(new
        {
            message = "访问 /actuator/health 查看 customHealth 组件状态",
            description = "CustomHealthContributor 演示如何自定义健康检查组件",
            status = "UP — 所有自定义检查通过",
            checks = new[]
            {
                new { name = "database", status = "UP", detail = "数据库连接正常 (模拟)" },
                new { name = "redis", status = "UP", detail = "Redis 连接正常 (模拟)" },
                new { name = "external-api", status = "UP", detail = "外部 API 可达 (模拟)" }
            }
        });
    }

    /// <summary>
    /// 演示自定义 IInfoContributor 的效果
    /// </summary>
    [HttpGet("custom-info")]
    public IActionResult GetCustomInfo()
    {
        return Ok(new
        {
            message = "访问 /actuator/info 查看 CustomInfoContributor 添加的额外信息",
            description = "CustomInfoContributor 演示如何在 /actuator/info 中添加自定义信息",
            customInfo = new
            {
                team = "Yzl.Extensions Team",
                project = "Yzl.Extensions.Actuator Samples",
                version = "1.0.0",
                contact = "developer@example.com"
            }
        });
    }

    /// <summary>
    /// 演示自定义 Actuator Endpoint 的效果
    /// </summary>
    [HttpGet("custom-endpoint")]
    public IActionResult GetCustomEndpoint()
    {
        return Ok(new
        {
            message = "访问 /actuator/custom 查看自定义端点输出",
            description = "CustomActuatorEndpoint 实现 IActuatorEndpoint，自动注册为 /actuator/custom 端点",
            sampleOutput = new
            {
                timestamp = DateTime.UtcNow,
                application = "Yzl.Extensions.Actuator Samples",
                features = new[]
                {
                    "Custom health checks",
                    "Custom info contributors",
                    "Custom actuator endpoints"
                }
            }
        });
    }

    /// <summary>
    /// 演示日志级别动态修改
    /// </summary>
    [HttpGet("logger-demo")]
    public IActionResult LoggerDemo([FromServices] ILogger<ActuatorDemoController> logger)
    {
        logger.LogTrace("TRACE 级别日志 — 通常不显示");
        logger.LogDebug("DEBUG 级别日志 — 开发环境显示");
        logger.LogInformation("INFO 级别日志 — 演示 Actuator 日志管理");
        logger.LogWarning("WARN 级别日志 — 可通过 Actuator 修改日志级别");
        logger.LogError("ERROR 级别日志 — 始终显示");

        return Ok(new
        {
            message = "已输出不同级别的日志",
            hint = "通过 POST /actuator/loggers/Yzl.Extensions.Samples.Actuator 修改日志级别，body: {\"configuredLevel\":\"DEBUG\"}",
            currentLevel = "INFO (默认)",
            actuactorDocs = "/actuator/loggers"
        });
    }

    /// <summary>
    /// 强制 GC 演示内存指标
    /// </summary>
    [HttpGet("gc-demo")]
    public IActionResult GCDemo()
    {
        var before = GC.GetTotalMemory(false);
        // 分配一些临时内存
        var temp = new byte[1024 * 1024]; // 1MB
        temp[0] = 1;
        var after = GC.GetTotalMemory(true);

        return Ok(new
        {
            message = "访问 /actuator/metrics 查看 GC 相关指标",
            metrics = new
            {
                memoryBeforeBytes = before,
                memoryAfterAllocationBytes = after,
                totalAllocatedBytes = GC.GetTotalAllocatedBytes(),
                gen0Collections = GC.CollectionCount(0),
                gen1Collections = GC.CollectionCount(1),
                gen2Collections = GC.CollectionCount(2)
            }
        });
    }
}
