using Prometheus;
using Yzl.Extensions.Sentinel.Metrics;

namespace Yzl.Extensions.Samples.Sentinel.Metrics;

/// <summary>
/// 宿主（样例）的 Sentinel → Prometheus 导出器：实现库的 SPI <see cref="ISentinelMetricsExporter"/>，
/// 在 <see cref="Export"/> 里把 <see cref="ISentinelMetricsProvider"/> 的统计写入 prometheus-net
/// （复用 <c>/actuator/prometheus</c>，不新增端点）。
/// 宿主只需声明本类——由库的 <c>SentinelMetricsExporterBootstrap</c> 启动时自动发现并接线，无需手动注册。
///
/// 只导出通过/拦截两类，业务结果与性能指标（success、exception、RT、并发）不导出。
///
/// 导出系列一览（<c>{resource}</c> 标签为 Sentinel 资源名）：
/// <code>
/// 系列                          类型      含义
/// sentinel_pass_qps{resource}   gauge   当前秒通过速率
/// sentinel_block_qps{resource}  gauge   当前秒拦截速率
/// sentinel_pass_total{resource} counter 累计通过次数
/// sentinel_block_total{resource}counter 累计拦截次数
/// </code>
/// <c>block</c> = 被任一规则拦截，含 flow 限流、degrade 熔断降级、authority 黑白名单
/// （见 <c>SentinelEngine.TryEntryAsync</c> 的三个 <c>AddBlock</c> 分支）。
/// Gauge 是当前秒速率，Counter 是进程累计次数（供 <c>rate()/increase()</c> 聚合）。
/// </summary>
public sealed class PrometheusSentinelMetrics : ISentinelMetricsExporter
{
    // 全限定 Prometheus.Metrics：本文件所在命名空间 Yzl.Extensions.Samples.Sentinel.Metrics 会遮蔽静态类 Metrics
    private static readonly Gauge PassQps = Prometheus.Metrics.CreateGauge(
        "sentinel_pass_qps", "Sentinel pass QPS",
        new GaugeConfiguration { LabelNames = new[] { "resource" } });
    private static readonly Gauge BlockQps = Prometheus.Metrics.CreateGauge(
        "sentinel_block_qps", "Sentinel block QPS",
        new GaugeConfiguration { LabelNames = new[] { "resource" } });

    // 累计计数：Counter 语义（单调递增），Prometheus 约定以 _total 结尾
    private static readonly Counter PassTotal = Prometheus.Metrics.CreateCounter(
        "sentinel_pass_total", "Sentinel cumulative passed entries",
        new CounterConfiguration { LabelNames = new[] { "resource" } });
    private static readonly Counter BlockTotal = Prometheus.Metrics.CreateCounter(
        "sentinel_block_total", "Sentinel cumulative blocked entries",
        new CounterConfiguration { LabelNames = new[] { "resource" } });

    /// <summary>
    /// 注册把 pull 快照写入 prometheus-net：每次 Prometheus scrape（GET /actuator/prometheus）前刷新，
    /// 热路径零开销。由库自动发现后调用一次。
    ///
    /// 累计系列用 <c>IncTo</c> 同步引擎的进程级累计值（Counter 本就该只增不减，prometheus-net 亦无公开 Set）。
    /// </summary>
    public void Export(ISentinelMetricsProvider provider)
    {
        Prometheus.Metrics.DefaultRegistry.AddBeforeCollectCallback(() =>
        {
            foreach (var m in provider.CollectCurrent())
            {
                var resource = m.Resource ?? string.Empty; // 引擎节点资源名恒非空，兜底防 null 告警
                PassQps.WithLabels(resource).Set(m.PassQps);
                BlockQps.WithLabels(resource).Set(m.BlockQps);
            }

            foreach (var t in provider.CollectTotals())
            {
                var resource = t.Resource ?? string.Empty;
                PassTotal.WithLabels(resource).IncTo(t.TotalPass);
                BlockTotal.WithLabels(resource).IncTo(t.TotalBlock);
            }
        });
    }
}
