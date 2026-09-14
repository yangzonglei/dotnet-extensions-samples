using Microsoft.AspNetCore.Mvc;

namespace Yzl.Extensions.Samples.Sentinel.Controllers;

/// <summary>
/// 限流（flow）全维度演示 API（429 走 JSON 分支）。与 <see cref="FlowDemoController"/>（页面）对照：
/// 同一套 grade × strategy × controlBehavior 维度，此处以 [ApiController] + 属性路由呈现。
/// 资源 key = 属性路由模板（/api/flow/*，由 SentinelResourceKeyBuilder 归一化，镜像
/// <see cref="ApiController"/> 的 /api/ratelimit），故**不加** [SentinelResource] 手动覆盖。
///
/// 限流规则在 Nacos data-id「flow-config」配置；被限流时由库内
/// UseSentinelBlockExceptionHandler 中间件输出 JsonTemplate 的 429 JSON
/// （[ApiController] 恒走 JSON 分支，与 Accept 无关）。
///
/// ⚠️ 链路（strategy=2）为**退化实现**（同 <see cref="FlowDemoController.Chain"/>）：
/// 规则已配置 &amp; 加载、语义退化为直连（refResource 仅用于通过校验），如实演示 + 标注退化，不改库。
/// </summary>
[ApiController]
[Route("api/flow")]
public class FlowApiController : ControllerBase
{
    /// <summary>
    /// QPS + 直接 + 快速失败（grade=1, strategy=0, controlBehavior=0, count=2）。
    /// 规则（Nacos flow-config）：
    ///   {"resource":"/api/flow/qps-fast-fail","grade":1,"count":2,"strategy":0,"controlBehavior":0}
    /// 快速并发调用超过 2 QPS 即返回 429 JSON。
    /// </summary>
    [HttpGet("qps-fast-fail")]
    public IActionResult QpsFastFail() => Ok(Result("/api/flow/qps-fast-fail", "count=2, grade=1, strategy=0, controlBehavior=0"));

    /// <summary>
    /// QPS + 直接 + 预热（grade=1, strategy=0, controlBehavior=1, count=10, warmUpPeriodSec=10）。
    /// 规则（Nacos flow-config）：
    ///   {"resource":"/api/flow/qps-warmup","grade":1,"count":10,"strategy":0,"controlBehavior":1,"warmUpPeriodSec":10}
    /// 空闲后冷启动：阈值从约 count/3 爬升到 10，超过当前可放行速率即 429 JSON。
    /// </summary>
    [HttpGet("qps-warmup")]
    public IActionResult QpsWarmUp() => Ok(Result("/api/flow/qps-warmup", "count=10, grade=1, strategy=0, controlBehavior=1, warmUpPeriodSec=10"));

    /// <summary>
    /// QPS + 直接 + 匀速排队（grade=1, strategy=0, controlBehavior=2, count=1, maxQueueingTimeMs=5000）。
    /// 规则（Nacos flow-config）：
    ///   {"resource":"/api/flow/qps-queue-wait","grade":1,"count":1,"strategy":0,"controlBehavior":2,"maxQueueingTimeMs":5000}
    /// 突发请求在引擎内 Task.Delay 排队等放行（间隔约 1s），预计等待超过 5000ms 才 429 JSON；
    /// 观察 time 字段间距可看到匀速效果。
    /// </summary>
    [HttpGet("qps-queue-wait")]
    public IActionResult QpsQueueWait() => Ok(Result("/api/flow/qps-queue-wait", "count=1, grade=1, strategy=0, controlBehavior=2, maxQueueingTimeMs=5000"));

    /// <summary>
    /// QPS + 关联（grade=1, strategy=1, controlBehavior=0, count=1, refResource=/api/flow/related-source）。
    /// 规则（Nacos flow-config）：
    ///   {"resource":"/api/flow/related","grade":1,"count":1,"strategy":1,"controlBehavior":0,"refResource":"/api/flow/related-source"}
    /// 规则挂在 /api/flow/related（A）上、refResource=B：B 热时 A 被限（429 JSON），B 本身不限。
    /// </summary>
    [HttpGet("related")]
    public IActionResult Related() => Ok(Result("/api/flow/related", "count=1, grade=1, strategy=1, controlBehavior=0, refResource=/api/flow/related-source"));

    /// <summary>
    /// 相关资源（B）：「关联」演示的压流量来源。count=100 占位，本身不受限。
    /// 规则（Nacos flow-config）：
    ///   {"resource":"/api/flow/related-source","grade":1,"count":100,"strategy":0,"controlBehavior":0}
    /// </summary>
    [HttpGet("related-source")]
    public IActionResult RelatedSource() => Ok(Result("/api/flow/related-source", "count=100 占位（无实际限流）"));

    /// <summary>
    /// QPS + 链路（grade=1, strategy=2, controlBehavior=0, count=2, refResource=/api/flow/chain）。
    /// 规则（Nacos flow-config）：
    ///   {"resource":"/api/flow/chain","grade":1,"count":2,"strategy":2,"controlBehavior":0,"refResource":"/api/flow/chain"}
    /// ⚠️ 链路为退化实现（同页面版）：refResource 仅用于通过校验，判定退化为直连。超过 2 QPS 即 429 JSON。
    /// </summary>
    [HttpGet("chain")]
    public IActionResult Chain() => Ok(Result("/api/flow/chain", "count=2, grade=1, strategy=2, controlBehavior=0（退化直连）"));

    /// <summary>
    /// 线程数 + 直接 + 快速失败（grade=0, strategy=0, controlBehavior=0, count=2）。
    /// 规则（Nacos flow-config）：
    ///   {"resource":"/api/flow/thread","grade":0,"count":2,"strategy":0,"controlBehavior":0}
    /// 动作持有 500ms 让并发可观测；并发在途 &gt; 2 时第 3 个请求即 429 JSON。
    /// </summary>
    [HttpGet("thread")]
    public async Task<IActionResult> Thread(CancellationToken cancellationToken)
    {
        await Task.Delay(500, cancellationToken);
        return Ok(Result("/api/flow/thread", "count=2 并发线程（动作持有 500ms）"));
    }

    /// <summary>
    /// 组装 200 响应体（resource = 真实资源 key 路由模板 /api/flow/{action}，rule = 规则参数可读串）。
    /// </summary>
    private static object Result(string resource, string rule) => new
    {
        code = 200,
        message = "ok",
        resource,
        rule,
        time = DateTime.Now.ToString("HH:mm:ss.fff"),
    };
}
