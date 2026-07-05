using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Actuator;
using Yzl.Extensions.Actuator.Abstractions;
using Yzl.Extensions.Actuator.Extensions;
using Yzl.Extensions.Actuator.Endpoints.Info;
using Yzl.Extensions.Samples.Actuator;
using Yzl.Extensions.Samples.Actuator.Custom;
using Yzl.Extensions.Samples.TestDashboard;

Console.WriteLine("""
                  ╔═══════════════════════════════════════════════════════╗
                  ║     Yzl.Extensions.Actuator 示例                     ║
                  ║                                                      ║
                  ║     访问: http://localhost:16601                      ║
                  ║     Actuator: /actuator                              ║
                  ║     仪表盘: /dashboard                                ║
                  ║                                                      ║
                  ║     演示 Actuator 健康检查 / 指标 / 环境 /           ║
                  ║     日志管理 / 缓存 / Bean / HttpTrace 等端点        ║
                  ╚═══════════════════════════════════════════════════════╝
                  """);

var builder = WebApplication.CreateBuilder(args);

// ── 注册控制器 ──
builder.Services.AddControllers();

// ── 注册 HttpClientFactory（用于 ActuatorEndpointsController 代理调用 Actuator） ──
builder.Services.AddHttpClient();

// ── 注册 Actuator ──
// AddSpringNetActuator 会自动扫描并注册所有 IActuatorEndpoint、IHealthContributor、
// IInfoContributor 以及 Loggers/Caches/Metrics 等核心能力
builder.Services.AddSpringNetActuator(builder.Configuration);

// ── 注册自定义的 IHealthContributor ──
builder.Services.AddSingleton<IHealthContributor, CustomHealthContributor>();

// ── 注册自定义的 IInfoContributor ──
builder.Services.AddSingleton<IInfoContributor, CustomInfoContributor>();

// ── 关闭 [ApiController] 自动 400 校验 ──
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

// ── NLog ──
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// ── Actuator HttpTrace 中间件（放在最前面以捕获所有请求） ──
app.UseSpringNetActuatorHttpTrace();

// ── Actuator Endpoints（复用业务端口模式） ──
app.UseSpringNetActuatorMapEndpoints();

// ── Controller 路由 ──
app.MapControllers();

// ── 根路径 ──
app.MapGet("/", () => new
{
    message = "Yzl.Extensions.Actuator Samples",
    actuator = "/actuator",
    dashboard = "/dashboard",
    examples = new[]
    {
        "/actuator",
        "/actuator/health",
        "/actuator/info",
        "/actuator/metrics",
        "/actuator/env",
        "/actuator/loggers",
        "/actuator/beans",
        "/actuator/caches",
        "/actuator/mappings",
        "/actuator/conditions",
        "/actuator/metadata",
        "/actuator/httptrace",
        "/custom-health",
        "/custom-info",
        "/custom-endpoint"
    }
});

// ── 测试仪表盘 ──
app.MapTestDashboard(new TestDashboardOptions
{
    Title = "Yzl.Extensions.Actuator 示例",
    Groups = new()
    {
        ["Home"] = ("🏠 首页", ""),
        ["ActuatorDemo"] = ("⚡ Actuator 演示端点", "自定义"),
        ["ActuatorEndpoints"] = ("🔧 Actuator 端点", "代理"),
        ["actuator"] = ("🔧 Actuator 端点", "")
    }
});

app.Run();
