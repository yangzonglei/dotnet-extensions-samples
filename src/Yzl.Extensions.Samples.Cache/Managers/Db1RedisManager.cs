namespace Yzl.Extensions.Samples.Cache.Managers;

/// <summary>
/// 【示例 8.6：Service 级默认后端 —— 同一个连接串、代码里指定 1 号库】
///
/// 这就是「自定义 Redis 缓存管理器」的全部代码 —— <b>一行重写</b>。
/// 与 <see cref="CasAuthRedisManager"/> 的区别是重写的重写点不同：
///
/// ╔══════════════════════════════════════════════════════════════════════╗
/// ║  四种「自定义后端」的重写方式（对照着看）                            ║
/// ║                                                                    ║
/// ║  CasAuthRedisManager（8.5）   ConnectionStringKey + Serializer      ║
/// ║                               → 换一个配置键 = 换一个 Redis 实例    ║
/// ║  ConfigBackendRedisManager    ConnectionStringKey                   ║
/// ║    （第十章 10.1）            → 连接名指向 spring:data:redis 里的   ║
/// ║                                 嵌套节，不写一行连接串             ║
/// ║  Db1RedisManager（8.6-A）     DatabaseIndex = 1                     ║
/// ║  Db2RedisManager（8.6-B）     DatabaseIndex = 2                     ║
/// ║                               → 沿用同一个连接串，只换库索引        ║
/// ╚══════════════════════════════════════════════════════════════════════╝
///
/// 为什么用「连接串 + 代码指定库索引」而不是「再写一个连接串」：
///
///   <c>redis:main-site</c> 的 <c>defaultDatabase</c> 是 11。重写 <see cref="DatabaseIndex"/>
///   之后，<b>连接串里的 defaultDatabase 被忽略，以代码里的值为准</b> —— 所以本管理器
///   实际落在 db 1，而不是 db 11。这不是「连接串写错了」，是框架文档明确规定的优先级。
///   好处是：同一个 <see cref="StackExchange.Redis.ConnectionMultiplexer"/> 被三个管理器
///   （<c>RedisCacheManager</c> / <c>Db1RedisManager</c> / <c>Db2RedisManager</c>）共享，
///   不重复建连，只是各自 <c>GetDatabase(n)</c> 取不同的库。
///
/// 等价于 Java 的 <c>RedisConfig.java</c> 里两个 <c>@Bean</c> 模板指向同一个
/// <c>RedisConnectionFactory</c>、但 <c>setDatabase(1)</c> / <c>setDatabase(2)</c> 不同。
///
/// ⚠️ 与所有 Redis 后端一样：<b>未配置 <c>redis:main-site</c> 时不会 500</b> ——
/// 首次触达连接的异常会被读/写两条缓存路径吞掉，表现为「缓存永远不命中」
/// （<c>elapsedMs</c> 恒为方法体本身的 ~1500），而不是「缓存被禁用」。
/// 注意与「配置写了但 Redis 不可达」区分：后者还要先等一段
/// <c>connect-timeout × connect-retry</c> 的重试时间（实测 4~8 秒）才走到方法体。
/// </summary>
public class Db1RedisManager(IRedisConnectionFactory connectionFactory)
    : RedisCacheManager(connectionFactory)
{
    /// <summary>
    /// 1 号库。样例里 <c>redis:main-site</c> 的 <c>defaultDatabase</c> 是 11，
    /// 代码里给了值就以代码为准 —— 所以数据落在 db 1，可用
    /// <c>redis-cli -n 1 KEYS 'svc:*'</c> 核对。
    /// </summary>
    protected override int? DatabaseIndex => 1;
}
