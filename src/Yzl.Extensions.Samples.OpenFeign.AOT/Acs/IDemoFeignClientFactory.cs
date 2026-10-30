using Yzl.Extensions.Samples.OpenFeign.AOT.Acs.FallbackHandler;
using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;

namespace Yzl.Extensions.Samples.OpenFeign.AOT.Acs;

/// <summary>
/// 降级工厂（<c>fallbackFactory</c>）演示接口 —— AOT（源生成器）形态。
///
/// 与 <see cref="IDemoFeignClient"/> 的唯一区别是：它声明的是 <c>fallbackFactory</c> 而不是 <c>fallback</c>，
/// 因此降级前能拿到<b>导致降级的异常</b>。
///
/// ⚠️ <c>fallback</c> 与 <c>fallbackFactory</c> 互斥（同时声明会在注册时抛 <c>InvalidOperationException</c>），
/// 所以工厂演示必须单独开一个接口。
///
/// AOT 侧由 <c>AddGeneratedFeignClients()</c> 生成代理
/// （含 <c>_fallbackFactory</c> 字段与 <c>Create(ex)</c> 分支），
/// 并自动注册 <c>IFeignFallbackFactory&lt;IDemoFeignClientFactory&gt;</c> → <c>DemoFeignClientFallbackFactory</c>
/// 的接口映射 —— 代理正是按这个接口解析工厂的，样例侧无需手写 DI 代码。
///
/// ⚠️ 同 <see cref="IDemoFeignClient"/>：AOT 源生成器只读客户端级 timeout，
/// 客户端级 2s &lt; 上游 3s，降级才会触发。
/// </summary>
[FeignClient(name: "demo-api-aot-factory", url: "{DemoApi:BaseUrl}",
    fallbackFactory: typeof(DemoFeignClientFallbackFactory), timeout: 2000)]
public interface IDemoFeignClientFactory
{
    [Get("/api/timeout", timeout: 2000)]
    string TimeoutWithFallback();

    [Get("/api/timeout", timeout: 2000)]
    Task<string> TimeoutWithFallbackAsync();
}
