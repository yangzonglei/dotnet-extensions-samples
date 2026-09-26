using NLog;
using NLog.Config;
using NLog.Targets;
using LogLevel = NLog.LogLevel;

namespace Yzl.Extensions.Samples.Actuator.Extensions;

/// <summary>
/// 应用生命周期日志 + NLog 运行时规则注入演示。
///
/// <para>
/// 演示两件事：
/// </para>
/// <list type="number">
///   <item>
///     <description>
///     用 <see cref="IHostApplicationLifetime"/> 的 Started / Stopping / Stopped 回调，
///     在应用启动、正在停止、已停止三个时点各打一条日志。
///     </description>
///   </item>
///   <item>
///     <description>
///     向 NLog 的 <c>LoggingConfiguration.LoggingRules</c> <b>头部</b>插入一条运行时规则
///     （<see cref="LoggingRule"/>），为指定命名空间开启独立的最低日志级别。
///     NLog 按规则顺序匹配，插入到索引 0 的规则优先级最高。
///     这与 <c>POST /actuator/loggers/{name}</c> 的运行时改级别是两套独立机制：
///     前者改的是 NLog 配置，后者改的是 Actuator 的日志级别覆盖表。
///     </description>
///   </item>
/// </list>
///
/// <para>
/// 注意：<c>LogManager.Configuration</c> 在未调用 <c>UseNLog()</c> 或缺少 nlog.config 时为 null，
/// 因此本扩展必须在 NLog 初始化之后（<c>builder.Host.UseNLog()</c> 之后、<c>app.Run()</c> 之前）调用。
/// </para>
/// </summary>
public static class LifetimeExtensions
{
    /// <summary>
    /// 注册应用程序生命周期事件，并为指定命名空间注入一条 NLog 运行时规则。
    /// </summary>
    /// <param name="applicationBuilder">IApplicationBuilder。</param>
    /// <param name="applicationName">应用程序名称，写入生命周期日志。</param>
    /// <param name="ruleNamespace">要注入规则的命名空间前缀，默认取本类的命名空间。</param>
    public static void RegisterApplicationLifetimeEvents(
        this IApplicationBuilder applicationBuilder,
        string applicationName = "Yzl.Extensions.Samples",
        string? ruleNamespace = null)
    {
        var applicationLifetime = applicationBuilder.ApplicationServices.GetService<IHostApplicationLifetime>();
        var logger = applicationBuilder.ApplicationServices.GetService<ILogger<RegisterLifetimeExtensions>>();

        var config = LogManager.Configuration;
        if (config != null)
        {
            var ns = ruleNamespace ?? typeof(LifetimeExtensions).Namespace;

            // 插入到索引 0：NLog 按顺序匹配规则，越靠前优先级越高。
            // 这里为示例程序集的命名空间单独开启 Info 级别，输出到第一个可用 target。
            var rule = new LoggingRule(ns, LogLevel.Info, GetOrCreateDefaultTarget(config));
            config.LoggingRules.Insert(0, rule);

            logger?.LogInformation("已向 NLog 注入运行时规则：logger={Namespace}, minlevel=Info", ns);
        }

        applicationLifetime?.ApplicationStarted.Register(() =>
            logger?.LogInformation("{ApplicationName} ApplicationStarted Successfully！", applicationName));

        applicationLifetime?.ApplicationStopping.Register(() =>
            logger?.LogInformation("{ApplicationName} ApplicationStopping！", applicationName));

        applicationLifetime?.ApplicationStopped.Register(() =>
            logger?.LogInformation("{ApplicationName} ApplicationStopped！", applicationName));
    }

    /// <summary>
    /// 取 NLog 配置中的第一个 target；若配置里没有任何 target，则临时创建一个控制台 target。
    /// </summary>
    private static Target GetOrCreateDefaultTarget(LoggingConfiguration config)
    {
        var target = config.AllTargets.FirstOrDefault();
        if (target == null)
        {
            var consoleTarget = new ConsoleTarget("console")
            {
                Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}"
            };
            config.AddTarget(consoleTarget);
            target = consoleTarget;
        }

        return target;
    }
}

/// <summary>
/// 用于获取 <see cref="ILogger{T}"/> 的标记类型（生命周期日志的 logger 名称）。
/// </summary>
public class RegisterLifetimeExtensions
{
}
