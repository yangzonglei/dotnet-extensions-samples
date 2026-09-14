using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Sentinel;
using Yzl.Extensions.Sentinel.Middleware;

namespace Yzl.Extensions.Samples.Sentinel.Controllers;

/// <summary>
/// 限流（flow）全维度演示页：QPS/线程数 × 直接/关联/链路 × 快速失败/预热/匀速排队。
/// 资源 key 全部用 <see cref="SentinelResourceAttribute"/> 手动覆盖为短名（<c>flow:page:*</c>），
/// 使规则在 Dashboard / Nacos 里一眼可读（镜像 <see cref="DegradeDemoController"/> 的 <c>degrade:xxx</c> 约定）。
///
/// 限流规则在 Nacos data-id「flow-config」中配置（本机配置了 datasource 时本地
/// <c>spring.cloud.sentinel.rules</c> 被忽略并告警，详见 README「限流规则配置」）；被限流时由库内
/// <see cref="Yzl.Extensions.Sentinel.SentinelMiddlewareExtensions.UseSentinelBlockExceptionHandler"/>
/// 中间件接管：页面渲染 <c>Views/Shared/RateLimit1.cshtml</c>（429，URL 不变），本动作不再执行。
///
/// ⚠️ 链路（strategy=2）在 .NET 库中是**退化实现**：<c>SentinelFlowRuleValidator</c> 仅要求
/// RefResource 非空即通过校验，<c>SentinelFlowRuleChecker</c> 恒用当前资源自身节点检查（checkNode =
/// 当前节点，与直接模式等价），<c>SentinelEngine</c> 不构建调用链。此处**如实演示 + 标注退化**
/// （链路规则已配置 &amp; 加载、语义退化为直连），不做任何库改动。
/// </summary>
public class FlowDemoController : Controller
{
    /// <summary>
    /// 流量整形效果①：QPS + 直接策略 + 快速失败（grade=1, strategy=0, controlBehavior=0）。
    /// 规则（Nacos flow-config）：
    ///   {"resource":"flow:page:qps-fast-fail","grade":1,"count":2,"strategy":0,"controlBehavior":0}
    /// 快速刷新（F5 连点）超过 2 QPS 即命中 429，渲染 RateLimit1.cshtml（URL 不变）。
    /// </summary>
    [SentinelResource("flow:page:qps-fast-fail")]
    public IActionResult QpsFastFail()
    {
        ViewBag.Time = DateTime.Now.ToString("HH:mm:ss.fff");
        ViewBag.Mode = "QPS × 直接 × 快速失败（grade=1, strategy=0, controlBehavior=0）";
        ViewBag.Rule = "count=2 QPS";
        ViewBag.Tip = "浏览器快速刷新本页（F5 连点）超过 2 QPS 即返回 429 页（RateLimit1.cshtml，URL 不变）。";
        return View();
    }

    /// <summary>
    /// 流量整形效果②：QPS + 直接策略 + 预热（grade=1, strategy=0, controlBehavior=1）。
    /// 规则（Nacos flow-config）：
    ///   {"resource":"flow:page:qps-warmup","grade":1,"count":10,"strategy":0,"controlBehavior":1,"warmUpPeriodSec":10}
    /// 冷启动：应用空闲后阈值从低谷（约 count/3）缓慢爬升到 count=10（预热因子来自
    /// spring.cloud.sentinel.flow.cold-factor=3，Token Bucket 冷启动）。先让应用空闲几秒，再持续刷新：
    /// 早期请求以低于阈值的速率通过，几秒后恢复到 10 QPS 全速；超过当前可放行速率即 429。
    /// </summary>
    [SentinelResource("flow:page:qps-warmup")]
    public IActionResult QpsWarmUp()
    {
        ViewBag.Time = DateTime.Now.ToString("HH:mm:ss.fff");
        ViewBag.Mode = "QPS × 直接 × 预热（grade=1, strategy=0, controlBehavior=1）";
        ViewBag.Rule = "count=10 QPS, warmUpPeriodSec=10（预热因子 cold-factor=3）";
        ViewBag.Tip = "先空闲 3~5 秒再连续刷新：冷启动初期放行速率低（约 3 QPS），随后 10s 内爬升到 10 QPS 全速；超过当前可放行速率即 429。";
        return View();
    }

    /// <summary>
    /// 流量整形效果③：QPS + 直接策略 + 匀速排队（grade=1, strategy=0, controlBehavior=2）。
    /// 规则（Nacos flow-config）：
    ///   {"resource":"flow:page:qps-queue-wait","grade":1,"count":1,"strategy":0,"controlBehavior":2,"maxQueueingTimeMs":5000}
    /// 每请求成本约 1000/1=1000ms：突发请求在引擎内 Task.Delay 排队等放行（不阻塞线程），请求被
    /// "拉开间距"而非立刻 429；只有预计等待超过 maxQueueingTimeMs=5000ms 才 429。看本页时间戳间距即可观察匀速效果。
    /// </summary>
    [SentinelResource("flow:page:qps-queue-wait")]
    public async Task<IActionResult> QpsQueueWait(CancellationToken cancellationToken)
    {
        await Task.Delay(200, cancellationToken); // 刻意留出观察窗：配合时间戳看排队间距
        ViewBag.Time = DateTime.Now.ToString("HH:mm:ss.fff");
        ViewBag.Mode = "QPS × 直接 × 匀速排队（grade=1, strategy=0, controlBehavior=2）";
        ViewBag.Rule = "count=1 QPS, maxQueueingTimeMs=5000";
        ViewBag.Tip = "并发连刷：请求被匀速排开（约每秒 1 个），多数为 200；突发超过 5s 排队预算的尾部请求才 429。观察各请求时间戳的间距。";
        return View();
    }

    /// <summary>
    /// 流控策略②：QPS + 关联（grade=1, strategy=1, controlBehavior=0）。
    /// 规则（Nacos flow-config）：
    ///   {"resource":"flow:page:related","grade":1,"count":1,"strategy":1,"controlBehavior":0,"refResource":"flow:page:related-source"}
    /// 规则挂在 A（本页）上、refResource=B（/FlowDemo/RelatedSource 即相关资源页）：
    /// A 的限流判定用 B 的统计节点（<see cref="SentinelEngine"/> 取 GetOrCreateNode(B) 交给 FlowRuleChecker）。
    /// 空闲时刷新 A 正常 200；并发刷 B（压流量）时 A 立即 429，B 本身不限。
    /// </summary>
    [SentinelResource("flow:page:related")]
    public IActionResult Related()
    {
        ViewBag.Time = DateTime.Now.ToString("HH:mm:ss.fff");
        ViewBag.Mode = "QPS × 关联（grade=1, strategy=1, controlBehavior=0, refResource=flow:page:related-source）";
        ViewBag.Rule = "count=1 QPS, refResource=flow:page:related-source";
        ViewBag.Tip = "本页（A）被 B 限流：空闲访问 A 正常；用并发工具刷 B（/FlowDemo/RelatedSource）时，A 立即 429（B 不受限）。";
        return View();
    }

    /// <summary>
    /// 相关资源页（B）：配合「关联」演示的压流量来源。本页不限流规则，可被任何请求刷。
    /// 规则（Nacos flow-config）：
    ///   {"resource":"flow:page:related-source","grade":1,"count":100,"strategy":0,"controlBehavior":0}
    /// count=100 仅作占位（避免空节点），不影响 B 的自由访问。
    /// </summary>
    [SentinelResource("flow:page:related-source")]
    public IActionResult RelatedSource()
    {
        ViewBag.Time = DateTime.Now.ToString("HH:mm:ss.fff");
        ViewBag.Mode = "相关资源（B）";
        ViewBag.Rule = "flow:page:related-source（无实际限流，count=100 占位）";
        ViewBag.Tip = "本页是「关联」演示的流量源 B：并发刷本页，另一页 /FlowDemo/Related（A）会被关联限流。";
        return View();
    }

    /// <summary>
    /// 流控策略③：QPS + 链路（grade=1, strategy=2, controlBehavior=0）。
    /// 规则（Nacos flow-config）：
    ///   {"resource":"flow:page:chain","grade":1,"count":2,"strategy":2,"controlBehavior":0,"refResource":"flow:page:chain"}
    /// ⚠️ **链路为退化实现**：.NET 无调用链上下文，<see cref="SentinelEngine"/> 不构建 chain，
    /// FlowRuleChecker 恒用当前资源自身节点检查（与直接等价）。本规则携带 refResource 以通过校验
    /// （Validator 要求非空），**如实演示 + 标注退化**：行为与直接策略相同，快速刷新超过 2 QPS 即 429。
    /// </summary>
    [SentinelResource("flow:page:chain")]
    public IActionResult Chain()
    {
        ViewBag.Time = DateTime.Now.ToString("HH:mm:ss.fff");
        ViewBag.Mode = "QPS × 链路（grade=1, strategy=2, controlBehavior=0）—— 退化直连语义";
        ViewBag.Rule = "count=2 QPS, strategy=2, refResource=flow:page:chain（退化直连）";
        ViewBag.Tip = "链路在当前 .NET 库退化为直连：refResource 仅用于通过规则校验，判定用当前资源自身节点。快速刷新超过 2 QPS 即 429。";
        return View();
    }

    /// <summary>
    /// 阈值类型②：线程数 + 直接 + 快速失败（grade=0, strategy=0, controlBehavior=0）。
    /// 规则（Nacos flow-config）：
    ///   {"resource":"flow:page:thread","grade":0,"count":2,"strategy":0,"controlBehavior":0}
    /// 动作故意持有 500ms（await Task.Delay(500)），<see cref="SentinelEngine"/> 进入时
    /// node.IncreaseThreadNum()、退出时 DecreaseThreadNum()：并发在途请求数 &gt; count=2 时第 3 个请求即 429。
    /// 用并发工具同时发 3+ 个请求即可观察（每个都持有 500ms，窗口重叠）。
    /// </summary>
    [SentinelResource("flow:page:thread")]
    public async Task<IActionResult> Thread(CancellationToken cancellationToken)
    {
        await Task.Delay(500, cancellationToken); // 持有请求，让并发线程数可观测
        ViewBag.Time = DateTime.Now.ToString("HH:mm:ss.fff");
        ViewBag.Mode = "线程数 × 直接 × 快速失败（grade=0, strategy=0, controlBehavior=0）";
        ViewBag.Rule = "count=2 并发线程（动作持有 500ms）";
        ViewBag.Tip = "用并发工具同时发 3+ 个请求（每个持有 500ms）：并发在途 > 2 时第 3 个请求即 429。";
        return View();
    }
}
