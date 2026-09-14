using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Sentinel;
using Yzl.Extensions.Sentinel.Exceptions;
using Yzl.Extensions.Sentinel.Middleware;

namespace Yzl.Extensions.Samples.Sentinel.Controllers;

/// <summary>
/// 熔断（degrade）三种策略演示：慢调用比例（grade=0）、异常比例（grade=1）、异常数（grade=2）。
/// 资源 key 全部用 <see cref="SentinelResourceAttribute"/> 手动覆盖为短名（<c>degrade:xxx</c>），
/// 使三条规则在 Dashboard / Nacos 规则里一眼可读，也演示"多入口共享同一资源"。
///
/// 降级（fallback）演示（镜像 Java <see cref="SentinelResourceAttribute.Fallback"/>）：
/// 每个动作都声明了静态 <c>XxxFallback(bool fail)</c> / <c>XxxBlocked(bool fail, Exception ex)</c>，
/// —— 业务抛异常 → 中间件调用 <c>Fallback</c>（可带末位 Exception 参数），返回友好提示页而非异常页；
/// —— 熔断拦截 → 中间件调用 <c>BlockHandler</c>（末位带 <see cref="SentinelBlockException"/>），返回降级提示页。
/// 未配置 fallback / blockHandler 时，中间件按拦截原因渲染内置降级页（degradeViewName / 默认模板）。
/// </summary>
public class DegradeDemoController : Controller
{
    /// <summary>
    /// 慢调用比例熔断（grade=0）。规则（Nacos degrade-config）：
    /// resource=degrade:slow-ratio, grade=0, count=300（RT 阈值 ms）, slowRatioThreshold=0.5,
    /// minRequestAmount=5, statIntervalMs=1000, timeWindow=10。
    /// 本页故意耗时 400ms；当 1s 窗口内慢请求（RT&gt;300ms）占比 ≥ 50% 且请求数 ≥ 5 时熔断 10s。
    /// </summary>
    [SentinelResource("degrade:slow-ratio")]
    public async Task<IActionResult> SlowRatio(CancellationToken cancellationToken)
    {
        await Task.Delay(400, cancellationToken);
        ViewBag.Time = DateTime.Now.ToString("HH:mm:ss.fff");
        ViewBag.Mode = "慢调用比例（grade=0）";
        ViewBag.Rule = "count=300ms, slowRatioThreshold=0.5, minRequestAmount=5";
        ViewBag.Tip = "用并发工具同时刷 5+ 个请求（每个耗时 400ms）→ 1s 内慢比例 ≥ 50% 即熔断 10s。";
        return View();
    }

    /// <summary>
    /// 异常比例熔断（grade=1）。规则（Nacos degrade-config）：
    /// resource=degrade:error-ratio, grade=1, count=0.5（异常比例阈值）, minRequestAmount=5,
    /// statIntervalMs=1000, timeWindow=10。
    /// 通过 <c>?fail=1</c> 开关**抛异常**（中间件捕获后计入熔断 errorCount）：请求数 ≥ 5 且
    /// 异常比例 &gt; 0.5 时熔断 10s。注意：4xx/5xx 响应**不抛异常不计入**，必须 throw。
    /// 熔断后（或异常时）走 <see cref="ErrorRatioFallback"/> 降级，不再出现异常页。
    /// </summary>
    [SentinelResource("degrade:error-ratio", Fallback = nameof(ErrorRatioFallback))]
    public IActionResult ErrorRatio([FromQuery] bool fail)
    {
        ViewBag.Time = DateTime.Now.ToString("HH:mm:ss.fff");
        ViewBag.Mode = "异常比例（grade=1）";
        ViewBag.Rule = "count=0.5, minRequestAmount=5";
        ViewBag.Tip = "带 ?fail=true 请求会抛异常；用并发工具同时刷 5+ 个请求，异常比例 > 50% 即熔断 10s（熔断后走降级页）。";

        if (fail)
        {
            throw new InvalidOperationException("模拟异常比例熔断（?fail=true）");
        }

        return View();
    }

    /// <summary>
    /// 异常数熔断（grade=2）。规则（Nacos degrade-config）：
    /// resource=degrade:error-count, grade=2, count=3（异常次数阈值）, minRequestAmount=5,
    /// statIntervalMs=1000, timeWindow=10。
    /// 通过 <c>?fail=1</c> 开关**抛异常**（中间件捕获后计入熔断 errorCount）：请求数 ≥ 5 且
    /// 异常数 &gt; 3 时熔断 10s。注意：4xx/5xx 响应**不抛异常不计入**，必须 throw。
    /// 熔断后（或异常时）走 <see cref="ErrorCountBlocked"/>（blockHandler）/ <see cref="ErrorCountFallback"/> 降级，不再出现异常页。
    /// </summary>
    [SentinelResource("degrade:error-count", BlockHandler = nameof(ErrorCountBlocked), Fallback = nameof(ErrorCountFallback))]
    public IActionResult ErrorCount([FromQuery] bool fail)
    {
        ViewBag.Time = DateTime.Now.ToString("HH:mm:ss.fff");
        ViewBag.Mode = "异常数（grade=2）";
        ViewBag.Rule = "count=3, minRequestAmount=5";
        ViewBag.Tip = "带 ?fail=true 请求会抛异常；用并发工具同时刷 5+ 个请求，累计异常数 > 3 即熔断 10s（熔断后走降级页）。";

        if (fail)
        {
            throw new InvalidOperationException("模拟异常数熔断（?fail=true）");
        }

        return View();
    }

    // ---- 降级方法（镜像 Java fallback / blockHandler，须为静态方法，用 ContentResult 输出） ----

    /// <summary>
    /// 异常比例降级：业务抛异常（?fail=true）时由中间件调用，返回友好提示页（替代异常页）。
    /// 签名＝原动作参数 <c>bool fail</c>（可省略）＋可选末位 <see cref="Exception"/>。
    /// 标题用「业务异常已降级」与 blockHandler 的「熔断降级已触发」区分：fallback 不代表熔断器已打开。
    /// </summary>
    public static IActionResult ErrorRatioFallback(bool fail, Exception ex)
        => DegradeResult("异常比例熔断 · 降级（fallback）", $"业务抛异常 → Fallback 接管；已降级：{ex.Message}。熔断解除后可恢复正常访问。", "业务异常已降级（Fallback）");

    /// <summary>异常数降级：业务抛异常（?fail=true）时由中间件调用。</summary>
    public static IActionResult ErrorCountFallback(bool fail, Exception ex)
        => DegradeResult("异常数熔断 · 降级（fallback）", $"业务抛异常 → Fallback 接管；已降级：{ex.Message}。熔断解除后可恢复正常访问。", "业务异常已降级（Fallback）");

    /// <summary>
    /// 异常数熔断拦截降级：熔断器打开时由中间件调用（blockHandler），返回降级提示页。
    /// 签名＝原动作参数 <c>bool fail</c>（可省略）＋可选末位 <see cref="SentinelBlockException"/>。
    /// </summary>
    public static IActionResult ErrorCountBlocked(bool fail, SentinelBlockException ex)
        => DegradeResult("异常数熔断 · 熔断拦截（blockHandler）", $"熔断保护已触发（{ex.Resource}），请稍后再试。", "熔断降级已触发");

    /// <summary>生成降级提示页（静态方法无法访问 ViewBag/View，改用 ContentResult + 占位符替换）。
    /// 注意用 <see cref="string.Replace(string,string)"/> 而非 <c>string.Format</c>：模板内含 CSS 花括号
    /// （<c>{ font-family: ... }</c>），会被 <c>string.Format</c> 当成格式占位符解析而抛 FormatException。
    /// 状态码用 429（镜像 Java <c>DefaultBlockExceptionHandler</c>：熔断/限流/鉴权拦截统一 429 Too Many
    /// Requests）；fallback（业务异常降级）走此处同文案，统一 429 与内置降级页一致。</summary>
    private static IActionResult DegradeResult(string mode, string tip, string title) => new ContentResult
    {
        StatusCode = StatusCodes.Status429TooManyRequests,
        ContentType = "text/html; charset=utf-8",
        Content = DegradePageTemplate
            .Replace("{0}", mode)
            .Replace("{1}", tip)
            .Replace("{2}", DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Replace("{3}", title),
    };

    /// <summary>降级提示页 HTML 模板（{0}=模式 {1}=提示 {2}=时间 {3}=标题；静态方法不依赖 ViewBag）。</summary>
    private const string DegradePageTemplate =
        "<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\" />" +
        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />" +
        "<title>{3}</title><style>" +
        "body { font-family: -apple-system, \"PingFang SC\", \"Microsoft YaHei\", sans-serif; max-width: 640px; margin: 40px auto; line-height: 1.7; text-align: center; color: #333; }" +
        "h1 { color: #c0392b; } p { color: #666; } .meta { color: #999; font-size: 13px; } a { color: #2f6fed; }" +
        "</style></head><body>" +
        "<h1>{3}</h1>" +
        "<p>模式：<strong>{0}</strong></p>" +
        "<p class=\"meta\">{1}</p>" +
        "<p>当前时间：<strong>{2}</strong></p>" +
        "<p class=\"meta\">本页由 [SentinelResource] 的 Fallback / BlockHandler 静态方法返回（镜像 Java @SentinelResource 降级语义），不再显示异常页。</p>" +
        "<p><a href=\"javascript:location.reload()\">刷新重试</a>　<a href=\"/\">← 返回首页</a></p>" +
        "</body></html>";
}
