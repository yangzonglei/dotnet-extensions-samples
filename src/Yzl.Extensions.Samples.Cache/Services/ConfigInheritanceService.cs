using Yzl.Extensions.Samples.Cache.Models;

namespace Yzl.Extensions.Samples.Cache.Services;

/// <summary>
/// 【CacheConfig —— 类级缓存配置继承】
///
/// [CacheConfig] 是类级别的注解，用于定义该类中所有缓存方法的默认配置。
/// 当方法上的 [Cacheable] / [CachePut] 未指定某些属性时，
/// 框架会自动从类上的 [CacheConfig] 继承这些属性。
///
/// ╔══════════════════════════════════════════════════════════════════╗
/// ║  配置继承优先级（从高到低）：                                  ║
/// ║                                                                ║
/// ║  1. 方法上的 [Cacheable]/[CachePut] 显式指定值                ║
/// ║  2. 类上的 [CacheConfig] 默认值                                ║
/// ║  3. 特性构造函数的硬编码默认值                                 ║
/// ║                                                                ║
/// ║  [CacheConfig] 可配置的属性：                                  ║
/// ║  ├─ DefaultCacheName           — 默认缓存区域名称              ║
/// ║  ├─ DefaultCacheType           — 默认缓存类型（Memory/Redis） ║
/// ║  ├─ DefaultTtlSeconds          — 默认 TTL 过期时间（秒）       ║
/// ║  └─ DefaultSlidingExpirationSeconds — 默认滑动过期时间（秒）  ║
/// ║                                                                ║
/// ║  注意：缓存键（Key）不会被继承，每个方法必须自行指定！        ║
/// ╚══════════════════════════════════════════════════════════════════╝
/// </summary>
[CacheConfig(defaultCacheName: "config-demo", defaultTtlSeconds: 120, defaultCacheType: CacheType.Memory)]
public class ConfigInheritanceService
{
    // ===================================================================
    // 模拟数据
    // ===================================================================
    private static readonly List<UserDto> _users = new()
    {
        new() { Id = 1, Name = "Alice", Age = 25, Email = "alice@example.com" },
        new() { Id = 2, Name = "Bob", Age = 30, Email = "bob@example.com" },
        new() { Id = 10, Name = "CacheTest", Age = 22, Email = "cache@test.com" },
    };

    private int _callCount;
    public int CallCount => _callCount;

    // ===================================================================
    // 示例 5.1: 完全继承 CacheConfig
    // ===================================================================

    /// <summary>
    /// 【完全继承默认配置】
    ///
    /// 本方法没有指定 cacheName 和 ttlSeconds，因此：
    ///   ├─ cacheName  → 继承 [CacheConfig] 的 "config-demo"
    ///   └─ ttlSeconds → 继承 [CacheConfig] 的 120 秒
    ///
    /// 最终的缓存键为：config-demo:1（当 id=1 时）
    /// </summary>
    [Cacheable(key: "#id")]
    public virtual UserDto? GetDefault(int id)
    {
        _callCount++;
        Console.WriteLine($"[完全继承] cacheName=继承(config-demo), ttl=继承(120s)");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 5.2: 部分覆盖 — 自定义缓存区域
    // ===================================================================

    /// <summary>
    /// 【部分覆盖：自定义 cacheName，继承 ttl】
    ///
    /// 本方法覆盖了 cacheName，但未指定 ttlSeconds：
    ///   ├─ cacheName  → "custom-name"（覆盖默认值）
    ///   └─ ttlSeconds → 继承 [CacheConfig] 的 120 秒
    ///
    /// 适用于：同一类中需要将不同方法路由到不同缓存区域的场景。
    /// </summary>
    [Cacheable(cacheName: "custom-name", key: "#id")]
    public virtual UserDto? GetWithCustomCacheName(int id)
    {
        _callCount++;
        Console.WriteLine($"[部分覆盖] cacheName=custom-name(覆盖), ttl=继承(120s)");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 5.3: 部分覆盖 — 自定义 TTL
    // ===================================================================

    /// <summary>
    /// 【部分覆盖：继承 cacheName，自定义 ttl】
    ///
    /// 本方法覆盖了 ttlSeconds，但未指定 cacheName：
    ///   ├─ cacheName  → 继承 [CacheConfig] 的 "config-demo"
    ///   └─ ttlSeconds → 30 秒（覆盖默认值 120 秒）
    ///
    /// 适用于：大部分方法使用标准 TTL，但少数热点数据需要更短 TTL。
    /// </summary>
    [Cacheable(key: "#id", ttlSeconds: 30)]
    public virtual UserDto? GetWithCustomTtl(int id)
    {
        _callCount++;
        Console.WriteLine($"[部分覆盖] cacheName=继承(config-demo), ttl=30s(覆盖)");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 5.4: 完全覆盖所有配置
    // ===================================================================

    /// <summary>
    /// 【完全覆盖：方法级别全量配置】
    ///
    /// 本方法完全覆盖了 [CacheConfig] 的所有默认值：
    ///   ├─ cacheName  → "fully-custom"
    ///   ├─ ttlSeconds → 600 秒
    ///   └─ slidingTtl → 60 秒
    ///
    /// 此时 [CacheConfig] 的默认值对本方法完全无效。
    /// 适用于：少数特殊方法需要完全不同的缓存策略。
    /// </summary>
    [Cacheable(cacheName: "fully-custom", key: "#id", ttlSeconds: 600, slidingTtl: 60)]
    public virtual UserDto? GetFullyCustom(int id)
    {
        _callCount++;
        Console.WriteLine($"[完全覆盖] cacheName=fully-custom, ttl=600s, sliding=60s");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 5.5: 类级别的 CachePut 默认配置
    // ===================================================================

    /// <summary>
    /// 【CachePut 也继承 CacheConfig】
    ///
    /// [CachePut] 同样会继承类级别的 [CacheConfig]：
    ///   ├─ cacheName  → 继承 "config-demo"
    ///   └─ ttlSeconds → 继承 120 秒
    /// </summary>
    [CachePut(key: "#user.Id")]
    public virtual UserDto? UpdateUser(UserDto user)
    {
        _callCount++;
        var existing = _users.FirstOrDefault(u => u.Id == user.Id);
        if (existing != null)
        {
            existing.Name = user.Name;
            existing.Age = user.Age;
            existing.UpdatedAt = DateTime.Now;
        }
        Console.WriteLine($"[CachePut继承] cacheName=继承(config-demo), key=#{user.Id}");
        return existing ?? user;
    }
}
