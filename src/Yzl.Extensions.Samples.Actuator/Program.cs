using Microsoft.AspNetCore.Mvc;
using NLog.Web;
using Yzl.Extensions.Actuator;
using Yzl.Extensions.Actuator.Abstractions;
using Yzl.Extensions.Actuator.Extensions;
using Yzl.Extensions.Actuator.Endpoints.Info;
using Yzl.Extensions.Samples.Actuator;
using Yzl.Extensions.Samples.Actuator.Custom;
using Yzl.Extensions.Samples.Actuator.Extensions;
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
                  ║                                                      ║
                  ║     独立管理端口模式：                                ║
                  ║       dotnet run -- --management:server:port=26601   ║
                  ║       或在 VS 中选择 "独立管理端口" 启动配置         ║
                  ║       此时 /actuator/* 只监听 26601，业务端口隔离    ║
                  ╚═══════════════════════════════════════════════════════╝
                  """);

var builder = WebApplication.CreateBuilder(args);

// ── 注册控制器 ──
builder.Services.AddControllers();

// ── 注册 HttpClientFactory（用于 ActuatorEndpointsController 代理调用 Actuator） ──
builder.Services.AddHttpClient();

// ── 日志：接入 NLog ──
// /actuator/loggers 由 NLogLoggerManagement 实现，需要存在 NLog 配置（nlog.config）。
// 未接入 NLog 时该端点会抛 InvalidOperationException("No NLog configuration found.")。
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// ── 注册 Actuator ──
// AddSpringNetActuator 会自动扫描并注册所有 IActuatorEndpoint、IHealthContributor、
// IInfoContributor 以及 Loggers/Caches/Metrics 等核心能力。
//
// 端口模式由 management:server:port 决定：
//   - 未配置（默认）：复用业务端口，需在下方调用 UseSpringNetActuatorMapEndpoints()
//   - 已配置（如 26601）：启动独立管理 Kestrel，业务端口上不再暴露 /actuator
builder.Services.AddSpringNetActuator(builder.Configuration);

// ── 注册自定义的 IHealthContributor ──
// 注意：AddSpringNetActuator 内部已通过 TryRegisterImplementations<IHealthContributor>()
// 扫描注册了本程序集里的所有 IHealthContributor，这里无需重复注册。
// 因此 CustomHealthContributor / DatabaseHealthContributor / RedisHealthContributor
// 会被自动发现，并并列出现在 /actuator/health 的 details 中。

// ── 注册自定义的 IInfoContributor ──
builder.Services.AddSingleton<IInfoContributor, CustomInfoContributor>();

// ── 关闭 [ApiController] 自动 400 校验 ──
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

var app = builder.Build();

// ── 应用生命周期事件 + NLog 运行时规则注入 ──
// 演示：启动/停止时打日志，并向 NLog 配置头部插入一条运行时 LoggingRule。
app.RegisterApplicationLifetimeEvents("Yzl.Extensions.Samples.Actuator");

// ── Actuator HttpTrace 中间件（放在最前面以捕获所有请求） ──
app.UseSpringNetActuatorHttpTrace();

// ── Actuator Endpoints（复用业务端口模式） ──
// 配置了 management:server:port 时本调用自动变为 no-op（端点只在管理端口暴露）
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
        "/api/cache/add?name=demo",
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
        ["Cache"] = ("🗄️ 缓存联动", "IMemoryCache"),
        ["actuator"] = ("🔧 Actuator 端点", "")
    }
});

app.Run();
