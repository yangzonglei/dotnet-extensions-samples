using Yzl.Extensions.Samples.OpenFeign.Fallbacks;
using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;

namespace Yzl.Extensions.Samples.OpenFeign.Acs;

/// <summary>
/// 降级工厂（<c>fallbackFactory</c>）演示接口。
///
/// 与 <see cref="IDemoFeignClient"/> 的唯一区别是：它声明的是 <c>fallbackFactory</c> 而不是 <c>fallback</c>，
/// 因此降级前能拿到<b>导致降级的异常</b>。
///
/// ⚠️ <c>fallback</c> 与 <c>fallbackFactory</c> 互斥（同时声明会在注册时抛 <c>InvalidOperationException</c>），
/// 所以工厂演示必须单独开一个接口，不能挂在 <see cref="IDemoFeignClient"/> 上。
///
/// 两个方法指向同一上游路径、同一客户端超时，用来对照验证
/// 「同步返回值」与「<c>Task&lt;string&gt;</c> 返回值」都会走工厂。
///
/// ⚠️ 客户端级与方法级都写 timeout：只有 runtime-proxy 形态读取方法级 timeout，
/// AOT 源生成器与 Net48 只读客户端级 timeout，两边都写才能保证三种形态行为一致。
/// </summary>
[FeignClient(name: "demo-api-factory", url: "{DemoApi:BaseUrl}",
    fallbackFactory: typeof(DemoFeignClientFallbackFactory), timeout: 2000)]
public interface IDemoFeignClientFactory
{
    [Get("/api/timeout", timeout: 2000)]
    string TimeoutWithFallback();

    [Get("/api/timeout", timeout: 2000)]
    Task<string> TimeoutWithFallbackAsync();
}
