// ══════════════════════════════════════════════════════════════════
// Yzl.Extensions.Sentinel 综合测试入口
//
// 演示内容：
//   - AddSentinel()：限流（flow）+ 熔断（degrade）+ Dashboard 心跳 + 命令中心
//   - Nacos 规则数据源：引用 Yzl.Extensions.Sentinel.DataSource.Nacos 包即自动发现（零代码），
//     仅需在 appsettings 配置 spring.cloud.sentinel.datasource.{name}.nacos 即激活；未配置则回退 spring.cloud.sentinel.rules
//   - UseSentinel() 中间件：路由后、业务前按 URL 推导资源 key（对标 Java CommonFilter）
//   - 资源 key = 显式路由模板（如 /api/ratelimit）或请求路径（如 /Home/RateLimitDemo）；无端点请求跳过
//   - spring.cloud.sentinel.filter.excludeUrlPatterns 过滤系统请求（首页 / favicon 等，不建节点、不入调用链路）
//   - 启动预注册全部 Controller 路由 → Dashboard 调用链路零流量也见完整路由清单
//   - 429 响应：拦截异常由库内 UseSentinelBlockExceptionHandler 中间件接管
//     （镜像 Java 全局异常处理器按 BlockException 子类型分场景渲染）：浏览器（text/html）
//     → 按异常子类自带 Response 的 ViewName 渲染宿主 Razor 视图（默认 RateLimit/Degrade）；
//     API（JSON）→ 按 JsonTemplate 输出 429 JSON（默认结构硬编码）
//   - 429 响应可用 UseSentinelBlockExceptionHandler(options => …) 配置参数按场景（Flow 限流 /
//     Degrade 降级·鉴权）定制：视图模板（ViewName）、页面 HTML 模板（HtmlTemplate）、
//     API JSON 结构（JsonTemplate）、文案（Message）、状态码（StatusCode）——配置参数优先，
//     未设置的字段沿用异常子类自带 Response（含默认值）
//   - 熔断降级 API（无 Fallback）：业务异常 → 全局异常处理 503 JSON；熔断打开 → 429 JSON
//
// 访问：
//   http://localhost:16609/           首页（系统页，被 filter 排除）
//   http://localhost:16609/Home/RateLimitDemo   QPS 限流演示（规则 /Home/RateLimitDemo，2 QPS）
//   http://localhost:16609/Home/Slow            慢请求熔断演示（规则 /Home/Slow，慢比例 50%）
//   http://localhost:16609/api/ratelimit        API 限流演示（规则 /api/ratelimit，1 QPS，JSON 429）
//   浏览器快速刷新上述页面即可触发 429（浏览器请求命中 rate-limit 时渲染 Views/Shared/RateLimit.cshtml）
//
// 对接 Dashboard：
//   spring:cloud:sentinel:transport:dashboard 配置为 Sentinel Dashboard 地址（如 127.0.0.1:8858）后，
//   启动本应用即每 10s 向 Dashboard 上报心跳；Dashboard 可在"机器列表"看到本机，
//   并通过命令中心（默认端口 8719）反向拉取/推送规则。
//
// Nacos 规则持久化：
//   spring.cloud.sentinel.datasource.{name}.nacos 配置后，规则从 Nacos 加载（权威源）；
//   Dashboard 修改规则会异步写回对应 data-id（不实时、重启不丢）。
// ══════════════════════════════════════════════════════════════════

using Prometheus;
using Yzl.Extensions.Sentinel;
using Yzl.Extensions.Sentinel.Configuration;
using Yzl.Extensions.Samples.Sentinel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// ===== Yzl.Extensions.Sentinel =====
// 配置节 spring:cloud:sentinel（enabled / eager / transport / datasource / rules）来自 appsettings.json；
// 429 拦截响应不再配置（spring.cloud.sentinel.response 已移除），由全局异常处理 ApiExceptionHandler 硬编码接管。
// 数据源提供者（如 Nacos）由 AddSentinel 自动发现引用程序集注册，无需单独调用。
builder.Services.AddSentinel(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // /Home/Error 没有视图，UseExceptionHandler 重执行后必然 404；AllowStatusCode404Response=true
    // 让异常处理中间件把 404 原样返回而不是抛"handler 产生 404"的包装异常掩盖真实错误。
    app.UseExceptionHandler(new ExceptionHandlerOptions
    {
        ExceptionHandlingPath = "/Home/Error",
        AllowStatusCode404Response = true,
    });
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// ===== Sentinel 限流/熔断中间件：必须在 UseRouting 之后、进入业务管线之前 =====
// 注册顺序（先注册→在外层）：UseSentinelBlockExceptionHandler 最外层 → ApiExceptionHandler →
// UseSentinel 最内层。
// - SentinelMiddleware 被限流/熔断/鉴权拦截且未配置 BlockHandler 时抛出 SentinelBlockException，
//   先穿过 ApiExceptionHandler（其 filter 排除该异常）→ 由最外层库内
//   UseSentinelBlockExceptionHandler 按异常子类自带 Response 规格接管（页面渲染视图 / API 输出 429 JSON），
//   异常被捕获、不再上抛。
// - 普通业务异常不被库中间件捕获：从业务代码抛出 → 先被 SentinelMiddleware catch（熔断计数、
//   fallback 处理都在这一步，保证熔断器正常打开）→ 再抛给 ApiExceptionHandler：/api 路径走 503 JSON
//   降级，非 /api 走 UseExceptionHandler(/Home/Error) 页面。两层职责完全分离。
app.UseSentinelBlockExceptionHandler(options =>
{
    // 限流（Flow）：页面仍渲染宿主 RateLimit 视图（@Model.Message 显示定制文案）；
    // API 返回自定义 JSON 结构（占位符 {code} {message} {resource} {detail} {time}）
    options.Flow = new SentinelBlockResponseOptions
    {
        Message = "访问过于频繁，请稍后再试",
        JsonTemplate = """{"code":{code},"msg":"{message}","resource":"{resource}","time":"{time}"}""",
        ViewName = "RateLimit1",
    };

    // 降级/鉴权（Degrade）：API 返回自定义 JSON；页面配置自定义 HTML 模板
    // （视图缺失/渲染失败或 ViewName="" 时输出；仅当存在页面降级端点时才会触发）
    options.Degrade = new SentinelBlockResponseOptions
    {
        Message = "服务暂不可用（已触发熔断），请稍后再试",
        HtmlTemplate =
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>服务暂不可用</title></head>" +
            "<body style=\"text-align:center;margin-top:15%\">" +
            "<h1 style=\"font-size:28px;color:#c0392b\">服务暂不可用（已触发熔断保护）</h1>" +
            "<p style=\"color:#999\">请稍后刷新重试</p></body></html>",
        JsonTemplate = """{"code":{code},"msg":"{message}","time":"{time}"}""",
    };
});

app.UseMiddleware<ApiExceptionHandler>();
app.UseSentinel();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.UseHttpMetrics(options => options.RequestDuration.Enabled = false);

app.MapMetrics("/actuator/prometheus");

app.Run();
