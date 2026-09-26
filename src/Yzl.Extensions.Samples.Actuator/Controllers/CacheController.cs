using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Actuator.Controllers;

/// <summary>
/// 演示业务侧 <see cref="IMemoryCache"/> 写入与 <c>/actuator/caches</c> 端点的联动。
///
/// <para>
/// <c>/actuator/caches</c> 通过 <c>ICacheRegistry</c> 反射读取 <see cref="MemoryCache"/> 内部的
/// 条目集合，因此只要业务代码往 <see cref="IMemoryCache"/> 里写过数据，
/// 端点就能列出对应的缓存名；调用 <c>DELETE /actuator/caches</c> 可清空全部缓存。
/// </para>
///
/// <para>
/// 典型用法：
/// <code>
/// GET  /api/cache/add?name=demo     # 写入一条缓存
/// GET  /api/cache/get?name=demo     # 读回
/// GET  /actuator/caches             # 看到 memoryCache → demo
/// DELETE /actuator/caches/demo      # 删除单个缓存
/// DELETE /actuator/caches           # 清空全部缓存
/// </code>
/// </para>
/// </summary>
[ApiController]
[Route("api/cache")]
[TestDashboardInfo("🗄️ 缓存联动", Order = 4, Badge = "IMemoryCache")]
public class CacheController : ControllerBase
{
    private readonly IMemoryCache _memoryCache;

    public CacheController(IMemoryCache memoryCache) => _memoryCache = memoryCache;

    /// <summary>写入一条缓存，随后可在 /actuator/caches 中看到该缓存名。</summary>
    [HttpGet("add")]
    public IActionResult AddCache([FromQuery] string name = "demo")
    {
        var value = Random.Shared.Next();
        _memoryCache.Set(name, value, TimeSpan.FromDays(1));

        return Ok(new
        {
            message = $"已写入缓存 {name}",
            name,
            value,
            hint = "访问 /actuator/caches 查看缓存列表，或 DELETE /actuator/caches 清空"
        });
    }

    /// <summary>读回缓存值。</summary>
    [HttpGet("get")]
    public IActionResult GetCache([FromQuery] string name = "demo")
    {
        var value = _memoryCache.Get(name);

        return Ok(new
        {
            name,
            value,
            found = value != null
        });
    }

    /// <summary>删除单条缓存。</summary>
    [HttpGet("remove")]
    public IActionResult RemoveCache([FromQuery] string name = "demo")
    {
        _memoryCache.Remove(name);

        return Ok(new { message = $"已移除缓存 {name}", name });
    }
}
