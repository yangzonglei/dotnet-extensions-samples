using System.Threading.Tasks;
using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;

namespace Yzl.Extensions.Samples.OpenFeign.Net48.Clients
{
    /// <summary>
    /// 降级工厂（fallbackFactory）演示接口 —— .NET Framework 4.8 形态。
    ///
    /// 与 IDemoFeignClient 的唯一区别是：它声明的是 fallbackFactory 而不是 fallback，
    /// 因此降级前能拿到「导致降级的异常」。
    ///
    /// ⚠️ fallback 与 fallbackFactory 互斥（同时声明会在注册时抛 InvalidOperationException），
    /// 所以工厂演示必须单独开一个接口。
    ///
    /// ⚠️ 同 IDemoFeignClient：Net48 只读客户端级 timeout，故此处设 2s（&lt; 上游 3s）。
    /// </summary>
    [FeignClient(name: "demo-api-factory", url: "http://localhost:16600",
        fallbackFactory: typeof(Fallbacks.DemoFeignClientFallbackFactory), timeout: 2000)]
    public interface IDemoFeignClientFactory
    {
        [Get("/api/timeout", timeout: 2000)]
        string TimeoutWithFallback();

        [Get("/api/timeout", timeout: 2000)]
        Task<string> TimeoutWithFallbackAsync();
    }
}
