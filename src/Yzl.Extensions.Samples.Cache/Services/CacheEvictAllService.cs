using Yzl.Extensions.Samples.Cache.Models;

namespace Yzl.Extensions.Samples.Cache.Services;

/// <summary>
/// 【CacheEvict AllEntries —— 批量清除缓存】
///
/// 除了逐条清除缓存外，CacheEvict 还支持通过 allEntries = true
/// 批量清除整个缓存区域（cacheName）中的所有条目。
///
/// ╔══════════════════════════════════════════════════════════════════════╗
/// ║  逐条清除 vs 批量清除                                              ║
/// ║                                                                    ║
/// ║  逐条清除：                                                        ║
/// ║    [CacheEvict(cacheName: "users", key: "#id")]                    ║
/// ║    → 只删除 "users:1" 这一个缓存条目                               ║
/// ║    → 其他用户的缓存保持不变                                        ║
/// ║                                                                    ║
/// ║  批量清除（AllEntries）：                                           ║
/// ║    [CacheEvict(cacheName: "users", allEntries: true)]              ║
/// ║    → 删除 "users:" 前缀下的所有缓存条目                            ║
/// ║    → 所有用户的缓存都会被清除                                      ║
/// ╚══════════════════════════════════════════════════════════════════════╝
///
/// 使用场景：
///   - 批量导入数据后清除所有相关缓存
///   - 全量数据更新（如凌晨的数据同步任务）
///   - 数据结构变更后重建缓存
///   - 管理员手动刷新缓存操作
/// </summary>
public class CacheEvictAllService
{
    // ===================================================================
    // 模拟数据
    // ===================================================================
    private static readonly List<UserDto> _users = new()
    {
        new() { Id = 1, Name = "Alice", Age = 25, Email = "alice@example.com" },
        new() { Id = 2, Name = "Bob", Age = 30, Email = "bob@example.com" },
        new() { Id = 3, Name = "Charlie", Age = 35, Email = "charlie@example.com" },
    };

    private int _callCount;
    public int CallCount => _callCount;

    // ===================================================================
    // 示例 9.1: 查询用户（写入缓存）
    // ===================================================================

    /// <summary>
    /// 【写入缓存】
    ///
    /// 供其他方法演示缓存逐条清除和批量清除的差异。
    /// </summary>
    [Cacheable(cacheName: "evict-all", key: "#id", ttlSeconds: 300)]
    public virtual UserDto? GetUser(int id)
    {
        _callCount++;
        Console.WriteLine($"[缓存写入] 执行查询：id={id}（缓存到 evict-all 区域）");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 9.2: 逐条清除缓存（key）
    // ===================================================================

    /// <summary>
    /// 【CacheEvict — 逐条清除】
    ///
    /// 只清除指定 key 的缓存条目。
    /// 例如清除 "evict-all:3" 后，"evict-all:1" 和 "evict-all:2" 仍然有效。
    ///
    /// 适用于：
    ///   - 单个用户数据变更后清除该用户的缓存
    ///   - 不影响其他用户的缓存数据
    /// </summary>
    [CacheEvict(cacheName: "evict-all", key: "#id")]
    public virtual void EvictSingle(int id)
    {
        _callCount++;
        Console.WriteLine($"[逐条清除] 清除缓存：evict-all:{id}（其他用户的缓存不受影响）");
        Thread.Sleep(200);
    }

    // ===================================================================
    // 示例 9.3: 批量清除整个区域的缓存（allEntries）
    // ===================================================================

    /// <summary>
    /// 【CacheEvict — 批量清除 allEntries】
    ///
    /// 清除 "evict-all" 缓存区域下的所有条目。
    /// 执行后：
    ///   - evict-all:1 → 已清除
    ///   - evict-all:2 → 已清除
    ///   - evict-all:3 → 已清除
    ///   所有缓存在下次查询时都需要重新从数据源加载。
    ///
    /// 注意：当 allEntries = true 时，key 参数会被忽略。
    ///
    /// 适用于：
    ///   - 全量数据库导入/同步后
    ///   - 定时刷新缓存任务
    ///   - 管理员手动清空缓存操作
    /// </summary>
    [CacheEvict(cacheName: "evict-all", allEntries: true)]
    public virtual void EvictAll()
    {
        _callCount++;
        Console.WriteLine("[批量清除] 清除 'evict-all' 区域下的所有缓存条目！");
        Console.WriteLine("[批量清除] 下次查询任何用户时都将重新从数据库加载。");
        Thread.Sleep(500);
    }

    // ===================================================================
    // 示例 9.4: CacheEvictAll 在 Redis 上的应用
    // ===================================================================

    /// <summary>
    /// 【CacheEvict AllEntries — Redis 版本】
    ///
    /// allEntries 同样支持 Redis 后端，会清除 Redis 中指定前缀的所有键。
    /// </summary>
    [CacheEvict(cacheName: "redis:evict-all", allEntries: true, cacheType: CacheType.Redis)]
    public virtual void EvictAllFromRedis()
    {
        _callCount++;
        Console.WriteLine("[Redis批量清除] 清除 Redis 中 'redis:evict-all' 前缀的所有缓存！");
        Thread.Sleep(500);
    }

    // ===================================================================
    // 示例 9.5: 逐条清除 + Condition
    // ===================================================================

    /// <summary>
    /// 【CacheEvict + Condition 条件性清除】
    ///
    /// 只有条件满足时才执行缓存清除。
    /// 本例：只有 id > 0 时才清除缓存。
    ///
    /// 适用于：
    ///   - 只在特定条件下清除缓存（如数据变更成功时）
    ///   - 防止无效的缓存清除操作
    /// </summary>
    [CacheEvict(cacheName: "evict-all", key: "#id", Condition = "#id > 0")]
    public virtual void ConditionalEvict(int id)
    {
        _callCount++;
        Console.WriteLine($"[条件清除] id={id} > 0 = true，已清除 evict-all:{id} 的缓存");
        Thread.Sleep(200);
    }
}
