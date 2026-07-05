using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Samples.Cache.Models;
using Yzl.Extensions.Samples.Cache.Services;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.Cache.Controllers;

/// <summary>
/// 第四章：Condition 和 Unless + 拼写错误容错测试
/// </summary>
[ApiController]
[TestDashboardInfo("📖 第四章：Condition & Unless", Order = 4, Badge = "4")]
[Route("api/samples")]
public class ConditionalController : ControllerBase
{
    private readonly ConditionalService _conditional;

    public ConditionalController(ConditionalService conditional)
    {
        _conditional = conditional;
    }

    /// <summary>
    /// 【4.1】Condition — 仅 id > 10 时缓存
    /// </summary>
    [Description("【4.1】Condition — 仅 id > 10 时缓存")]
    [HttpGet("condition/cacheable/{id}")]
    public IActionResult ConditionCacheable(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _conditional.GetUserWithCondition(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _conditional.ConditionCallCount,
            condition = "#id > 10",
            isCaching = id > 10,
            note = id > 10
                ? "Condition=true → 正常使用缓存"
                : "Condition=false → 每次调用都执行方法（不缓存）"
        });
    }

    /// <summary>
    /// 【4.2】Unless — 排除 null 结果
    /// </summary>
    [Description("【4.2】Unless — 排除 null 结果")]
    [HttpGet("condition/unless/{id}")]
    public IActionResult ConditionUnless(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _conditional.GetUserWithUnless(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _conditional.UnlessCallCount,
            unless = "#result == null",
            cached = user != null,
            note = user != null ? "结果不为 null → 写入缓存" : "结果为 null → 不缓存"
        });
    }

    /// <summary>
    /// 【4.3】Condition + Unless 组合
    /// </summary>
    [Description("【4.3】Condition + Unless 组合")]
    [HttpGet("condition/combined/{id}")]
    public IActionResult ConditionCombined(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _conditional.GetUserCombined(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _conditional.CombinedCallCount,
            condition = "#id > 0",
            unless = "#result == null || #result.Age > 40",
            shouldCache = user != null && user.Age <= 40,
            note = user != null
                ? (user.Age <= 40 ? "条件满足 → 缓存" : "年龄 > 40 → 不缓存")
                : "结果为空 → 不缓存"
        });
    }

    /// <summary>
    /// 【4.4】CachePut 条件写入
    /// </summary>
    [Description("【4.4】CachePut 条件写入")]
    [HttpPost("condition/put")]
    public IActionResult ConditionPut([FromForm] int id, [FromForm] string? name = null,
        [FromForm] int age = 0)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _conditional.ConditionalUpdateUser(new UserDto
        {
            Id = id, Name = name ?? "", Age = age, Email = $"{name ?? ""}@test.com"
        });
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _conditional.PutCallCount,
            note = name == "skip" ? "名称为 'skip' → 不缓存" : "正常写入缓存"
        });
    }

    /// <summary>
    /// 【4.5】CacheEvict + Condition 条件性驱逐
    /// </summary>
    [Description("【4.5】CacheEvict + Condition 条件性驱逐")]
    [HttpPost("condition/evict")]
    public IActionResult ConditionEvict([FromForm] int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _conditional.ConditionalEvictUser(id);
        sw.Stop();
        return Ok(new
        {
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _conditional.EvictCallCount,
            condition = "#id > 0",
            evicted = id > 0,
            note = id > 0 ? "已驱逐 condition:put 区域的缓存" : "Condition=false → 跳过缓存驱逐"
        });
    }

    /// <summary>
    /// 【4.6】复杂条件
    /// </summary>
    [Description("【4.6】复杂条件表达式")]
    [HttpGet("condition/complex/{id}")]
    public IActionResult ConditionComplex(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _conditional.GetUserComplex(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _conditional.ComplexCallCount,
            condition = "#p0 > 0 && #p0 < 100",
            unless = "#result.Email == 'skip@test.com'",
            note = user?.Email == "skip@test.com"
                ? "除非条件满足（skip邮箱）→ 不缓存"
                : "条件全部通过 → 正常缓存"
        });
    }

    /// <summary>
    /// 【4.7】异步方法 + Unless
    /// </summary>
    [Description("【4.7】异步方法 + Unless")]
    [HttpGet("condition/async-unless/{id}")]
    public async Task<IActionResult> ConditionAsyncUnless(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = await _conditional.GetUserAsyncUnless(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _conditional.ConditionAsyncUnlessCallCount,
            unless = "#result == null",
            cached = user != null,
            note = user != null ? "结果不为 null → 写入缓存" : "结果为 null → 不缓存"
        });
    }

    // ===================================================================
    // 拼写错误容错测试端点
    // ===================================================================

    /// <summary>
    /// 【4.8】Condition 拼写错误 — #res111ult 不存在 → 永远跳过缓存
    /// </summary>
    [Description("【4.8】Condition 拼写错误 — #res111ult → 永不缓存")]
    [HttpGet("condition/typo-condition/{id}")]
    public IActionResult ConditionTypoCondition(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _conditional.GetUserWithTypoInCondition(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _conditional.ErrorCorrectionCallCount,
            note = "Condition 拼写错误（#res111ult != null），每次调用都执行方法体（1.5s），永不缓存"
        });
    }

    /// <summary>
    /// 【4.9】Unless 拼写错误 — #res111ult 不存在 → 永远不写缓存
    /// </summary>
    [Description("【4.9】Unless 拼写错误 — #res111ult → 永不写缓存")]
    [HttpGet("condition/typo-unless/{id}")]
    public IActionResult ConditionTypoUnless(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _conditional.GetUserWithTypoInUnless(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _conditional.ErrorCorrectionCallCount,
            note = "Unless 拼写错误（#res111ult == null），永不写入缓存，每次调用都执行方法体"
        });
    }

    /// <summary>
    /// 【4.10】属性链根变量拼写错误 — #usre.Age → #usre 不存在 → 永远跳过缓存
    /// </summary>
    [Description("【4.10】属性链拼写错误 — #usre.Age → 永不缓存")]
    [HttpGet("condition/typo-chain/{id}")]
    public IActionResult ConditionTypoChain(int id)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var user = _conditional.GetUserWithTypoInPropertyChain(id);
        sw.Stop();
        return Ok(new
        {
            data = user,
            elapsedMs = sw.ElapsedMilliseconds,
            callCount = _conditional.ErrorCorrectionCallCount,
            note = "属性链根变量拼写错误（#usre.Age > 0），每次调用都执行方法体（1.5s），永不缓存"
        });
    }

    // ===================================================================
    // 诊断端点：条件执行次数统计
    // ===================================================================

    /// <summary>
    /// 查看 GetUserWithCondition 实际方法执行次数
    /// </summary>
    [Description("📊 GetUserWithCondition 执行次数")]
    [HttpGet("condition/call-count")]
    public IActionResult ConditionCallCount()
    {
        return Ok(new
        {
            method = "GetUserWithCondition",
            executedTimes = _conditional.ConditionCallCount,
            note = "数值不变 → 缓存命中；数值 +1 → 条件绕开缓存或缓存未命中"
        });
    }

    /// <summary>
    /// 查看 GetUserWithUnless 实际方法执行次数
    /// </summary>
    [Description("📊 GetUserWithUnless 执行次数")]
    [HttpGet("condition/unless-count")]
    public IActionResult ConditionUnlessCount()
    {
        return Ok(new
        {
            method = "GetUserWithUnless",
            executedTimes = _conditional.UnlessCallCount,
            note = "配合 condition/unless/{id} 端点验证 unless 行为"
        });
    }

    /// <summary>
    /// 查看 GetUserCombined 实际方法执行次数
    /// </summary>
    [Description("📊 GetUserCombined 执行次数")]
    [HttpGet("condition/combined-count")]
    public IActionResult ConditionCombinedCount()
    {
        return Ok(new
        {
            method = "GetUserCombined",
            executedTimes = _conditional.CombinedCallCount
        });
    }

    /// <summary>
    /// 查看 GetUserAsyncUnless 实际方法执行次数
    /// </summary>
    [Description("📊 GetUserAsyncUnless 执行次数")]
    [HttpGet("condition/async-unless-count")]
    public IActionResult ConditionAsyncUnlessCount()
    {
        return Ok(new
        {
            method = "GetUserAsyncUnless",
            executedTimes = _conditional.ConditionAsyncUnlessCallCount
        });
    }
}
