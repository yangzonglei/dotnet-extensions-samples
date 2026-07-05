using Yzl.Extensions.Samples.Cache.Models;

namespace Yzl.Extensions.Samples.Cache.Services;

/// <summary>
/// 【Redis 缓存 —— 使用 Redis 作为缓存后端】
///
/// 框架支持 Memory 和 Redis 两种缓存后端，通过 cacheType 参数切换。
///
/// ╔══════════════════════════════════════════════════════════════════════╗
/// ║  Memory vs Redis 对比                                              ║
/// ║                                                                    ║
/// ║  Memory（内存缓存）：                                              ║
/// ║    ├─ 数据存储在应用程序进程内存中                                 ║
/// ║    ├─ 访问速度最快（微秒级）                                      ║
/// ║    ├─ 应用重启后缓存丢失                                          ║
/// ║    ├─ 多实例部署下缓存不共享                                      ║
/// ║    └─ 适合：单实例应用、开发环境、非关键数据                      ║
/// ║                                                                    ║
/// ║  Redis（分布式缓存）：                                             ║
/// ║    ├─ 数据存储在独立的 Redis 服务器中                              ║
/// ║    ├─ 访问速度较快（毫秒级）                                      ║
/// ║    ├─ 应用重启后缓存保留                                          ║
/// ║    ├─ 多实例部署下缓存共享                                        ║
/// ║    ├─ 支持数据持久化                                              ║
/// ║    └─ 适合：多实例部署、生产环境、需要共享缓存的场景              ║
/// ╚══════════════════════════════════════════════════════════════════════╝
///
/// ⚠️ 使用 Redis 缓存需要：
///   1. 在 appsettings.Development.json 中配置 Redis 连接字符串
///   2. 在 Program.cs 中调用 AddEnableCaching(enableRedis: true, ...)
///   3. 在 [Cacheable] 上设置 cacheType: CacheType.Redis
/// </summary>
public class RedisCacheService
{
    // ===================================================================
    // 模拟数据
    // ===================================================================
    private static readonly List<UserDto> _users = new()
    {
        new() { Id = 1, Name = "RedisAlice", Age = 25, Email = "redis@example.com" },
        new() { Id = 2, Name = "RedisBob", Age = 30, Email = "redis_bob@example.com" },
    };

    private int _callCount;
    public int CallCount => _callCount;

    // ===================================================================
    // 示例 8.1: 基础 Redis 缓存
    // ===================================================================

    /// <summary>
    /// 【基础 Redis 缓存】
    ///
    /// 使用 cacheType: CacheType.Redis 将缓存存储到 Redis。
    /// 其他所有特性（SpEL、TTL、Condition 等）与 Memory 缓存完全一致。
    ///
    /// 缓存键在 Redis 中的存储格式：
    ///   {cacheName}:{key}
    ///   例如：redis:users:1
    ///
    /// 可以通过 redis-cli 直接查看 Redis 中的数据：
    ///   $ redis-cli
    ///   > keys redis:users:*
    ///   > get redis:users:1
    ///   > ttl redis:users:1
    /// </summary>
    [Cacheable(cacheName: "redis:users", key: "#id", ttlSeconds: 300, cacheType: CacheType.Redis)]
    public virtual UserDto? GetUser(int id)
    {
        _callCount++;
        Console.WriteLine($"[Redis缓存] 执行数据库查询：id={id}（结果将缓存到 Redis）");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 8.2: Redis + 滑动过期
    // ===================================================================

    /// <summary>
    /// 【Redis + 滑动过期】
    ///
    /// Redis 同样支持滑动过期策略。
    /// Redis 内部使用 EXPIRE 命令实现滑动 TTL。
    ///
    /// ttlSeconds=86400, slidingTtl=300：
    ///   - 每次读取时 Redis 自动续期 300 秒（5 分钟）
    ///   - 最长不超过 86400 秒（24 小时）
    ///
    /// 适用于：
    ///   - 用户 Session 数据（多实例共享）
    ///   - API 限流计数器
    ///   - 分布式锁状态
    /// </summary>
    [Cacheable(cacheName: "redis:sliding", key: "#id", ttlSeconds: 86400, slidingTtl: 300,
               cacheType: CacheType.Redis)]
    public virtual UserDto? GetUserSliding(int id)
    {
        _callCount++;
        Console.WriteLine($"[Redis+滑动] 执行查询：id={id}（Redis+slidingTtl=300s）");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 8.3: Redis CachePut
    // ===================================================================

    /// <summary>
    /// 【Redis CachePut】
    ///
    /// CachePut 配合 Redis 使用，更新数据库后同步更新 Redis 缓存。
    ///
    /// 适用于多实例部署场景：
    ///   实例 A 更新了用户数据 → 同时更新 Redis 缓存
    ///   实例 B 读取同一用户 → 从 Redis 获取最新数据
    ///   保证了多实例间的缓存一致性
    /// </summary>
    [CachePut(cacheName: "redis:users", key: "#user.Id", ttlSeconds: 300,
              cacheType: CacheType.Redis)]
    public virtual UserDto? UpdateUser(UserDto user)
    {
        _callCount++;
        Console.WriteLine($"[Redis CachePut] 更新用户：id={user.Id}（同步更新 Redis）");
        Thread.Sleep(500);

        var existing = _users.FirstOrDefault(u => u.Id == user.Id);
        if (existing != null)
        {
            existing.Name = user.Name;
            existing.Age = user.Age;
            existing.Email = user.Email;
            existing.UpdatedAt = DateTime.Now;
            return existing;
        }
        _users.Add(user);
        return user;
    }

    // ===================================================================
    // 示例 8.4: Redis CacheEvict
    // ===================================================================

    /// <summary>
    /// 【Redis CacheEvict】
    ///
    /// 从 Redis 中删除指定缓存条目。
    /// </summary>
    [CacheEvict(cacheName: "redis:users", key: "#id", cacheType: CacheType.Redis)]
    public virtual void DeleteUser(int id)
    {
        _callCount++;
        Console.WriteLine($"[Redis CacheEvict] 删除用户缓存：id={id}（从 Redis 中移除）");
        Thread.Sleep(200);

        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user != null)
        {
            _users.Remove(user);
        }
    }

    // ===================================================================
    // 示例 8.5: Memory 和 Redis 混合使用
    // ===================================================================

    /// <summary>
    /// 【Memory + Redis 混合缓存策略】
    ///
    /// 同一个服务中可以混合使用 Memory 和 Redis 缓存。
    ///
    /// 推荐策略：
    ///   - 本地高频读数据（字典、配置）→ Memory（速度快）
    ///   - 跨实例共享数据（用户信息、订单）→ Redis（共享一致）
    ///
    /// 本方法使用 Memory 缓存（默认），与上面的 Redis 方法形成对比。
    /// </summary>
    [Cacheable(cacheName: "memory:users", key: "#id", ttlSeconds: 60, cacheType: CacheType.Memory)]
    public virtual UserDto? GetUserFromMemory(int id)
    {
        _callCount++;
        Console.WriteLine($"[Memory缓存] 执行查询：id={id}（结果缓存在进程内存中）");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }
}
