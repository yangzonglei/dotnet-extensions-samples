using System;
using Microsoft.Extensions.DependencyInjection;
using Yzl.Extensions.Http.OpenFeign;
using Yzl.Extensions.Http.OpenFeign.Serializer;

namespace Yzl.Extensions.Samples.OpenFeign.Net48
{
    /// <summary>
    /// OpenFeign.Net48 静态 Service Locator 配置
    ///
    /// 适用于 .NET Framework 4.8 项目（如 WebForms、MVC、WinForms），
    /// 在 Global.asax 或应用启动时调用 FeignConfig.Register()，
    /// 然后通过 FeignConfig.GetFeignClient&lt;T&gt; 获取 Feign 客户端。
    ///
    /// 使用方式（Global.asax.cs）：
    /// <code>
    /// protected void Application_Start()
    /// {
    ///     FeignConfig.Register();
    /// }
    /// </code>
    ///
    /// 使用方式（任意代码位置）：
    /// <code>
    /// var client = FeignConfig.GetFeignClient&lt;IDemoFeignClient&gt;();
    /// var user = await client.GetById(1);
    /// </code>
    /// </summary>
    public static class FeignConfig
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        /// <summary>
        /// 注册 OpenFeign 服务。
        /// 在应用启动时调用一次（如 Global.asax Application_Start）。
        /// </summary>
        public static void Register()
        {
            try
            {
                var services = new ServiceCollection();

                // 注册 OpenFeign 客户端
                services.AddFeignStarter(options =>
                {
                    // 全局默认超时（毫秒）
                    options.Default.Timeout = 5000;

                    // 重试策略
                    options.Default.Retry.Enabled = true;
                    options.Default.Retry.MaxAttempts = 3;
                    options.Default.Retry.DelayMs = 500;

                    // 熔断器
                    options.Default.CircuitBreaker.Enabled = true;
                    options.Default.CircuitBreaker.MinimumThroughput = 5;
                    options.Default.CircuitBreaker.BreakSeconds = 10;

                    // 连接池
                    options.HttpClient.Pool.MaxConnections = 20;
                    options.HttpClient.Pool.ConnectionLifetime = TimeSpan.FromMinutes(5);
                    options.HttpClient.Pool.IdleTimeout = TimeSpan.FromMinutes(2);

                    // 序列化器
                    options.SerializerType = typeof(SystemTextJsonFeignSerializer);
                });

                ServiceProvider = services.BuildServiceProvider();
            }
            catch (Exception ex)
            {
                var msg = ex.ToString();
            }
        }

        /// <summary>
        /// 获取 Feign 客户端实例
        /// </summary>
        /// <typeparam name="T">Feign 客户端接口类型</typeparam>
        /// <returns>Feign 客户端代理实例</returns>
        public static T GetFeignClient<T>() where T : class
        {
            if (ServiceProvider == null)
            {
                throw new InvalidOperationException("FeignConfig has not been initialized. Call FeignConfig.Register() first.");
            }
            return ServiceProvider.GetRequiredService<T>();
        }
    }
}
