using System.Text.Encodings.Web;
using System.Text.Json;
using Yzl.Extensions.Sentinel;
using Yzl.Extensions.Sentinel.Exceptions;
namespace Yzl.Extensions.Samples.Sentinel;

/// <summary>
/// 业务异常降级中间件（镜像 Java 全局异常处理对**普通业务异常**的接管）。
///
/// 与库内 <c>UseSentinelBlockExceptionHandler</c>（<see cref="SentinelBlockException"/> 响应中间件）
/// 的关系（镜像 Java <c>AbstractSentinelInterceptor</c> + 全局异常处理器，两层各司其职）：
/// - 被限流/熔断/鉴权拦截且未配置 blockHandler → SentinelMiddleware 抛
///   <see cref="SentinelBlockException"/>，由**库中间件**按异常子类自带 Response 规格接管
///   （页面渲染 RateLimit/Degrade 视图，API 输出 429 JSON）——本中间件不碰拦截异常
///   （catch filter <c>ex is not SentinelBlockException</c> 显式排除）。
/// - 本中间件只处理**普通业务异常**（/api 路径）→ 503 JSON 降级；非 /api 业务异常 →
///   继续抛给宿主 <c>UseExceptionHandler(/Home/Error)</c>。
/// - 熔断器对异常的计数在 <see cref="SentinelMiddleware"/> 的 <c>finally</c> 里
///   <c>entry.Exit(error)</c> 完成，本中间件不干预熔断状态。
///
/// ⚠️ **两条拦截异常接管路径互斥**：库中间件 <c>UseSentinelBlockExceptionHandler</c> 与宿主
/// 自己的全局异常处理是二选一——注册了库中间件时，<see cref="SentinelBlockException"/> 被它捕获、
/// 不再上抛（本中间件的 filter 即为此设计的）；**不注册**库中间件时，<see cref="SentinelBlockException"/>
/// 会照常抛到宿主，宿主必须自行按子类型（<see cref="SentinelFlowException"/> 限流 /
/// <see cref="SentinelDegradeException"/> 熔断 / <see cref="SentinelAuthorityException"/> 鉴权）接管。
/// 若宿主全局 handler 选择接管拦截异常，就应去掉本 filter 里的 <c>is not SentinelBlockException</c>
/// 排除（或增加对应分支），二者不可同时生效。
///
/// 分场景响应：
/// <list type="bullet">
/// <item>业务异常（/api）→ 503 + <c>Retry-After: 1</c>，message 提示已降级，返回 5xx 供调用方感知
///   上游异常（区别于限流 429）。</item>
/// <item><see cref="OperationCanceledException"/> / <see cref="TaskCanceledException"/> →
///   客户端断开，直接 499 语义（不可恢复，不降级）。</item>
/// </list>
/// 附带 <c>Retry-After</c> 与 <c>Cache-Control: no-store</c>：让客户端退避重试、不缓存降级响应。
/// </summary>
public sealed class ApiExceptionHandler(RequestDelegate next)
{
    private const string ApiPrefix = "/api";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is not SentinelBlockException
                                   && context.Request.Path.StartsWithSegments(ApiPrefix))
        {
            // 业务异常（/api 路径）→ 503 JSON 降级；非 /api 交给宿主 UseExceptionHandler(/Home/Error)
            // 拦截异常（SentinelBlockException）排除在外：由更外层的库内 UseSentinelBlockExceptionHandler 处理
            await WriteDegradeJsonAsync(context, ex);
        }
    }

    /// <summary>业务异常（/api）→ 503 JSON 降级响应（绝不二次抛给上层）。</summary>
    private static async Task WriteDegradeJsonAsync(HttpContext context, Exception exception)
    {
        var isClientCanceled = exception is OperationCanceledException or TaskCanceledException;

        // 客户端已断开：无意义再写响应，直接放行（避免写已断开的连接）
        if (isClientCanceled || context.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "1";
        context.Response.Headers.CacheControl = "no-store, no-cache";
        context.Response.ContentType = "application/json; charset=utf-8";

        // 镜像 degrade 响应结构：{ code, message }；用 UnsafeRelaxedJsonEscaping（不转义中文）保持 JSON 可读。
        var body = JsonSerializer.Serialize(
            new
            {
                code = 503,
                message = "服务已降级，请稍后重试",
                detail = exception.Message,
                time = DateTime.Now.ToString("HH:mm:ss.fff"),
            },
            new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
        await context.Response.WriteAsync(body);
    }
}
