using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Yzl.Extensions.Samples.Cache.Configuration;
using Yzl.Extensions.Samples.Cache.Managers;
using Yzl.Extensions.Samples.Cache.Services;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Cache.Controllers;

/// <summary>
/// 第十章：Redis 连接（配置节 / 用户注入连接 / 连接层高可用）
///
/// 8.x 演绎的是「注解上选哪个后端」；本章演绎的是更下一层的**连接从哪来**：
/// 10.1 连接来自 <c>spring:data:redis</c> 配置节（Java：改 <c>spring.data.redis.*</c>）；
/// 10.2 连接由用户自己建并注入（Java：自己声明一个 <c>@Bean RedisConnectionFactory</c>）；
/// 10.3 证明「同一个连接名只建一次」；
/// 10.4 是客户端自己用 <c>[ConfigurationProperties]</c> 读 Redis 配置的标准写法；
/// 10.5 是连接层高可用（开机抖动 / 中途断开后自愈，不需要重启宿主）。
///
/// ⚠️ 本章所有端点都<b>不输出任何凭据</b>：样例自己的配置类
/// （<see cref="SamplesRedisConfig"/>）刻意没有 <c>Password</c> 属性。
/// </summary>
[ApiController]
[TestDashboardInfo("📖 第十章：Redis 连接（配置 / 注入 / 高可用）", Order = 10, Badge = "10")]
[Route("api/samples")]
public class CacheConnectionController : ControllerBase
{
    private readonly CacheConnectionService _connection;
    private readonly IOptions<SamplesRedisConfig> _samplesRedisConfig;

    /// <summary>10.5 用来推断「本次是不是命中」——每次调用都重新计时，所以不保留状态。</summary>
    private const long HitThresholdMs = 800;

    public CacheConnectionController(
        CacheConnectionService connection,
        IOptions<SamplesRedisConfig> samplesRedisConfig)
    {
        _connection = connection;
        _samplesRedisConfig = samplesRedisConfig;
    }

    // ===================================================================
    // 10.1 连接来自配置节
    // ===================================================================

    /// <summary>
    /// 【10.1】连接来自 <c>spring:data:redis</c> 配置节（一行子类，只换连接名）
    /// </summary>
    [Description("【10.1】连接来自配置节 spring:data:redis（Java：改 spring.data.redis.*）")]
    [HttpGet("connection/config/{id}")]
    public IActionResult ConfigBackend(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _connection.GetFromConfig(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _connection.CallCount,
            connectionName = ConfigBackendRedisManager.Name,
            resolvedFrom = "spring:data:redis（唯一那个 Redis 的配置节；连接名没有扁平键，于是落到这一节）",
            cacheManager = nameof(ConfigBackendRedisManager) + "（继承 RedisCacheManager，只重写 ConnectionStringKey）",
            configuredDatabase = ConfigBackendRedisManager.ConfiguredDatabase,
            configuredClientName = ConfigBackendRedisManager.ConfiguredClientName,
            cacheKey = $"conn:config:users:{id}",
            redisCli = "redis-cli -p 6399 -n 5 KEYS 'conn:config*'",
            note = "代码里没有任何连接参数：host/port/database/超时/心跳/重试/client-name 全在配置节里。" +
                   "改配置就能换实例、换库、调超时 —— 不需要重新编译，也不需要改一行 C#。" +
                   $"第二次调用 elapsedMs 应掉到个位数；id 建议取 {CacheConnectionService.IdBase}~{CacheConnectionService.IdUpper}。"
        });
    }

    // ===================================================================
    // 10.2 连接由用户注入
    // ===================================================================

    /// <summary>
    /// 【10.2】连接由用户自己建（<c>IRedisConnectionSource</c> + 不继承基类的 <c>ICacheManager</c>）
    /// </summary>
    [Description("【10.2】连接由用户注入（IRedisConnectionSource + 手写 ICacheManager）")]
    [HttpGet("connection/injected/{id}")]
    public IActionResult InjectedBackend(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _connection.GetFromInjected(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _connection.CallCount,
            connectionName = SamplesInjectedConnectionSource.ConnectionName,
            resolvedFrom = "① 用户 IRedisConnectionSource 认领（配置里的同名扁平键被跳过）",
            cacheManager = nameof(InjectedConnectionCacheManager) + "（不继承 RedisCacheManager，直接实现 ICacheManager）",
            serializer = nameof(Yzl.Extensions.Cache.Redis.Serialization.SystemTextJsonRedisCacheSerializer),
            actualDatabase = SamplesInjectedConnectionSource.DatabaseIndex,
            configuredDatabaseForSameName = 7,
            cacheKey = $"conn:injected:users:{id}",
            redisCli = $"redis-cli -p 6399 -n {SamplesInjectedConnectionSource.DatabaseIndex} KEYS 'conn:injected*'",
            note = "配置里同一个连接名的扁平键写的是 defaultDatabase=7，实际落到了 8 —— 这个不一致是刻意的证据：" +
                   "RedisConnectionProvider.Create() 的顺序是 ①用户来源 → ②配置 → ③抛错，第①步命中后压根不读配置。" +
                   "核对：redis-cli -p 6399 -n 7 KEYS 'conn:injected*' 返回空；-n 8 才有值。" +
                   "CLIENT LIST 里还能看到连接自带的客户端名 samples-cache-injected-by-user。",
            hint = $"id 建议取 {CacheConnectionService.IdBase}~{CacheConnectionService.IdUpper}。"
        });
    }

    // ===================================================================
    // 10.3 「只创建一次」的证明
    // ===================================================================

    /// <summary>
    /// 【10.3】证明用户连接「只被创建一次」—— 多次访问计数不变
    /// </summary>
    [Description("【10.3】用户连接只创建一次（建连计数器）")]
    [HttpGet("connection/injected-connection-count")]
    public IActionResult InjectedConnectionCount() => Ok(new
    {
        connectionName = SamplesInjectedConnectionSource.ConnectionName,
        connectCount = SamplesInjectedConnectionSource.ConnectCount,
        cacheManager = nameof(InjectedConnectionCacheManager),
        note = "计数在【用户自己的来源】里，不是框架内部 —— 因为契约表写得很清楚：" +
               "「只创建一次」最终由你的实现保证（框架只按连接名去重，不去干预你返回什么）。" +
               "期待行为：从 0 涨到 1 之后就不再变化。先连续访问 /connection/injected/2001 若干次，再看这个端点仍是 1。" +
               "认真核对并发：用多个终端同时打第一个请求，计数依然是 1（来源内部 lock + 二次检查，" +
               "且工厂在锁内、发布之前完成 —— 失败不留痕，成功才发布）。",
        whyNotLazy = "刻意没用 Lazy<IConnectionMultiplexer>(ExecutionAndPublication)：它会永久缓存工厂抛出的异常，" +
                     "第一次失败之后 factory 再也不被调用 —— 「开机抖动一次 = 这个后端到进程重启为止一直失效」。",
        ownership = "连接由用户创建 ⇒ 归用户释放。本样例把来源注册成 DI 单例并实现 IDisposable，由容器负责释放，框架的 Dispose 链不碰它。"
    });

    // ===================================================================
    // 10.4 客户端自己读配置（并当场演示冒号层级这个坑）
    // ===================================================================

    /// <summary>
    /// 【10.4】客户端自己用 <c>[ConfigurationProperties("spring:data:redis")]</c> 读配置
    /// </summary>
    [Description("【10.4】客户端自定义配置类读取 spring:data:redis（与 Java 同形）")]
    [HttpGet("connection/options")]
    public IActionResult Options()
    {
        var bound = _samplesRedisConfig.Value;

        return Ok(new
        {
            sectionName = SamplesRedisConfig.SectionName,
            boundType = nameof(SamplesRedisConfig),
            registeredBy = "全局配置扫描（AddBatchServices / AddFeignStarter 内部调 AddConfigurationPropertiesScan）—— 本类没有任何注册代码",
            bound = new
            {
                host = bound.Host,
                port = bound.Port,
                database = bound.Database,
                clientName = bound.ClientName,
                timeout = bound.Timeout,
                connectTimeout = bound.ConnectTimeout
            },
            shape = "spring:data:redis 直接就是这几个字段 —— 与 Java 的 RedisProperties 同形，" +
                    "没有中间的 default / connections 层。框架自己的 RedisCacheOptions 也是同样的平铺结构。",
            multipleRedises = "要连多个 Redis 时不要在这里加嵌套层（Java 也不这么做）：" +
                              "Java 是自己声明多个 @Bean RedisConnectionFactory，本框架是 AddRedisConnection(...) / " +
                              "IRedisConnectionSource（见 10.2）。",
            keyNameTrap = "client-name / connect-timeout 这类带连字符的键必须显式标 [ConfigurationKeyName]，" +
                          "裸写 ClientName 是静默绑不上的（不抛异常、值为 null）—— 与框架 RedisCacheOptions 同一处坑。",
            durationTrap = "timeout / connect-timeout 在框架侧是 Java Duration 语义（5s / 500ms / PT5S，无单位按毫秒），" +
                           "本样例只把原样字符串读出来，不做二次解析。",
            noPassword = "本响应与样例配置类都刻意不含 Password 字段 —— 凭据不进日志、不进接口响应、不进文档。"
        });
    }

    // ===================================================================
    // 10.5 连接层高可用
    // ===================================================================

    /// <summary>
    /// 【10.5】连接层高可用：健康 / 抖动 / 自愈的同一条路径
    /// </summary>
    [Description("【10.5】连接层高可用（失败不留痕：抖动后能自愈，不用重启宿主）")]
    [HttpGet("connection/ha/{id}")]
    public IActionResult HighAvailability(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _connection.GetFromInjected(id);
        sw.Stop();

        var elapsed = sw.ElapsedMilliseconds;
        var likelyHit = elapsed < HitThresholdMs;

        return Ok(new
        {
            id,
            data = user,
            elapsedMs = elapsed,
            likelyHit,
            hitThresholdMs = HitThresholdMs,
            callCount = _connection.CallCount,
            connectCount = SamplesInjectedConnectionSource.ConnectCount,
            httpStatusIsNotTheSignal = "⚠️ 连接不可达时接口【不会】500，而是降级执行真实方法：" +
                                       "RedisConnectionException 是普通 Exception，被 CacheOperationDispatcher 记日志后放行。" +
                                       "唯一会 500 的是「cacheManager 写的类型没注册」—— 那是 InvalidOperationException，会穿透降级过滤器。",
            diagnostic = "判断「连接通没通」只看 elapsedMs：命中 ≈ 个位数毫秒（Redis 往返），" +
                         "未命中/降级 ≈ 1500ms 起（真实方法里的 Thread.Sleep(1500)）" +
                         "—— 若 Redis 是【不可达】而非仅未命中，还要叠加 connect-timeout × connect-retry 的重试时间，" +
                         "实测会到 4~8 秒。判定看「个位数 ⇄ 秒级」的量级跃迁，不要盯某个固定数字。",
            steps = new[]
            {
                "① 先访问本端点两次：第一次 elapsedMs ≈ 1500（未命中），第二次个位数（命中）。",
                "② 杀掉本地可丢弃实例：redis-cli -p 6399 shutdown nosave（或 Ctrl+C 掉那个进程）。",
                "③ 再访问一次：不会 500，elapsedMs 从个位数跌回秒级（连接失败被吞掉，走了真实方法）。" +
                "具体秒数不固定 —— 还要等 connect-timeout × connect-retry（样例配置 2000 × 3）。",
                "④ 把 Redis 起回来，再访问一次：【缓存自己恢复了命中】，elapsedMs 回到个位数 —— 不需要重启宿主。" +
                "第 1 次可能要几秒（多路复用器重新握手），第 2 次才回到个位数，两次都别重启宿主。",
                "⑤ 关键对照：改造前用的是 Lazy<IConnectionMultiplexer>(ExecutionAndPublication)，" +
                "它会把工厂抛出的异常【永久缓存】，第 ④ 步永远不会发生 —— 一次抖动 = 该后端到进程重启为止一直失效。"
            },
            whyItSelfHeals = "RedisConnectionProvider 内部包的是 RedisMultiplexerHolder，不是 Lazy：" +
                             "成功才发布、失败不留痕（工厂在锁内、发布之前完成；抛异常时什么都没发布，下一次访问重新建连）。",
            whyNotFailFast = "框架默认 abortConnect=false：开机瞬间 Redis 不可达【不抛】，返回一个已注册但尚未连上的多路复用器，" +
                             "后台自动重连。语义对齐 Java —— Lettuce 不会因为首次连不上就拒绝创建连接工厂。",
            credentials = "凭据只从配置读取，任何异常消息、ClientName、日志都不含密码；本端点也不回显任何配置里的敏感值。"
        });
    }

    // ===================================================================
    // 10.6 本章涉及的连接名一览
    // ===================================================================

    /// <summary>
    /// 【10.6】连接名一览：每一层各自命中了哪个连接名
    /// </summary>
    [Description("【10.6】连接名与四级查找的对照表")]
    [HttpGet("connection/names")]
    public IActionResult Names() => Ok(new
    {
        lookupOrder = new[]
        {
            "① 按连接名的扁平连接串键 configuration[连接名]（即 redis:main-site 这种，既有部署形态）",
            "② spring:data:redis 这一节（整节只描述一个 Redis，与 Java 一致；且必须给了端点）",
            "③ 都没有 ⇒ 抛「缺配置」的 InvalidOperationException ⇒ 被读/写路径的 catch 吞掉 ⇒ 缓存静默不生效"
        },
        whyFlatFirst = "扁平键只对那一个连接名生效，比全局唯一的 spring:data:redis 更具体 —— " +
                       "与 Java 里「自己声明的 @Bean RedisConnectionFactory 优先于自动配置」同一方向。",
        userSourceLookup = "⚠️ 以上两层之前还有一步：先问容器里所有 IRedisConnectionSource，任何一个认领了就【不读配置】。",
        names = new object[]
        {
            new
            {
                name = "redis:main-site",
                usedBy = "第八章 8.1~8.6 的默认后端 / RedisCacheManager 的默认 ConnectionStringKey",
                resolvedFrom = "第 ① 层：appsettings.Development.json 里的扁平连接串键",
                chapter = 8
            },
            new
            {
                name = "redis:cas-auth",
                usedBy = "8.5 的 CasAuthRedisManager",
                resolvedFrom = "第 ① 层：appsettings.Development.json 里的扁平连接串键",
                chapter = 8
            },
            new
            {
                name = ConfigBackendRedisManager.Name,
                usedBy = "10.1 的 ConfigBackendRedisManager",
                resolvedFrom = "第 ② 层：appsettings.json 的 spring:data:redis（该连接名没有扁平键）",
                chapter = 10
            },
            new
            {
                name = SamplesInjectedConnectionSource.ConnectionName,
                usedBy = "10.2 的 InjectedConnectionCacheManager（手写 ICacheManager）",
                resolvedFrom = "用户 IRedisConnectionSource 认领（配置节也在，但被跳过）",
                chapter = 10
            }
        },
        crossChapterRule = "全不命中时【不会 500】：读路径的异常被 CacheableHandler.TryReadCache 的 catch(Exception) 吞掉、" +
                           "写路径被 CacheOperationHelper.WriteCache 的同类 catch 吞掉。表现是「缓存永远不命中」。" +
                           "所以新增后端后要确认连接名真的配得上 —— 缺配置抛出的异常消息里会带上那个连接名与 cacheManager，这是唯一线索。",
        noValuesEchoed = "本端点只列【连接名】，不回显任何连接串或密码。"
    });
}
