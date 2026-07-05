using Yzl.Extensions.Actuator;
using Yzl.Extensions.Actuator.Extensions;
using Yzl.Extensions.Core.Extensions;
using Yzl.Extensions.SpringBoot.Admin.Client.Net;

Console.WriteLine("""
                  ╔═══════════════════════════════════════════════════════╗
                  ║     Yzl.Extensions.SpringBoot.Admin.Net 测试        ║
                  ║                                                      ║
                  ║     访问: http://localhost:16606                      ║
                  ║     Actuator: /actuator/health                       ║
                  ║                                                      ║
                  ║     Spring Boot Admin 客户端注册演示                 ║
                  ║     健康检查 / 指标收集 / 日志管理 / 环境信息       ║
                  ║                                                      ║
                  ║     需配置 spring:boot:admin:client:url              ║
                  ║     注册到 Spring Boot Admin Server（需要启动 SBA 服务端） ║
                  ╚═══════════════════════════════════════════════════════╝
                  """);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSpringBootAdminClient(builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => new
{
    message = "Yzl.Extensions.SpringBoot.Admin.Client.Net Samples",
    actuator = "/actuator",
    sba = "Configure spring:boot:admin:client:url to register this app to Spring Boot Admin Server.",
    examples = new[]
    {
        "/actuator/health",
        "/actuator/info",
        "/actuator/metrics",
        "/actuator/loggers",
        "/actuator/beans",
        "/actuator/httptrace"
    }
});

// ===== Spring.Net Actuator HttpTrace 中间件（注册在管道最前面以捕获所有请求），生产环境慎用 =====
app.UseSpringNetActuatorHttpTrace();

// ===== Spring.Net Actuator Endpoints(复用业务端口时，必须配置) =====
// app.UseSpringNetActuatorMapEndpoints(builder.Configuration);

// ===== Spring.Net Actuator Endpoints =====
app.MapControllers();
app.RegisterApplicationLifetimeEvents();

app.Run();
