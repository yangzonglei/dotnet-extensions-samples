using Yzl.Extensions.Actuator.Abstractions;
using Yzl.Extensions.Actuator.Endpoints.Health;

namespace Yzl.Extensions.Samples.Actuator.Custom;

/// <summary>
/// Redis 健康检查 — 演示「成功走 <see cref="HealthComponents.Up()"/>、
/// 异常走 <see cref="HealthComponents.Down(Exception)"/>」的标准写法。
///
/// <para>
/// 与 <see cref="DatabaseHealthContributor"/> 一起演示多个 contributor 并列出现在
/// /actuator/health 的 details 中（键名分别为 <c>db</c> 与 <c>redis</c>），
/// 只要有一个返回非 Up，整体状态即为 DOWN。
/// </para>
///
/// <para>
/// 注册方式：无需手工注册，由 <c>TryRegisterImplementations&lt;IHealthContributor&gt;()</c> 自动发现。
/// </para>
/// </summary>
public sealed class RedisHealthContributor : IHealthContributor
{
    /// <summary>设为 true 可模拟 Redis 不可用，观察 /actuator/health 整体转为 DOWN。</summary>
    private static readonly bool SimulateFailure = false;

    public string Name => "redis";

    public Task<HealthComponent> CheckAsync(CancellationToken ct)
    {
        try
        {
            // 此处可放真实的 Redis 探活，例如：
            //   using var redis = await ConnectionMultiplexer.ConnectAsync(_connectionString);
            //   await redis.GetDatabase().PingAsync();

            if (SimulateFailure)
            {
                throw new InvalidOperationException("Redis connection refused (simulated).");
            }

            return HealthComponents.Up();
        }
        catch (Exception ex)
        {
            // Down(ex) 自动写入 error = ex.Message、exception = ex.GetType().Name
            return HealthComponents.Down(ex);
        }
    }
}
