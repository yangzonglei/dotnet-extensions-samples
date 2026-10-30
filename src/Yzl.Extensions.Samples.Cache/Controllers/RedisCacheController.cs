using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Samples.Cache.Models;
using Yzl.Extensions.Samples.Cache.Services;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Cache.Controllers;

/// <summary>
/// 第八章：Redis 缓存
///
/// 8.1~8.4 演示默认后端（redis:main-site / db 11 / Newtonsoft）。
/// 8.5 演示「一个项目里同时挂两个 Redis 后端」——同一个 id 分别落到 db 11 与 db 12，
/// 且两边的 JSON 由不同序列化器产生。
/// 8.6 演示「Service 级默认后端 + 方法级覆盖」——类级 [CacheConfig] 指定自定义的
/// db 1 管理器，方法 A 继承它、B 覆盖为 db 2、C 覆盖为内存。
/// </summary>
[ApiController]
[TestDashboardInfo("📖 第八章：Redis 缓存", Order = 8, Badge = "8")]
[Route("api/samples")]
public class RedisCacheController : ControllerBase
{
    private readonly RedisCacheService _redis;
    private readonly MultiRedisCacheService _multi;
    private readonly DefaultBackendCacheService _defaultBackend;

    public RedisCacheController(
        RedisCacheService redis,
        MultiRedisCacheService multi,
        DefaultBackendCacheService defaultBackend)
    {
        _redis = redis;
        _multi = multi;
        _defaultBackend = defaultBackend;
    }

    /// <summary>
    /// 【8.1】Redis 查询用户
    /// </summary>
    [Description("【8.1】Redis 查询用户")]
    [HttpGet("redis/{id}")]
    public IActionResult RedisGet(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _redis.GetUser(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _redis.CallCount,
            cacheManager = "RedisCacheManager",
            cacheKey = $"redis:users:{id}",
            note = "数据存储在 Redis 中，多实例共享缓存。可通过 redis-cli 查看"
        });
    }

    /// <summary>
    /// 【8.2】Redis + 滑动过期
    /// </summary>
    [Description("【8.2】Redis + 滑动过期")]
    [HttpGet("redis/sliding/{id}")]
    public IActionResult RedisSliding(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _redis.GetUserSliding(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _redis.CallCount,
            cacheManager = "RedisCacheManager + 滑动过期",
            slidingTtl = "300秒",
            absoluteMaxTtl = "86400秒（24小时）"
        });
    }

    /// <summary>
    /// 【8.3】Redis CachePut
    /// </summary>
    [Description("【8.3】Redis CachePut")]
    [HttpPost("redis/update")]
    public IActionResult RedisUpdate([FromForm] int id, [FromForm] string? name = null,
        [FromForm] int age = 0)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _redis.UpdateUser(new UserDto
        {
            Id = id, Name = name ?? "", Age = age, Email = $"{name ?? ""}@redis.com"
        });
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            cacheManager = "RedisCacheManager (CachePut)",
            note = "更新数据库后同步更新 Redis 缓存，多实例共享最新数据"
        });
    }

    /// <summary>
    /// 【8.4】Memory 缓存（与 Redis 对比）
    /// </summary>
    [Description("【8.4】Memory 缓存（与 Redis 对比）")]
    [HttpGet("redis/memory/{id}")]
    public IActionResult RedisMemory(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _redis.GetUserFromMemory(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _redis.CallCount,
            cacheManager = "MemoryCacheManager（进程内缓存）",
            note = "数据存储在应用程序进程内存中，访问速度最快但多实例不共享"
        });
    }

    /// <summary>
    /// 【8.5】一个项目里同时使用两个 Redis 后端
    /// </summary>
    [Description("【8.5】多个 Redis 后端（同一项目，两个实例/索引 + 两套序列化器）")]
    [HttpGet("redis/multi/{id}")]
    public IActionResult RedisMulti(int id)
    {
        // 同一个 id 走两次，只是 cacheManager 不同 —— 数据落在 db 11 与 db 12
        var swMain = System.Diagnostics.Stopwatch.StartNew();
        var fromMain = _multi.GetUserFromMainSite(id);
        swMain.Stop();

        var swCas = System.Diagnostics.Stopwatch.StartNew();
        var fromCas = _multi.GetUserFromCasAuth(id);
        swCas.Stop();

        // 再加一组「复杂类型」样本：UserDto 在两套序列化器下文本恰好相同，
        // 而 MultiBackendSampleDto 含 double，能做到肉眼可见不同（1.0 vs 1）。
        var swMainSample = System.Diagnostics.Stopwatch.StartNew();
        var sampleMain = _multi.GetSampleFromMainSite(id);
        swMainSample.Stop();

        var swCasSample = System.Diagnostics.Stopwatch.StartNew();
        var sampleCas = _multi.GetSampleFromCasAuth(id);
        swCasSample.Stop();

        return Ok(new
        {
            id,
            backends = new object[]
            {
                new
                {
                    name = "main-site",
                    cacheManager = "RedisCacheManager（基类，未重写任何重写点）",
                    configKey = "redis:main-site",
                    redisDb = "连接串 defaultDatabase（样例为 11）",
                    serializer = "NewtonsoftRedisCacheSerializer（默认）",
                    cacheKey = $"multi:main:{id}",
                    data = fromMain,
                    elapsedMs = swMain.ElapsedMilliseconds
                },
                new
                {
                    name = "cas-auth",
                    cacheManager = "CasAuthRedisManager（继承 RedisCacheManager，重写两个点）",
                    configKey = "redis:cas-auth",
                    redisDb = "连接串 defaultDatabase（样例为 12）",
                    serializer = "SystemTextJsonRedisCacheSerializer",
                    cacheKey = $"multi:cas:{id}",
                    data = fromCas,
                    elapsedMs = swCas.ElapsedMilliseconds
                }
            },
            // 8.5 的「看得见序列化差异」对照组：同一类型、同一 id、只换 cacheManager
            serializerProof = new
            {
                type = nameof(MultiBackendSampleDto),
                why = "同一个复杂类型走两套序列化器；键名不同是因为字节不同，不能共用 cacheName",
                mainSite = new
                {
                    cacheKey = $"multi:sample:{id}",
                    redisDb = 11,
                    serializer = "Newtonsoft —— Score 写作 1.0",
                    data = sampleMain,
                    elapsedMs = swMainSample.ElapsedMilliseconds
                },
                casAuth = new
                {
                    cacheKey = $"multi:cas-sample:{id}",
                    redisDb = 12,
                    serializer = "System.Text.Json —— Score 写作 1",
                    data = sampleCas,
                    elapsedMs = swCasSample.ElapsedMilliseconds
                }
            },
            callCount = _multi.CallCount,
            note = "四个方法只有 cacheManager 一个参数不同。第二次调用 elapsedMs 应掉到个位数。" +
                   "核对：redis-cli -n 11 GET multi:sample:1（Score:1.0）与 " +
                   "redis-cli -n 12 GET multi:cas-sample:1（Score:1）—— 两边的 JSON 文本不同，" +
                   "这就是『换了后端真的换了序列化器』的直接证据。"
        });
    }

    /// <summary>
    /// 【8.6】Service 级默认后端（自定义 db 1） + 方法级覆盖
    /// </summary>
    [Description("【8.6】Service 级默认后端（自定义 db 1）+ 方法级覆盖（db 2 / 内存）")]
    [HttpGet("redis/default-backend/{id}")]
    public IActionResult RedisDefaultBackend(int id)
    {
        // 三个方法故意只差「写没写 cacheManager」这一个参数：
        //   A 不写      → 继承类级 [CacheConfig] 的 Db1RedisManager（db 1）
        //   B 写 db 2   → 方法级覆盖（db 2，同一个 cacheName = 同键不同库）
        //   C 写内存    → 方法级覆盖（进程内；不能靠「不写」表达这个意思）
        var swA = System.Diagnostics.Stopwatch.StartNew();
        var fromA = _defaultBackend.GetDefaultBackend(id);
        swA.Stop();

        var swB = System.Diagnostics.Stopwatch.StartNew();
        var fromB = _defaultBackend.GetFromDb2(id);
        swB.Stop();

        var swC = System.Diagnostics.Stopwatch.StartNew();
        var fromC = _defaultBackend.GetFromMemory(id);
        swC.Stop();

        return Ok(new
        {
            id,
            methods = new object[]
            {
                new
                {
                    name = "A",
                    method = nameof(DefaultBackendCacheService.GetDefaultBackend),
                    annotation = "[Cacheable(key: \"#id\")]",
                    cacheManager = "继承类级 [CacheConfig] → Db1RedisManager",
                    redisDb = 1,
                    cacheKey = $"svc:default:{id}",
                    data = fromA,
                    elapsedMs = swA.ElapsedMilliseconds
                },
                new
                {
                    name = "B",
                    method = nameof(DefaultBackendCacheService.GetFromDb2),
                    annotation = "[Cacheable(key: \"#id\", cacheManager: typeof(Db2RedisManager))]",
                    cacheManager = "方法级覆盖 → Db2RedisManager",
                    redisDb = 2,
                    cacheKey = $"svc:default:{id}",
                    data = fromB,
                    elapsedMs = swB.ElapsedMilliseconds
                },
                new
                {
                    name = "C",
                    method = nameof(DefaultBackendCacheService.GetFromMemory),
                    annotation = "[Cacheable(cacheName: \"svc:memory\", key: \"#id\", cacheManager: typeof(MemoryCacheManager))]",
                    cacheManager = "方法级覆盖 → MemoryCacheManager",
                    redisDb = (int?)null,
                    cacheKey = $"svc:memory:{id}",
                    data = fromC,
                    elapsedMs = swC.ElapsedMilliseconds
                }
            },
            callCount = _defaultBackend.CallCount,
            note = "A 与 B 的缓存键完全相同（svc:default:{id}），只是落在 db 1 / db 2 —— " +
                   "两条数据互不可见，所以第一次调用三个方法都真实执行（callCount 增长 3）。" +
                   "第二次调用三个都命中（callCount 不再增长）。核对：" +
                   "redis-cli -n 1 KEYS 'svc:default*' 与 redis-cli -n 2 KEYS 'svc:default*' 各自有值；" +
                   "redis-cli -n 11 查不到 svc:* —— 代码里指定了库号，连接串的 defaultDatabase(11) 被忽略。",
            hint = $"⚠️ 建议 id 取 {DefaultBackendCacheService.IdBase}~{DefaultBackendCacheService.IdUpper} 以验证「真实值」命中。"
                 + "服务端只在这个区间造数据，区间外返回 null —— v0.2.0 起框架默认也缓存 null"
                 + "（对齐 Spring，Redis 里写 7 字节哨兵区分「存了 null」与「键不存在」），"
                 + "所以区间外的 id 第二次同样命中；要退回旧行为请用 unless = \"#result == null\"。"
        });
    }
}
