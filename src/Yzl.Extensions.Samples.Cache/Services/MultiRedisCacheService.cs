using Yzl.Extensions.Samples.Cache.Managers;
using Yzl.Extensions.Samples.Cache.Models;

namespace Yzl.Extensions.Samples.Cache.Services;

/// <summary>
/// 【示例 8.5：一个项目里同时使用两个 Redis 后端】
///
/// 下面两个方法<b>只有 <c>cacheManager</c> 一个参数不同</b> —— 同一个 <c>id</c>、
/// 同样 1500ms 的模拟查询、同样的 TTL，但数据落在两个不同的 Redis 库里，
/// 用两套不同的序列化器：
///
/// <code>
///                          cacheManager                        连接串 / 库索引   序列化器
///   GetUserFromMainSite    typeof(RedisCacheManager)           redis:main-site / 11   Newtonsoft
///   GetUserFromCasAuth     typeof(CasAuthRedisManager)         redis:cas-auth  / 12   System.Text.Json
/// </code>
///
/// 这就是「一个项目里使用多个 Redis 配置」的完整用法：后端 = 一个继承
/// <see cref="RedisCacheManager"/> 的子类，选哪个后端 = 注解上写哪个 <c>typeof</c>。
/// 框架源码不需要改，也不需要在 <c>Program.cs</c> 里注册任何东西 ——
/// <b>继承即注册</b>：<c>AddEnableCaching(assemblies: ...)</c> 会自动发现并注册扫描范围内
/// 所有具体的 <c>ICacheManager</c> 实现（<see cref="CasAuthRedisManager"/> 就是其中之一，
/// 它连 <c>[IocService]</c> 都不需要标）。
///
/// 对照 Java：等价于在 <c>RedisConfig.java</c> 里声明两个 <c>@Bean</c> 模板，
/// 注入时用 <c>@Qualifier("downloadTaskRedisTemplate")</c> 选一个。
///
/// ⚠️ 未配置对应连接串时，<b>不会 500</b> —— 首次触达连接抛的异常会被读/写两条
/// 缓存路径吞掉，表现为「缓存永远不命中」（每次调用都执行真实方法）。
/// 换句话说：<c>elapsedMs</c> 一直是 1500 左右就说明那个后端的配置没生效，
/// 而不是「缓存被禁用」。
/// </summary>
[IocService(lifetime: ServiceLifetime.Transient)]
public class MultiRedisCacheService
{
    private static readonly List<UserDto> _users = new()
    {
        new() { Id = 1, Name = "MultiAlice", Age = 25, Email = "multi@example.com" },
        new() { Id = 2, Name = "MultiBob", Age = 30, Email = "multi_bob@example.com" },
    };

    private static readonly List<MultiBackendSampleDto> _samples = new()
    {
        new() { Id = 1, Name = "ScoreSample", Score = 1.0 },
        new() { Id = 2, Name = "ScoreSample2", Score = 2.5 },
    };

    /// <summary>两个后端共用一个计数器：谁真的执行了真实方法，一眼可见。</summary>
    private int _callCount;

    public int CallCount => _callCount;

    // ===================================================================
    // 8.5-A: 默认后端（redis:main-site / db 11 / Newtonsoft）
    // ===================================================================

    /// <summary>
    /// 【8.5-A 默认 Redis 后端】
    ///
    /// 与 8.1 用同一个管理器类型，只是换了个 <c>cacheName</c>（<c>multi:main</c>），
    /// 以便在 redis-cli 里一眼区分出这组对照数据。
    /// </summary>
    [Cacheable(cacheName: "multi:main", key: "#id", ttlSeconds: 300, cacheManager: typeof(RedisCacheManager))]
    public virtual UserDto? GetUserFromMainSite(int id)
    {
        _callCount++;
        Console.WriteLine($"[多后端/主站] 执行查询：id={id}（redis:main-site / db 11 / Newtonsoft）");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 8.5-B: 第二个后端（redis:cas-auth / db 12 / System.Text.Json）
    // ===================================================================

    /// <summary>
    /// 【8.5-B 自定义 Redis 后端 —— 只改了一个 <c>cacheManager</c>】
    ///
    /// <see cref="CasAuthRedisManager"/> 覆盖了 <c>ConnectionStringKey</c> 与
    /// <c>Serializer</c>，于是这个方法的数据落到另一个库、用另一套 JSON 引擎写。
    ///
    /// 验证方式（见 TestManual 8.5）：
    /// <code>
    ///   redis-cli -n 11 KEYS 'multi:main*'     → 有值，Newtonsoft 格式
    ///   redis-cli -n 12 KEYS 'multi:cas*'      → 有值，System.Text.Json 格式
    /// </code>
    /// </summary>
    [Cacheable(cacheName: "multi:cas", key: "#id", ttlSeconds: 300,
               cacheManager: typeof(CasAuthRedisManager))]
    public virtual UserDto? GetUserFromCasAuth(int id)
    {
        _callCount++;
        Console.WriteLine($"[多后端/cas-auth] 执行查询：id={id}（redis:cas-auth / db 12 / System.Text.Json）");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 8.5-C / 8.5-D: 让「两套序列化器」肉眼可见
    // ===================================================================

    /// <summary>
    /// 【8.5-C 同款样本，走默认后端（db 11 / Newtonsoft）】
    ///
    /// 返回类型是 <see cref="MultiBackendSampleDto"/> —— 一个<b>复杂类型</b>，
    /// 因此它会走序列化器（标量如 <c>double</c> 是绕过序列化器的裸值，
    /// 拿标量做对照会看不到任何差异）。
    ///
    /// 这个方法与 <see cref="GetSampleFromCasAuth"/> 的<b>唯一区别也是 <c>cacheManager</c></b>，
    /// 两者的 redis-cli 输出里 <c>Score</c> 的写法不同：本方法写 <c>1.0</c>。
    /// </summary>
    [Cacheable(cacheName: "multi:sample", key: "#id", ttlSeconds: 300, cacheManager: typeof(RedisCacheManager))]
    public virtual MultiBackendSampleDto? GetSampleFromMainSite(int id)
    {
        _callCount++;
        Console.WriteLine($"[多后端/主站样本] 执行查询：id={id}（Newtonsoft 写 Score=1.0）");
        Thread.Sleep(1500);
        return _samples.FirstOrDefault(s => s.Id == id);
    }

    /// <summary>
    /// 【8.5-D 同款样本，走自定义后端（db 12 / System.Text.Json）】
    ///
    /// 与 8.5-C 缓存的是<b>同一个类型、同一个 id</b>，但序列化器不同 ⇒
    /// redis-cli 里 <c>Score</c> 写作 <c>1</c>（而不是 <c>1.0</c>）。
    ///
    /// <code>
    ///   redis-cli -n 11 GET multi:sample:1      → ..."Score":1.0}
    ///   redis-cli -n 12 GET multi:cas-sample:1  → ..."Score":1}
    /// </code>
    ///
    /// 这就是「按后端选序列化器」真的生效的直接证据。也正因为字节不同，
    /// 两个后端<b>必须用不同的 cacheName</b>（这里是 <c>multi:cas-sample</c>）——
    /// 同名换序列化器会让旧数据静默读不回来。
    /// </summary>
    [Cacheable(cacheName: "multi:cas-sample", key: "#id", ttlSeconds: 300,
               cacheManager: typeof(CasAuthRedisManager))]
    public virtual MultiBackendSampleDto? GetSampleFromCasAuth(int id)
    {
        _callCount++;
        Console.WriteLine($"[多后端/cas-auth 样本] 执行查询：id={id}（System.Text.Json 写 Score=1）");
        Thread.Sleep(1500);
        return _samples.FirstOrDefault(s => s.Id == id);
    }
}
