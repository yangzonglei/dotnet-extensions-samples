using Samples.Models;
using Yzl.Extensions.Cache.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;

namespace Yzl.Extensions.Samples.Cache.Feign;

/// <summary>
/// 【OpenFeign 远程客户端 —— 调用 Samples.Api】
///
/// 这是一个纯声明式的 HTTP 接口：没有实现类，由 OpenFeign 在运行时生成代理，
/// 把方法调用翻译成 HTTP 请求发往 <c>http://localhost:16600</c>。
///
/// ╔══════════════════════════════════════════════════════════════╗
/// ║ ✅ [Cacheable] 直接标在接口方法上即可生效                   ║
/// ║                                                            ║
/// ║   [Cacheable(cacheName: "feign:users", key: "#id")]        ║
/// ║   [Get("/api/test/users/{id}/slow")]                       ║
/// ║   Task<UserDto> GetByIdSlow([PathVariable("id")] long id);  ║
/// ║                                                            ║
/// ║ 与 Java/Spring 的写法一致，不需要再包一层 Service 类。      ║
/// ╚══════════════════════════════════════════════════════════════╝
///
/// 实现要点（框架侧）：
///   AddEnableCaching 会扫描<b>已注册进 DI 容器的接口服务</b>，把注解标在接口方法上的
///   接口再包一层缓存代理。命中缓存时只设置返回值、不调用 Proceed()，
///   因此向下的 OpenFeign 拦截器根本不会执行 —— HTTP 请求不会发出。
///
/// ⚠ 顺序要求：AddEnableCaching 必须最后调用。
///   AddBatchServices() → AddFeignStarter(...) → AddEnableCaching(...)
///   晚于 AddEnableCaching 注册的接口不会被包住（静默失效）。
///
/// 调用链：
///   CacheTestController → ICacheDemoFeignClient（本接口，缓存代理 + OpenFeign 代理）
///                       → Samples.Api 的 TestController.GetByIdSlow（睡 10 秒）
/// </summary>
[FeignClient(name: "cache-demo-api", url: "http://localhost:16600", timeout: 30000)]
public interface ICacheDemoFeignClient
{
    /// <summary>
    /// 远程查询用户（带缓存）—— 服务端会先睡 10 秒再返回。
    ///
    /// 刻意选 10 秒：缓存命中（≈0ms）与未命中（≈10000ms）在耗时上差异极大，
    /// 不需要看日志就能判断缓存到底有没有生效。
    ///
    /// timeout 说明：客户端级 timeout 设为 30000ms。
    ///   - [FeignClient] 默认超时是 3000ms，会被 10 秒的服务端响应直接打爆；
    ///   - 若沿用默认值，请求会在 3 秒时被 Polly 的 Timeout 策略取消。
    /// </summary>
    [Cacheable(cacheName: "feign:users", key: "#id", ttlSeconds: 60)]
    [Get("/api/test/users/{id}/slow")]
    Task<UserDto> GetByIdSlow([PathVariable("id")] long id);

    /// <summary>
    /// 远程 ping —— 用于在测试前确认 Samples.Api 确实在运行（瞬时返回，不走缓存）。
    ///
    /// 刻意不加 [Cacheable]：本方法每次都是真实远程调用，作为对照组。
    /// </summary>
    [Get("/api/test/ping")]
    Task<string> Ping();
}
