using Yzl.Extensions.Actuator.Abstractions;
using Yzl.Extensions.Actuator.Endpoints.Health;

namespace Yzl.Extensions.Samples.Actuator.Custom;

/// <summary>
/// 自定义健康检查组件，演示如何为 Actuator 添加自定义健康检查逻辑。
///
/// 注册方式：
/// <code>
/// builder.Services.AddSingleton&lt;IHealthContributor, CustomHealthContributor&gt;();
/// </code>
///
/// 访问 /actuator/health 可在响应中看到 customHealth 组件。
/// </summary>
public class CustomHealthContributor : IHealthContributor
{
    public string Name => "customHealth";

    public Task<HealthComponent> CheckAsync(CancellationToken ct)
    {
        // 模拟健康检查逻辑
        var details = new Dictionary<string, object>
        {
            ["database"] = new { status = "UP", message = "数据库连接正常" },
            ["redis"] = new { status = "UP", message = "Redis 连接正常" },
            ["externalApi"] = new { status = "UP", message = "外部 API 可达" },
            ["lastCheckTime"] = DateTime.UtcNow.ToString("O")
        };

        var result = new HealthComponent
        {
            Status = HealthStatus.Up,
            Details = details
        };

        return Task.FromResult(result);
    }
}
