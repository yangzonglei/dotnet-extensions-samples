using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Samples.Cache.Services;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Cache.Controllers;

/// <summary>
/// 首页和服务统计
/// </summary>
[ApiController]
[TestDashboardInfo("🏠 首页 & 服务统计", Order = 0)]
[Route("api/samples")]
public class HomeController : ControllerBase
{
    private readonly BasicCacheService _basicCache;
    private readonly CacheLifecycleService _lifecycle;
    private readonly SpelKeyService _spelKey;
    private readonly ConditionalService _conditional;
    private readonly ConfigInheritanceService _config;
    private readonly AsyncCacheService _async;
    private readonly SlidingExpirationService _sliding;
    private readonly RedisCacheService _redis;
    private readonly CacheEvictAllService _evictAll;

    public HomeController(
        BasicCacheService basicCache,
        CacheLifecycleService lifecycle,
        SpelKeyService spelKey,
        ConditionalService conditional,
        ConfigInheritanceService config,
        AsyncCacheService async,
        SlidingExpirationService sliding,
        RedisCacheService redis,
        CacheEvictAllService evictAll)
    {
        _basicCache = basicCache;
        _lifecycle = lifecycle;
        _spelKey = spelKey;
        _conditional = conditional;
        _config = config;
        _async = async;
        _sliding = sliding;
        _redis = redis;
        _evictAll = evictAll;
    }

    /// <summary>
    /// 示例导航首页
    /// </summary>
    [Description("🧭 示例导航首页")]
    [HttpGet]
    public IActionResult Index()
    {
        return Content(GetHomePage(), "text/html");
    }

    /// <summary>
    /// 查看所有服务的调用统计
    /// </summary>
    [Description("📊 查看所有服务调用统计")]
    [HttpGet("stats")]
    public IActionResult Stats()
    {
        return Ok(new
        {
            basicCache_calls = _basicCache.GetUserCallCount,
            legacyCache_calls = _basicCache.GetUserLegacyCallCount,
            lifecycle_calls = _lifecycle.CallCount,
            spelKey_calls = _spelKey.CallCount,
            conditional_calls = new
            {
                condition = _conditional.ConditionCallCount,
                unless = _conditional.UnlessCallCount,
                combined = _conditional.CombinedCallCount,
                complex = _conditional.ComplexCallCount,
                put = _conditional.PutCallCount,
                evict = _conditional.EvictCallCount,
                asyncUnless = _conditional.ConditionAsyncUnlessCallCount,
                errorCorrection = _conditional.ErrorCorrectionCallCount,
            },
            config_calls = _config.CallCount,
            async_calls = _async.CallCount,
            sliding_calls = _sliding.CallCount,
            redis_calls = _redis.CallCount,
            evictAll_calls = _evictAll.CallCount,
            tip = "调用统计计数的是实际执行的方法次数（不含缓存命中的请求），用于验证缓存命中率"
        });
    }

    // ===================================================================
    // 首页 HTML
    // ===================================================================
    private static string GetHomePage()
    {
        return @"<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <title>Yzl.Extensions.Samples.Cache 示例项目</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: -apple-system, 'Microsoft YaHei', sans-serif; background: #f5f5f5; padding: 20px; color: #333; }
        .container { max-width: 1100px; margin: 0 auto; }
        h1 { color: #2563eb; border-bottom: 3px solid #2563eb; padding-bottom: 10px; margin-bottom: 20px; font-size: 24px; }
        h2 { color: #1e40af; margin: 25px 0 10px; padding-left: 10px; border-left: 4px solid #2563eb; font-size: 18px; }
        .section { background: #fff; border-radius: 8px; padding: 15px 20px; margin-bottom: 15px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
        .endpoint { display: inline-block; background: #059669; color: #fff; padding: 3px 8px; border-radius: 4px; font-size: 12px; font-family: monospace; margin: 2px 0; }
        .endpoint.get { background: #059669; }
        .endpoint.post { background: #d97706; }
        ul { list-style: none; padding: 0; }
        li { padding: 6px 0; border-bottom: 1px solid #f0f0f0; display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
        li:last-child { border-bottom: none; }
        a { color: #2563eb; text-decoration: none; font-size: 14px; font-family: monospace; }
        a:hover { text-decoration: underline; color: #1d4ed8; }
        .badge { font-size: 11px; padding: 2px 6px; border-radius: 10px; color: #fff; }
        .badge.memory { background: #6366f1; }
        .badge.redis { background: #ef4444; }
        .badge.both { background: #8b5cf6; }
        .badge.async { background: #0ea5e9; }
        .badge.typo { background: #ef4444; }
        .badge.diag { background: #6b7280; }
        .desc { font-size: 12px; color: #9ca3af; margin-left: 4px; }
        .stats-link { display: inline-block; margin-top: 15px; padding: 8px 16px; background: #2563eb; color: #fff; border-radius: 6px; text-decoration: none; font-size: 14px; }
        .stats-link:hover { background: #1d4ed8; }
    </style>
</head>
<body>
<div class='container'>
    <h1>🚀 Yzl.Extensions.Cache 示例项目</h1>
    <p style='color:#6b7280;margin-bottom:20px;'>
        本项目演示了 Yzl.Extensions.Cache 框架的所有核心功能。
        建议按照章节顺序依次访问，配合源码中的 XML 注释学习。
    </p>

    <h2>📖 第一章：基础 Cacheable 用法</h2>
    <div class='section'>
        <ul>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/basic/1'>/api/samples/basic/{id}</a> <span class='desc'>基础缓存（按ID查询，TTL=60s）</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/basic/short-ttl/1'>/api/samples/basic/short-ttl/{id}</a> <span class='desc'>短 TTL（10秒过期）</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/basic/by-name?name=Alice'>/api/samples/basic/by-name?name=</a> <span class='desc'>字符串缓存键</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/basic/age-range?minAge=25&maxAge=35'>/api/samples/basic/age-range?minAge=&maxAge=</a> <span class='desc'>组合键缓存</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/basic/legacy/1'>/api/samples/basic/legacy/{id}</a> <span class='desc'>ttl 简写属性兼容性</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/basic/legacy-sliding/1'>/api/samples/basic/legacy-sliding/{id}</a> <span class='desc'>slidingTtl 简写属性兼容性</span></li>
        </ul>
    </div>

    <h2>📖 第二章：CachePut &amp; CacheEvict（缓存生命周期）</h2>
    <div class='section'>
        <ul>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/lifecycle/1'>/api/samples/lifecycle/{id}</a> <span class='desc'>查询（Cacheable）</span></li>
            <li><span class='endpoint post'>POST</span> /api/samples/lifecycle/update <span class='desc'>更新（CachePut，始终执行并写入）</span></li>
            <li><span class='endpoint post'>POST</span> /api/samples/lifecycle/delete <span class='desc'>删除（CacheEvict）</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/lifecycle/refresh/1'>/api/samples/lifecycle/refresh/{id}</a> <span class='desc'>强制刷新缓存（CachePut）</span></li>
        </ul>
    </div>

    <h2>📖 第三章：SpEL 键表达式</h2>
    <div class='section'>
        <ul>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/spel/query?userId=1&keyword=test'>/api/samples/spel/query</a> <span class='desc'>嵌套属性访问 #qo.UserId:#qo.Keyword</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/spel/config'>/api/samples/spel/config</a> <span class='desc'>字典键访问 #cfg.site_name:#cfg.version</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/spel/positional/1'>/api/samples/spel/positional/{id}</a> <span class='desc'>位置参数 #p0</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/spel/default-name/1'>/api/samples/spel/default-name/{id}</a> <span class='desc'>默认方法全限定名作为 cacheName</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/spel/sliding-redis/1'>/api/samples/spel/sliding-redis/{id}</a> <span class='badge redis'>Redis</span> <span class='desc'>SpEL + Redis + 滑动过期组合</span></li>
        </ul>
    </div>

    <h2>📖 第四章：Condition &amp; Unless（条件缓存）</h2>
    <div class='section'>
        <ul>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/condition/cacheable/20'>/api/samples/condition/cacheable/{id}</a> <span class='desc'>Condition: #id > 10</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/condition/unless/1'>/api/samples/condition/unless/{id}</a> <span class='desc'>Unless: #result == null</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/condition/combined/1'>/api/samples/condition/combined/{id}</a> <span class='desc'>Condition + Unless 组合</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/condition/complex/1'>/api/samples/condition/complex/{id}</a> <span class='desc'>复杂条件：#p0 && 排除特殊邮箱</span></li>
            <li><span class='endpoint post'>POST</span> /api/samples/condition/put <span class='desc'>CachePut + 条件写入</span></li>
            <li><span class='endpoint post'>POST</span> /api/samples/condition/evict <span class='desc'>CacheEvict + Condition 条件性驱逐</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/condition/async-unless/1'>/api/samples/condition/async-unless/{id}</a> <span class='badge async'>async</span> <span class='desc'>异步方法 + Unless</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/condition/typo-condition/1'>/api/samples/condition/typo-condition/{id}</a> <span class='badge typo'>typo</span> <span class='desc'>Condition 拼写错误（永不缓存）</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/condition/typo-unless/1'>/api/samples/condition/typo-unless/{id}</a> <span class='badge typo'>typo</span> <span class='desc'>Unless 拼写错误（永不缓存）</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/condition/typo-chain/1'>/api/samples/condition/typo-chain/{id}</a> <span class='badge typo'>typo</span> <span class='desc'>属性链拼写错误（永不缓存）</span></li>
        </ul>
    </div>

    <h2>📖 第五章：CacheConfig 继承</h2>
    <div class='section'>
        <ul>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/config/default/1'>/api/samples/config/default/{id}</a> <span class='desc'>完全继承 [CacheConfig]</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/config/custom-name/10'>/api/samples/config/custom-name/{id}</a> <span class='desc'>覆盖 cacheName</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/config/custom-ttl/1'>/api/samples/config/custom-ttl/{id}</a> <span class='desc'>覆盖 TTL</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/config/fully-custom/1'>/api/samples/config/fully-custom/{id}</a> <span class='desc'>完全覆盖</span></li>
        </ul>
    </div>

    <h2>📖 第六章：异步缓存</h2>
    <div class='section'>
        <ul>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/async/100'>/api/samples/async/{id}</a> <span class='desc'>异步查询（async/await）</span></li>
            <li><span class='endpoint post'>POST</span> /api/samples/async/update <span class='desc'>异步更新（CachePut）</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/async/all'>/api/samples/async/all</a> <span class='desc'>异步批量查询（缓存集合）</span></li>
        </ul>
    </div>

    <h2>📖 第七章：滑动过期</h2>
    <div class='section'>
        <ul>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/sliding/fixed/1'>/api/samples/sliding/fixed/{id}</a> <span class='desc'>固定 TTL（对照实验）</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/sliding/basic/1'>/api/samples/sliding/basic/{id}</a> <span class='desc'>滑动过期（每次访问续期 30 秒）</span></li>
        </ul>
    </div>

    <h2>📖 第八章：Redis 缓存</h2>
    <div class='section'>
        <ul>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/redis/1'>/api/samples/redis/{id}</a> <span class='badge redis'>Redis</span> <span class='desc'>Redis 查询</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/redis/sliding/1'>/api/samples/redis/sliding/{id}</a> <span class='badge redis'>Redis</span> <span class='desc'>Redis + 滑动过期</span></li>
            <li><span class='endpoint post'>POST</span> /api/samples/redis/update <span class='badge redis'>Redis</span> <span class='desc'>Redis CachePut</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/redis/memory/1'>/api/samples/redis/memory/{id}</a> <span class='badge memory'>Memory</span> <span class='desc'>Memory vs Redis 对比</span></li>
        </ul>
    </div>

    <h2>📖 第九章：CacheEvict AllEntries</h2>
    <div class='section'>
        <ul>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/evict-all/1'>/api/samples/evict-all/{id}</a> <span class='desc'>查询用户（写入缓存）</span></li>
            <li><span class='endpoint post'>POST</span> /api/samples/evict-all/evict-single/{id} <span class='desc'>逐条清除缓存</span></li>
            <li><span class='endpoint post'>POST</span> /api/samples/evict-all/clear-all <span class='desc'>批量清除整个缓存区域</span></li>
        </ul>
    </div>

    <h2>🔍 诊断工具：执行次数统计</h2>
    <div class='section'>
        <ul>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/condition/call-count'>/api/samples/condition/call-count</a> <span class='badge diag'>diag</span> <span class='desc'>GetUserWithCondition 执行次数</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/condition/unless-count'>/api/samples/condition/unless-count</a> <span class='badge diag'>diag</span> <span class='desc'>GetUserWithUnless 执行次数</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/condition/combined-count'>/api/samples/condition/combined-count</a> <span class='badge diag'>diag</span> <span class='desc'>GetUserCombined 执行次数</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/condition/async-unless-count'>/api/samples/condition/async-unless-count</a> <span class='badge diag'>diag</span> <span class='desc'>GetUserAsyncUnless 执行次数</span></li>
            <li><span class='endpoint get'>GET</span> <a href='/api/samples/async/call-count'>/api/samples/async/call-count</a> <span class='badge diag'>diag</span> <span class='desc'>GetUserAsync 执行次数</span></li>
        </ul>
    </div>

    <div style='text-align:center;margin-top:20px;'>
        <a href='/api/samples/stats' class='stats-link'>📊 查看完整服务调用统计</a>
    </div>

    <p style='text-align:center;color:#9ca3af;font-size:12px;margin-top:30px;'>
        Yzl.Extensions.Cache Samples
    </p>
</div>
</body>
</html>";
    }
}
