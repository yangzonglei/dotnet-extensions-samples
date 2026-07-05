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
}
