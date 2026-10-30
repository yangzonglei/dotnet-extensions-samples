using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Samples.Cache.Services;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Cache.Controllers;

/// <summary>
/// 第九章：CacheEvict AllEntries
/// </summary>
[ApiController]
[TestDashboardInfo("📖 第九章：CacheEvict AllEntries", Order = 9, Badge = "9")]
[Route("api/samples")]
public class CacheEvictAllController : ControllerBase
{
    private readonly CacheEvictAllService _evictAll;

    public CacheEvictAllController(CacheEvictAllService evictAll)
    {
        _evictAll = evictAll;
    }

    /// <summary>
    /// 【9.1】查询用户（用于演示缓存清除）
    /// </summary>
    [Description("【9.1】查询用户（写入缓存）")]
    [HttpGet("evict-all/{id}")]
    public IActionResult EvictAllGet(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _evictAll.GetUser(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _evictAll.CallCount,
            note = $"用户 {id} 的数据已缓存到 'evict-all' 区域"
        });
    }

    /// <summary>
    /// 【9.2】逐条清除缓存（仅清除指定 ID）
    /// </summary>
    [Description("【9.2】逐条清除缓存")]
    [HttpPost("evict-all/evict-single/{id}")]
    public IActionResult EvictSingle(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _evictAll.EvictSingle(id);
        sw.Stop();
        return Ok(new
        {
            elapsedMs = sw.ElapsedMilliseconds,
            operation = $"已清除 evict-all:{id} 的缓存",
            note = "只清除了指定用户的缓存，其他用户的缓存仍然有效"
        });
    }

    /// <summary>
    /// 【9.3】批量清除整个缓存区域
    /// </summary>
    [Description("【9.3】批量清除整个缓存区域（allEntries）")]
    [HttpPost("evict-all/clear-all")]
    public IActionResult EvictAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _evictAll.EvictAll();
        sw.Stop();
        return Ok(new
        {
            elapsedMs = sw.ElapsedMilliseconds,
            operation = "已清除 'evict-all' 区域下所有缓存",
            note = "下次查询任何用户时，都将重新从数据源加载"
        });
    }

    /// <summary>
    /// 【9.4】Redis 后端的 allEntries —— 与 9.3 的关键差异
    ///
    /// <c>MemoryCacheProvider.RemoveByPrefixAsync</c> 是空操作（<c>MemoryCache</c>
    /// 原生不支持前缀枚举），所以 9.3 清不掉任何东西；而 Redis 后端走
    /// <c>SCAN prefix*</c> + <c>DEL</c>，是<b>真的</b>按前缀批量驱逐。
    ///
    /// 本端点用独立的 cacheName <c>redis:evict-all</c>，落在 redis:main-site 的 db 11，
    /// 不会碰到其他章节的键。
    /// </summary>
    [Description("【9.4】Redis allEntries 批量清除（真删）")]
    [HttpPost("evict-all/clear-all-redis")]
    public IActionResult EvictAllRedis()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _evictAll.EvictAllFromRedis();
        sw.Stop();
        return Ok(new
        {
            elapsedMs = sw.ElapsedMilliseconds,
            operation = "已按前缀 'redis:evict-all' 批量删除 Redis 键",
            note = "与 9.3 对比：内存后端的 allEntries 是空操作，Redis 后端是真删"
        });
    }

    /// <summary>
    /// 【9.4】查询用户（写入 Redis 缓存，供 allEntries 演示使用）
    /// </summary>
    [Description("【9.4】查询用户（写入 Redis）")]
    [HttpGet("evict-all/redis/{id}")]
    public IActionResult EvictAllRedisGet(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _evictAll.GetUserFromRedis(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _evictAll.CallCount,
            cacheKey = $"redis:evict-all:{id}",
            note = "键落在 redis:main-site 的 db 11，可用 redis-cli -n 11 KEYS 'redis:evict-all*' 核对"
        });
    }
}
