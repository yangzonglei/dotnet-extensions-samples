using Yzl.Extensions.Cache.Redis.Serialization;

namespace Yzl.Extensions.Samples.Cache.Managers;

/// <summary>
/// 【示例 8.5：同一项目里的第二个 Redis 后端】
///
/// 这就是「多 Redis 后端」的全部代码 —— <b>一行重写</b>，框架源码里没有任何一处提到本类型。
/// 注解上写 <c>cacheManager: typeof(CasAuthRedisManager)</c> 就选中了这个后端。
///
/// ╔══════════════════════════════════════════════════════════════════════╗
/// ║  与默认 RedisCacheManager 的两点差异                                ║
/// ║                                                                    ║
/// ║  1. ConnectionStringKey = "redis:cas-auth"                         ║
/// ║     → 换了一个配置键 = 换了一个 Redis 实例（也可以只是另一个 DB 索引，║
/// ║       本样例就是后者：cas-auth 的 defaultDatabase 是 12，          ║
/// ║       main-site 是 11）                                            ║
/// ║  2. Serializer = SystemTextJsonRedisCacheSerializer               ║
/// ║     → 与 main-site 的 Newtonsoft 产生不同文本的 JSON               ║
/// ╚══════════════════════════════════════════════════════════════════════╝
///
/// 对照 Java（<c>RedisConfig.java</c>）：
///   <c>@Bean("downloadTaskRedisTemplate")</c> / <c>@Bean("r2StringRedisTemplate")</c> ——
///   每个命名模板各带自己的一套连接参数与序列化器，注解上选名字。
///   .NET 这边的「名字」就是 <c>cacheManager</c> 上那个 <see cref="Type"/>。
///
/// ⚠️ 两个坑（写在这里免得下次踩）：
///   1. <b>继承即注册，本类型不需要任何注册代码</b>：不再依赖 <c>[IocService]</c> +
///      <c>AddBatchServices()</c>。v0.2.0 起 <c>AddEnableCaching(assemblies: ...)</c> 会扫描范围内的
///      所有具体 <see cref="Yzl.Extensions.Cache.ICacheManager"/> 实现并自动注册为单例，
///      <c>sealed</c> 子类也一样。（<c>sealed</c> 只在走 <c>[IocService]</c> 那条老路时会被
///      <c>!t.IsSealed</c> 的过滤器静默跳过 —— 那已经不是缓存管理器的推荐写法。）
///   2. <b>必须实现 <see cref="Yzl.Extensions.Cache.ICacheManager"/></b> —— 这里是继承基类，
///      天然满足；若改成手写实现，注意 <c>Provider</c> 的声明类型必须恰是
///      <c>ICacheProvider</c>（核心包的 <c>CacheInvokerCache</c> 依赖这一点）。
///
/// 另外：<c>DatabaseIndex</c> 没重写（= <c>null</c>），所以库索引沿用连接串里的
/// <c>defaultDatabase</c>。若想「同一个连接串 + 代码里指定库号」，重写它即可，
/// 代码里给了值就以代码为准（连接串的 defaultDatabase 被忽略）。
/// </summary>
public class CasAuthRedisManager(IRedisConnectionFactory connectionFactory)
    : RedisCacheManager(connectionFactory)
{
    /// <summary>
    /// 覆盖 <c>ConnectionStringKey</c>，换一个配置键 = 换一个后端。
    /// 样例里 <c>redis:cas-auth</c> 指向与 <c>redis:main-site</c> 同一个实例、
    /// 但 <c>defaultDatabase</c> 不同（12 vs 11），所以两者数据互不可见。
    /// </summary>
    protected override string ConnectionStringKey => "redis:cas-auth";

    /// <summary>
    /// 换用 System.Text.Json。同一个 <see cref="Models.UserDto"/> 在两个后端里的
    /// JSON 文本会不一样（浮点最直观：Newtonsoft 写 <c>1.0</c>，System.Text.Json 写 <c>1</c>），
    /// 这正是 8.5 用来证明「两个后端真的各走各的」的观察点。
    ///
    /// ⚠️ 所以<b>不要在同一个 cacheName 上换序列化器</b> —— 旧数据会读不回来，
    /// 而且失败方式是静默的（反序列化异常被吞成「未命中」）。
    /// </summary>
    protected override IRedisCacheSerializer? Serializer => new SystemTextJsonRedisCacheSerializer();
}
