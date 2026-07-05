using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Actuator.Controllers;

/// <summary>
/// Actuator 示例系统的首页 Controller
/// </summary>
[ApiController]
[TestDashboardInfo("🏠 首页", Order = 1, Badge = "Actuator")]
public class HomeController : ControllerBase
{
    [HttpGet("/api/actuator/overview")]
    public IActionResult Overview()
    {
        return Ok(new
        {
            title = "Yzl.Extensions.Actuator 示例",
            version = "1.0.0",
            description = "演示 Spring Boot 风格的 Actuator 端点，包括健康检查、指标、环境、日志管理、缓存、Bean 等",
            endpoints = new[]
            {
                new { path = "/actuator", method = "GET", desc = "Actuator 根端点 — 列出所有可用端点的 HAL 链接" },
                new { path = "/actuator/health", method = "GET", desc = "健康检查 — 包含 Ping、磁盘空间、自定义检查" },
                new { path = "/actuator/info", method = "GET", desc = "应用信息 — 包含构建信息、环境信息、自定义信息" },
                new { path = "/actuator/metrics", method = "GET", desc = "指标列表 — CPU、内存、线程、GC 等" },
                new { path = "/actuator/metrics/{name}", method = "GET", desc = "指定指标的详细信息" },
                new { path = "/actuator/env", method = "GET", desc = "环境配置属性" },
                new { path = "/actuator/loggers", method = "GET", desc = "日志级别列表" },
                new { path = "/actuator/loggers/{name}", method = "POST", desc = "运行时修改指定 Logger 的日志级别" },
                new { path = "/actuator/beans", method = "GET", desc = "DI 容器中注册的所有 Bean（服务）" },
                new { path = "/actuator/caches", method = "GET", desc = "缓存管理器列表" },
                new { path = "/actuator/mappings", method = "GET", desc = "当前应用的所有路由映射" },
                new { path = "/actuator/conditions", method = "GET", desc = "条件评估报告" },
                new { path = "/actuator/metadata", method = "GET", desc = "应用元数据" },
                new { path = "/actuator/httptrace", method = "GET", desc = "HTTP 请求追踪记录" },
            }
        });
    }
}
