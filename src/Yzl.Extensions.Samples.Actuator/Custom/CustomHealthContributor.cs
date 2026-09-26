using Yzl.Extensions.Actuator.Abstractions;
using Yzl.Extensions.Actuator.Endpoints.Health;

namespace Yzl.Extensions.Samples.Actuator.Custom;

/// <summary>
/// 自定义健康检查组件，演示如何为 Actuator 添加自定义健康检查逻辑。
///
/// <para>
/// 注册方式：<b>无需手工注册</b>。<c>AddSpringNetActuator</c> 内部调用
/// <c>TryRegisterImplementations&lt;IHealthContributor&gt;()</c>，
/// 会自动扫描并注册当前程序集中所有 <see cref="IHealthContributor"/> 实现。
/// 若确实要手工注册，写法是：
/// <code>
/// builder.Services.AddSingleton&lt;IHealthContributor, CustomHealthContributor&gt;();
/// </code>
/// </para>
///
/// <para>
/// 本例使用 <see cref="HealthComponents"/> 工厂方法构造结果，比手工 <c>new HealthComponent</c> 更简洁：
/// <code>
/// return HealthComponents.Up(("database", ...), ("redis", ...));
/// </code>
/// </para>
///
/// 访问 /actuator/health 可在 <c>details.customHealth</c> 看到本组件的结果。
/// </summary>
public sealed class CustomHealthContributor : IHealthContributor
{
    public string Name => "customHealth";

    public Task<HealthComponent> CheckAsync(CancellationToken ct)
    {
        // 模拟健康检查逻辑：全部正常 → 用 HealthComponents.Up(...) 携带明细。
        // 若某项异常，可改为 return HealthComponents.Down(ex); 或 HealthComponents.Down(("reason", "..."))
        return HealthComponents.Up(
            ("database", new { status = "UP", message = "数据库连接正常" }),
            ("redis", new { status = "UP", message = "Redis 连接正常" }),
            ("externalApi", new { status = "UP", message = "外部 API 可达" }),
            ("lastCheckTime", DateTime.UtcNow.ToString("O")));
    }
}
