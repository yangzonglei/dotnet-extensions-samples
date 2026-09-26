using System.Diagnostics;
using Yzl.Extensions.Actuator.Abstractions;
using Yzl.Extensions.Actuator.Endpoints.Health;

namespace Yzl.Extensions.Samples.Actuator.Custom;

/// <summary>
/// 数据库健康检查 — 演示 <see cref="HealthComponents"/> 工厂方法 + 耗时统计。
///
/// <para>
/// 对应 Spring Boot 的写法：
/// <code>
/// Health.up().withDetail("database", "1").withDetail("latency", "3ms").build();
/// </code>
/// 本类用 <c>HealthComponents.Up(("database", "1"), ("latency", "3ms"))</c> 表达同样语义。
/// </para>
///
/// <para>
/// 注册方式：无需手工注册。<c>AddSpringNetActuator</c> 内部调用
/// <c>TryRegisterImplementations&lt;IHealthContributor&gt;()</c>，
/// 会自动扫描并注册当前程序集中所有 <see cref="IHealthContributor"/> 实现。
/// </para>
///
/// <para>
/// 访问 /actuator/health 可在 <c>details.db</c> 看到本组件的结果。
/// </para>
/// </summary>
public sealed class DatabaseHealthContributor : IHealthContributor
{
    public string Name => "db";

    public Task<HealthComponent> CheckAsync(CancellationToken ct)
    {
        try
        {
            var sw = Stopwatch.StartNew();

            // 此处可放真实的数据库探活，例如：
            //   await using var conn = new SqlConnection(_connectionString);
            //   await conn.OpenAsync(ct);
            sw.Stop();

            return HealthComponents.Up(
                ("database", "1"),
                ("latency", $"{sw.ElapsedMilliseconds}ms"));
        }
        catch (Exception ex)
        {
            // HealthComponents.Down(ex) 会自动写入 error = ex.Message、exception = ex.GetType().Name
            return HealthComponents.Down(ex);
        }
    }
}
