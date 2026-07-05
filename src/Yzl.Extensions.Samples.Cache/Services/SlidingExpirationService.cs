using Yzl.Extensions.Samples.Cache.Models;

namespace Yzl.Extensions.Samples.Cache.Services;

/// <summary>
/// 【滑动过期 —— Sliding Expiration】
///
/// 滑动过期是缓存系统中重要的过期策略，与固定 TTL（绝对过期）相比：
///
/// ╔══════════════════════════════════════════════════════════════════════╗
/// ║  固定 TTL（绝对过期） vs 滑动过期                                  ║
/// ║                                                                    ║
/// ║  固定 TTL (ttlSeconds):                                           ║
/// ║    数据写入缓存后，无论是否被访问，ttlSeconds 秒后一定过期。       ║
/// ║    ┌──────┬──────┬──────┬──────┬──────┬──────┐                    ║
/// ║    │写入  │      │      │过期  │      │      │   ← 到期自动删除   ║
/// ║    └──────┴──────┴──────┴──────┴──────┴──────┘                    ║
/// ║    0      30     60     90    120    150    180  (秒)             ║
/// ║    TTL=60s → 无论是否访问，60秒后过期                              ║
/// ║                                                                    ║
/// ║  滑动过期 (slidingTtl + ttlSeconds 作为绝对过期兜底):              ║
/// ║    每次访问缓存时，TTL 计时器自动重置，但不超过绝对过期时间。      ║
/// ║    ┌──────┬──访问──┬──访问──┬──────┬──────┬──────┐                ║
/// ║    │写入  │→ 重置  │→ 重置  │      │      │过期  │                ║
/// ║    └──────┴────────┴────────┴──────┴──────┴──────┘                ║
/// ║    0      10       20       80    140    200    260  (秒)         ║
/// ║    slidingTtl=30s + ttlSeconds=300s                                ║
/// ║    → 每次访问延长30s，但最长不超过300s                             ║
/// ╚══════════════════════════════════════════════════════════════════════╝
///
/// 滑动过期适合以下场景：
///   - Session 数据：用户活动期间缓存保持有效
///   - 配置信息：频繁访问的配置自动续期
///   - 用户Token：每次请求自动延长有效期
///   - 热点数据：访问越频繁，缓存越持久
/// </summary>
public class SlidingExpirationService
{
    // ===================================================================
    // 模拟数据
    // ===================================================================
    private static readonly List<UserDto> _users = new()
    {
        new() { Id = 1, Name = "Alice", Age = 25, Email = "alice@example.com" },
        new() { Id = 2, Name = "Bob", Age = 30, Email = "bob@example.com" },
    };

    private int _callCount;
    public int CallCount => _callCount;

    // ===================================================================
    // 示例 7.1: 固定 TTL（对照实验）
    // ===================================================================

    /// <summary>
    /// 【固定 TTL — 对照实验】
    ///
    /// 仅设置 ttlSeconds=10：
    ///   - 缓存写入后 10 秒内有效
    ///   - 无论在这 10 秒内访问多少次，10 秒后必定过期
    ///   - 适用于：数据变化规律明确，不需要滑动续期的场景
    /// </summary>
    [Cacheable(cacheName: "expire:fixed", key: "#id", ttlSeconds: 10)]
    public virtual UserDto? GetUserFixed(int id)
    {
        _callCount++;
        Console.WriteLine($"[固定TTL] 执行方法：id={id}，TTL=10s（绝对过期）");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 7.2: 滑动过期（slidingTtl）
    // ===================================================================

    /// <summary>
    /// 【滑动过期 — 每次访问续期】
    ///
    /// 设置 slidingTtl=30, ttlSeconds=86400：
    ///   - 写入缓存后 30 秒内有效
    ///   - 如果在 30 秒内再次访问该缓存，TTL 重置为 30 秒（续期）
    ///   - 即使不断续期，最长也不超过 ttlSeconds=86400 秒（24小时绝对上限）
    ///
    /// ⚠️ 注意：当 slidingTtl > 0 时，ttlSeconds 退化为"绝对过期兜底"。
    ///   也就是说，即使一直在访问，86400 秒后缓存也一定会过期。
    ///   这防止了"永远不过期"的热点数据堆积。
    ///
    /// 验证方法：
    ///   1. GET  /api/samples/sliding-basic/1  → 慢（第一次查询）
    ///   2. 等待 20 秒
    ///   3. GET  /api/samples/sliding-basic/1  → 快（缓存命中，TTL重置）
    ///   4. 等待 40 秒（超过 30 秒滑动窗口）
    ///   5. GET  /api/samples/sliding-basic/1  → 慢（缓存已过期）
    /// </summary>
    [Cacheable(cacheName: "expire:sliding", key: "#id", ttlSeconds: 86400, slidingTtl: 30)]
    public virtual UserDto? GetUserSliding(int id)
    {
        _callCount++;
        Console.WriteLine($"[滑动TTL] 执行方法：id={id}，slidingTtl=30s，绝对过期兜底=86400s(24h)");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 7.3: 短滑动过期（适合高频率更新数据）
    // ===================================================================

    /// <summary>
    /// 【短滑动过期 — 高频率更新场景】
    ///
    /// slidingTtl=5, ttlSeconds=60：
    ///   - 每次访问续期 5 秒
    ///   - 如果 5 秒内无人访问，缓存自动过期
    ///   - 绝对上限 60 秒
    ///
    /// 适用于：
    ///   - 实时排行榜数据
    ///   - 股票价格/加密货币行情
    ///   - 在线用户状态
    /// </summary>
    [Cacheable(cacheName: "expire:hot", key: "#id", ttlSeconds: 60, slidingTtl: 5)]
    public virtual UserDto? GetUserHotData(int id)
    {
        _callCount++;
        Console.WriteLine($"[短滑动] 执行方法：id={id}，slidingTtl=5s（频繁续期）");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 7.4: CachePut + Sliding
    // ===================================================================

    /// <summary>
    /// 【CachePut + 滑动过期】
    ///
    /// CachePut 同样支持 slidingTtl：
    ///   更新缓存时写入新数据，并设置滑动过期策略。
    /// </summary>
    [CachePut(cacheName: "expire:sliding", key: "#user.Id", ttlSeconds: 86400, slidingTtl: 30)]
    public virtual UserDto? UpdateUser(UserDto user)
    {
        _callCount++;
        Console.WriteLine($"[CachePut+滑动] 更新用户：id={user.Id}");
        Thread.Sleep(500);
        return user;
    }
}
