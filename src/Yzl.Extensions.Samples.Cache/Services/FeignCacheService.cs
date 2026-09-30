using Samples.Models;
using Yzl.Extensions.Samples.Cache.Feign;

namespace Yzl.Extensions.Samples.Cache.Services;

/// <summary>
/// 【OpenFeign + Cacheable —— 远程调用结果缓存】
///
/// 第十一章：当「远程调用」遇到「缓存」。
///
/// ╔══════════════════════════════════════════════════════════════╗
/// ║ 本服务只是「纯转发」，本身<b>不</b>承载任何缓存注解：     ║
/// ║                                                            ║
/// ║   Controller  →  FeignCacheService  →  OpenFeign 接口      ║
/// ║                                        ↑ [Cacheable] 标在  ║
/// ║                                          接口方法上        ║
/// ║                                                            ║
/// ║ 注解写在 ICacheDemoFeignClient.GetByIdSlow 上即可生效，    ║
/// ║ 与 Java/Spring 的写法一致（见该接口的类注释）。            ║
/// ╚══════════════════════════════════════════════════════════════╝
///
/// 缓存为什么有价值：
///   OpenFeign 接口本身不做缓存（每次调用都是真实 HTTP 请求），
///   在它上面叠一层缓存，命中时连 HTTP 请求都不会发出 ——
///   服务端那 10 秒的 Task.Delay 自然也就省掉了。
///
/// 诊断口径的变化（重要）：
///   缓存层下沉到 Feign 接口后，<b>命中缓存时本类的方法体也会执行</b>
///   （缓存短路发生在本类之下的接口代理里），因此本类里的计数器
///   只能反映「请求穿过了几层」，不能再用来证明「没发出 HTTP」。
///   判断缓存是否命中请统一看 <c>elapsedMs</c>：
///   未命中 ≈ 10000ms，命中 ≈ 0ms。
/// </summary>
[IocService(lifetime: ServiceLifetime.Transient)]
public class FeignCacheService
{
    private readonly ICacheDemoFeignClient _feign;

    // 用 static：本服务注册为 Transient，每次请求都是一个新实例，
    // 实例字段的计数会随请求重置（永远是 0），看不出累计情况。
    private static int _callCount;

    /// <summary>
    /// 本服务方法体的累计执行次数。
    ///
    /// ⚠ 它不是「HTTP 请求次数」：缓存层在 <see cref="ICacheDemoFeignClient"/> 接口方法上，
    /// 命中时只拦截了更下层的 HTTP 调用，本方法体照常执行、计数照常 +1。
    /// 仅作为「请求确实进到了这一层」的链路探针使用。
    /// </summary>
    public int CallCount => _callCount;

    public FeignCacheService(ICacheDemoFeignClient feign)
    {
        _feign = feign;
    }

    /// <summary>
    /// 【11.1】远程查询用户（缓存由 <see cref="ICacheDemoFeignClient.GetByIdSlow"/> 上的注解提供）
    ///
    /// 完整过程：
    ///   首次调用：Feign 接口查出缓存未命中 → 发 HTTP → 服务端睡 10 秒 → 返回
    ///             → 结果写入缓存 feign:users:{id}（TTL 60 秒）
    ///   再次调用：Feign 接口查出缓存命中 → 直接返回缓存值，
    ///             向下的 HTTP 拦截器不执行、请求不发出 → elapsedMs ≈ 0
    ///
    /// TTL 设为 60 秒：足够连续刷新几次验证命中，
    /// 又不至于让「过期后重新走远程」等太久。
    /// </summary>
    public virtual async Task<UserDto?> GetUserViaFeignAsync(long id)
    {
        _callCount++;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = await _feign.GetByIdSlow(id);
        sw.Stop();

        Console.WriteLine($"[Feign+Cacheable] id={id} 返回，本层耗时 {sw.ElapsedMilliseconds}ms"
                          + (sw.ElapsedMilliseconds < 1000 ? "（缓存命中，未发出 HTTP）" : "（缓存未命中，已发出 HTTP）"));

        return user;
    }

    /// <summary>
    /// 【11.2】远程 ping（不走缓存）
    ///
    /// 对照组：同一个 OpenFeign 客户端上的「瞬时接口」。
    /// 用来先确认 Samples.Api 在运行 —— 如果连 ping 都失败，
    /// 那么 11.1 的耗时就不能用来判断缓存了。
    ///
    /// 该接口方法上没有 [Cacheable]，每次都是真实远程调用。
    /// </summary>
    public virtual async Task<string> RemotePingAsync()
    {
        _callCount++;
        return await _feign.Ping();
    }
}
