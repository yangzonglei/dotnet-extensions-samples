using Yzl.Extensions.Cache.Managers;
using Yzl.Extensions.Samples.Cache.Managers;
using Yzl.Extensions.Samples.Cache.Models;

namespace Yzl.Extensions.Samples.Cache.Services;

/// <summary>
/// 【示例 8.6：Service 级默认后端 + 方法级覆盖】
///
/// 前面 8.1~8.5 都是<b>每个方法各自写 <c>cacheManager</c></b>。本示例把「选后端」这件事
/// 提到类上：类级 <c>[CacheConfig]</c> 指定一个自定义的 Redis 管理器（1 号库），
/// 方法上不写 <c>cacheManager</c> 就自动继承它；个别方法想换后端时再单独覆盖。
///
/// ╔══════════════════════════════════════════════════════════════════════╗
/// ║  三个方法，故意只差「写没写 cacheManager」                          ║
/// ║                                                                    ║
/// ║  方法                 cacheManager 参数        实际后端             ║
/// ║  ────────────────────────────────────────────────────────────────  ║
/// ║  A  GetDefaultBackend   <b>不写</b>                 继承类上的 Db1RedisManager ║
/// ║                                          → redis:main-site / <b>db 1</b> ║
/// ║  B  GetFromDb2          typeof(Db2RedisManager)   redis:main-site / db 2 ║
/// ║  C  GetFromMemory       typeof(MemoryCacheManager)  进程内存（无 Redis）║
/// ╚══════════════════════════════════════════════════════════════════════╝
///
/// <b>⚠️ 方法 A 「不标记 Cacheable」的准确含义</b>
///
/// 「不标记」指的是<b>不标记 <c>cacheManager</c> 这个参数</b>，不是「不加 <c>[Cacheable]</c>」。
/// 这两个写法差别很大，必须说清楚：
///
/// <code>
///   // ✅ 本样例的写法：加了 Cacheable，但不写 cacheManager
///   [Cacheable(key: "#id")]
///   public virtual UserDto? GetDefaultBackend(int id) { ... }
///   // → 代理生效，cacheManager 继承类级 [CacheConfig] 的 Db1RedisManager
///
///   // ❌ 完全不加注解：方法根本不被代理
///   public virtual UserDto? GetDefaultBackend(int id) { ... }
///   // → 没有缓存，类级 [CacheConfig] 对它完全无效
/// </code>
///
/// 原因是类级 <c>[CacheConfig]</c> 的合并发生在
/// <c>MethodMetadataCache.ResolveCacheConfig</c>，而它<b>只有在方法上存在
/// <c>Cacheable</c> / <c>CachePut</c> / <c>CacheEvict</c> 时才会被触达</b>；
/// 同时 <c>AddEnableCaching</c> 也只会为「含缓存注解方法的类」建代理。
/// 也就是说：<c>[CacheConfig]</c> 本身<b>不产生</b>缓存，它只提供默认值。
///
/// <b>方法 C 为什么必须显式写 <c>typeof(MemoryCacheManager)</c></b>：合并逻辑是
/// <c>cacheManager ??= configAttr.CacheManager</c> —— 不写就等于「继承类级值」，会落到
/// db 1，而不是内存。想回默认内存后端必须显式指名。
///
/// 同理，方法 A/B 的 <c>cacheName</c> 也不写，继承类上的 <c>svc:default</c>；
/// 方法 A 与 B 因此<b>同名不同库</b>：键一样（<c>svc:default:{id}</c>），但一个在 db 1、
/// 一个在 db 2，各自独立 —— 这是「换后端 = 换存储位置」最直观的一组证据。
///
/// 对照 Java：等价于类上 <c>@CacheConfig(cacheManager = "db1CacheManager")</c>，
/// 方法上用 <c>@Cacheable</c> 继承它、用 <c>@Cacheable(cacheManager = "db2CacheManager")</c> 覆盖它。
/// </summary>
[CacheConfig(defaultCacheName: "svc:default", cacheManager: typeof(Db1RedisManager))]
[IocService(lifetime: ServiceLifetime.Transient)]
public class DefaultBackendCacheService
{
    // ===================================================================
    // 模拟数据
    // ===================================================================

    /// <summary>
    /// 演示用的 id 区间下界（含）。
    ///
    /// 本样例刻意提供<b>一个区间</b>而不是固定两个 id：A/B 是 Redis 缓存（TTL 300 秒），
    /// 固定 id 会把上一轮的数据留在 db 1 / db 2 里，下次跑自动化脚本时
    /// 「首次未命中」会假性失败。有了区间，脚本就能用「时间戳取模」挑一个
    /// 本次运行专属的 id（见 <c>test.sh</c> 的 8.6 段）。
    /// </summary>
    public const int IdBase = 1000;

    /// <summary>演示用的 id 区间上界（含）。</summary>
    public const int IdUpper = 1999;

    /// <summary>
    /// 模拟一次数据库查询。区间内按 id 现场造一条记录，区间外返回 <c>null</c>。
    ///
    /// ⚠️ 区间外返回 null 是刻意保留的：v0.2.0 起框架<b>默认缓存 null</b>
    /// （对齐 Spring/Java，Redis 用 7 字节哨兵区分「存了 null」与「键不存在」），
    /// 所以区间外的 id 连续调两次，第二次同样<b>命中</b>（elapsedMs ≈ Redis 往返，不再打真实查询）。
    /// 想要退回「null 不写缓存」，用逐方法 opt-out <c>unless = "#result == null"</c>。
    /// 8.6 的自动化脚本之所以仍挑区间内的 id，是为了验证「真实值」的命中路径，
    /// 而不是因为空结果写不进去。
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
            Name = $"SvcUser{id}",
            Age = 20 + (id % 40),
            Email = $"svc{id}@example.com"
        };
    }

    /// <summary>三个方法共用一个计数器：谁真的执行了真实方法，一眼可见。</summary>
    private int _callCount;

    public int CallCount => _callCount;

    // ===================================================================
    // 8.6-A: 不写 cacheManager → 继承类级 [CacheConfig]（db 1）
    // ===================================================================

    /// <summary>
    /// 【8.6-A 方法级<b>不写</b> <c>cacheManager</c> → 用 Service（类级）的缓存】
    ///
    /// 本方法只写了 <c>key</c>，<b>没有</b> <c>cacheName</c>、<c>cacheManager</c>、<c>ttlSeconds</c>，
    /// 三者全部继承类上的 <c>[CacheConfig]</c>：
    ///
    /// <code>
    ///   cacheName    → "svc:default"（继承）
    ///   ttlSeconds   → 300（继承；类上没写，用特性的硬编码默认值）
    ///   cacheManager → Db1RedisManager（继承）→ redis:main-site / <b>db 1</b>
    /// </code>
    ///
    /// 最终缓存键：<c>svc:default:{id}</c>，落在 <b>db 1</b>。
    ///
    /// ⚠️ 演示时 id 要落在 <see cref="IdBase"/>~<see cref="IdUpper"/> 区间内，才能看出「真实值命中」。
    /// v0.2.0 起框架<b>默认缓存 null</b>（对齐 Spring/Java），传个区间外的 id（例如 <c>1</c>）
    /// 也会在第二次调用时命中 —— 空结果同样被写入 Redis（值是一枚 7 字节哨兵）。
    ///
    /// ⚠️ 继承不适用于 <c>key</c> —— key 是方法级 SpEL 表达式，
    /// <c>[CacheConfig]</c> 里没有对应字段，每个方法必须自己写。
    /// </summary>
    [Cacheable(key: "#id")]
    public virtual UserDto? GetDefaultBackend(int id)
    {
        _callCount++;
        Console.WriteLine($"[8.6-A] 执行查询：id={id}（cacheManager 继承 Db1RedisManager / db 1）");
        Thread.Sleep(1500);
        return Find(id);
    }

    // ===================================================================
    // 8.6-B: 方法级指定 db 2
    // ===================================================================

    /// <summary>
    /// 【8.6-B 方法级指定 <c>cacheManager</c> → <b>db 2</b>】
    ///
    /// 与 8.6-A 的唯一区别就是多了 <c>cacheManager: typeof(Db2RedisManager)</c>。
    /// <c>cacheName</c> / <c>ttlSeconds</c> 仍然继承类上的配置 —— 于是两个方法的
    /// <b>缓存键完全相同</b>（<c>svc:default:{id}</c>），只差一个库索引：
    ///
    /// <code>
    ///   redis-cli -n 1 KEYS 'svc:default*'   → A 写的值
    ///   redis-cli -n 2 KEYS 'svc:default*'   → B 写的值
    /// </code>
    ///
    /// 两条数据互不可见：A 命中不会让 B 也命中，反之亦然。这正是
    /// 「方法级 <c>cacheManager</c> 覆盖类级值」的直接证据 —— 如果覆盖没生效，
    /// B 会跟 A 撞在 db 1 的同一个键上，第二次调用就会出现「只睡 1500ms 一次」。
    /// </summary>
    [Cacheable(key: "#id", cacheManager: typeof(Db2RedisManager))]
    public virtual UserDto? GetFromDb2(int id)
    {
        _callCount++;
        Console.WriteLine($"[8.6-B] 执行查询：id={id}（cacheManager 覆盖为 Db2RedisManager / db 2）");
        Thread.Sleep(1500);
        return Find(id);
    }

    // ===================================================================
    // 8.6-C: 方法级切到内存缓存
    // ===================================================================

    /// <summary>
    /// 【8.6-C 方法级切到内存缓存 —— <b>必须显式写出来</b>】
    ///
    /// 这个方法和 A/B 的区别是「不想要 Redis 了」。⚠️ 注意<b>不能靠「不写 cacheManager」</b>
    /// 来表达这个意思：不写 = 继承类级值 = 仍然是 db 1 的 Redis。
    /// 合并逻辑是 <c>cacheManager ??= configAttr.CacheManager</c>，
    /// 「不写」与「要默认内存后端」是两件事，必须写 <c>typeof(MemoryCacheManager)</c> 才明确。
    ///
    /// <code>
    ///   cacheName    → "svc:memory"（覆盖；内存提供器与 Redis 用不同区域，便于对照）
    ///   ttlSeconds   → 300（继承类级）
    ///   cacheManager → MemoryCacheManager（覆盖，进程内）
    /// </code>
    ///
    /// 对照结果：A/B 命中时仍有一次 Redis 网络往返（约 1~3ms；跨机器时到 50~150ms），
    /// 本方法命中是真正的 0ms 量级，且服务重启即失效（多实例不共享）。
    ///
    /// 顺带说明一个易踩的坑：本方法<b>不必</b>为了「用内存」而把整个类从
    /// <c>[CacheConfig]</c> 上摘下来 —— 类级只是默认值，方法级能逐字段覆盖。
    /// </summary>
    [Cacheable(cacheName: "svc:memory", key: "#id", cacheManager: typeof(MemoryCacheManager))]
    public virtual UserDto? GetFromMemory(int id)
    {
        _callCount++;
        Console.WriteLine($"[8.6-C] 执行查询：id={id}（cacheManager 覆盖为 MemoryCacheManager / 进程内）");
        Thread.Sleep(1500);
        return Find(id);
    }
}
