using Yzl.Extensions.Cache.Managers;
using Yzl.Extensions.Samples.Cache.Managers;
using Yzl.Extensions.Samples.Cache.Models;

namespace Yzl.Extensions.Samples.Cache.Services;

/// <summary>
/// 【示例 10.1 / 10.2：两条连接路 —— 配置给的 vs 用户注入的】
///
/// 这个 Service 只做一件事：把「同一个查询」分别走两条<b>不同的连接获取路径</b>，
/// 好让你直接对比它们的落点。两条路对应 Java 的两种写法：
///
/// ╔══════════════════════════════════════════════════════════════════════╗
/// ║  方法              cacheManager                      连接怎么来的    ║
/// ║  ────────────────────────────────────────────────────────────────    ║
/// ║  A  GetFromConfig    ConfigBackendRedisManager         ①<b>配置</b>：        ║
/// ║                      （一行子类，只换连接名）           spring:data:redis ║
/// ║                                                        → db <b>5</b>          ║
/// ║                                                                      ║
/// ║  B  GetFromInjected  InjectedConnectionCacheManager    ②<b>用户注入</b>：    ║
/// ║                      （<b>不继承基类</b>，手写 ICacheManager）  IRedisConnection ║
/// ║                                                        Source → db <b>8</b>   ║
/// ╚══════════════════════════════════════════════════════════════════════╝
///
/// <b>⚔️ 为什么这就是「用户连接优先」最硬的证据</b>
///
/// <c>appsettings.json</c> 里<b>两个连接名都配了</b>（<c>spring:data:redis</c> 写 <c>database: 5</c>；
/// <c>redis:samples-injected</c> 的扁平键写 <c>defaultDatabase=7</c>），
/// 但方法 B 在 <c>RedisConnectionProvider.Create()</c> 的第 ① 步就被
/// <see cref="SamplesInjectedConnectionSource"/> 认领，<b>压根不会去读配置</b>。
/// 于是：
///
/// <code>
///   A 落 db 5  ← 与 spring:data:redis 的 database=5 一致   （配置说了算）
///   B 落 db 8  ← 扁平键写的是 7，实际是 8                  （用户连接说了算，配置被跳过）
/// </code>
///
/// 核对方式：
/// <code>
///   redis-cli -p 6399 -n 5 KEYS 'conn:config*'     → 有值（A）
///   redis-cli -p 6399 -n 7 KEYS 'conn:injected*'   → <b>空</b>（配置被跳过）
///   redis-cli -p 6399 -n 8 KEYS 'conn:injected*'   → 有值（B，用户连接生效）
/// </code>
///
/// <b>⚔️ 两条连接名都必须真的存在，否则失败方式是「静默不命中」而不是报错</b>
///
/// 连接名两级查找全不命中时抛 <c>InvalidOperationException</c>，但读路径的这个异常
/// 会被 <c>CacheableHandler.TryReadCache</c> 的 <c>catch (Exception)</c> 吞掉、
/// 写路径被 <c>CacheOperationHelper.WriteCache</c> 的同类 <c>catch</c> 吞掉
/// ⇒ 表现是「缓存永远不命中、每次都睡 1500ms」，<b>接口不返回 500</b>。
/// 所以判断「我这条连接到底通没通」看的是 <c>elapsedMs</c>，不是 HTTP 状态码。
///
/// <b>⚔️ 两个方法各自的 cacheName 都独占一段前缀</b>
///
/// 方法 B 用的是 <c>SystemTextJsonRedisCacheSerializer</c>（非默认），产出的字节与
/// 默认的 Newtonsoft 实现不同。同字节流才可互相读回，所以它必须独占 cacheName
/// —— 详见 <see cref="InjectedConnectionCacheManager"/> 的说明。
/// </summary>
[IocService(lifetime: ServiceLifetime.Transient)]
public class CacheConnectionService
{
    // ===================================================================
    // 模拟数据
    // ===================================================================

    /// <summary>
    /// 演示用的 id 区间下界（含）。
    ///
    /// 与 8.6 的 <c>1000~1999</c> <b>刻意错开</b>：8.6 的 id 会留在 db 1 / db 2，
    /// 本章的 id 留在 db 5 / db 8；区间分开之后，自动化脚本可以各自独立挑
    /// 「本次运行专属」的 id，互不干扰 TTL 300 秒内的残留数据。
    /// </summary>
    public const int IdBase = 2000;

    /// <summary>演示用的 id 区间上界（含）。</summary>
    public const int IdUpper = 2999;

    /// <summary>
    /// 模拟一次数据库查询。区间内现场造一条记录，区间外返回 <c>null</c>。
    ///
    /// ⚠️ 骨架与 8.6 的 <c>Find</c> 完全一致（含「区间外返回 null」）：
    /// 框架默认缓存 null（对齐 Spring/Java，Redis 用 7 字节哨兵区分
    /// 「存了 null」与「键不存在」），所以区间外的 id 第二次同样命中。
    /// 本章的 id 之所以仍取区间内，是为了验证<b>真实值</b>的命中路径。
    /// </summary>
    private static UserDto? Find(int id)
    {
        if (id < IdBase || id > IdUpper)
        {
            return null;
        }
        return new UserDto
        {
            Id = id,
            Name = $"ConnUser{id}",
            Age = 20 + (id % 40),
            Email = $"conn{id}@example.com"
        };
    }

    /// <summary>两个方法共用一个计数器：谁真的执行了真实方法，一眼可见。</summary>
    private int _callCount;

    public int CallCount => _callCount;

    // ===================================================================
    // 10.1: 连接来自配置节（一行子类）
    // ===================================================================

    /// <summary>
    /// 【10.1 连接来自 <c>spring:data:redis</c> —— Java 的「改 <c>spring.data.redis.*</c>」】
    ///
    /// 这里<b>没有</b>任何连接参数：参数全在 <c>appsettings.json</c> 的
    /// <c>spring:data:redis</c> 节里（host/port/database/timeout/connect-timeout/
    /// client-name，节点形状与 Java 的 <c>RedisProperties</c> 一致）。改配置就能换实例、换库、调超时，
    /// 代码一行不改 —— 这正是「需不需要重新编译」这个问题的答案。
    ///
    /// 落在 <b>db 5</b>，<c>CLIENT LIST</c> 里显示 <c>samples-cache-config-backend</c>。
    /// </summary>
    [Cacheable(cacheName: "conn:config:users", key: "#id", ttlSeconds: 300,
        cacheManager: typeof(ConfigBackendRedisManager))]
    public virtual UserDto? GetFromConfig(int id)
    {
        _callCount++;
        Console.WriteLine($"[10.1] 执行查询：id={id}（连接名 redis:samples-config → 命中 spring:data:redis → 实际 db {ConfigBackendRedisManager.ConfiguredDatabase}）");
        Thread.Sleep(1500);
        return Find(id);
    }

    // ===================================================================
    // 10.2: 连接由用户注入（不继承基类的手写 ICacheManager）
    // ===================================================================

    /// <summary>
    /// 【10.2 连接由用户自己给 —— Java 的「自己声明一个 <c>@Bean RedisConnectionFactory</c>」】
    ///
    /// 连接是 <see cref="SamplesInjectedConnectionSource"/> 自己建的（参数写死在那一侧，
    /// 完全不受配置节约束）。本方法的 <c>cacheManager</c> 是一个<b>不继承
    /// <c>RedisCacheManager</c></b> 的手写实现（<see cref="InjectedConnectionCacheManager"/>），
    /// 用来证明「实现接口」与「继承父类」这两条路框架都收。
    ///
    /// 落在 <b>db 8</b> —— 而配置里对同一个连接名的扁平键写的是 <c>defaultDatabase=7</c>。
    /// 这个<b>不一致是刻意的</b>：它是「用户连接优先于配置」的直接证据。
    /// </summary>
    [Cacheable(cacheName: "conn:injected:users", key: "#id", ttlSeconds: 300,
        cacheManager: typeof(InjectedConnectionCacheManager))]
    public virtual UserDto? GetFromInjected(int id)
    {
        _callCount++;
        Console.WriteLine($"[10.2] 执行查询：id={id}（走用户注入连接 → 实际 db {SamplesInjectedConnectionSource.DatabaseIndex}，配置里的 7 被跳过）");
        Thread.Sleep(1500);
        return Find(id);
    }
}
