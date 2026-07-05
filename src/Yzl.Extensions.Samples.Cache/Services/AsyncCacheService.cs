using Yzl.Extensions.Samples.Cache.Models;

namespace Yzl.Extensions.Samples.Cache.Services;

/// <summary>
/// 【异步缓存 —— async/await 方法支持】
///
/// 框架完全支持异步方法（async Task<T>）的缓存。
/// 与同步方法的使用方式完全一致，框架会自动处理 Task 的等待和结果提取。
///
/// ╔══════════════════════════════════════════════════════════════╗
/// ║  异步缓存注意事项：                                        ║
/// ║                                                            ║
/// ║  1. async 方法的缓存行为与同步方法完全一致                 ║
/// ║     - 缓存命中时跳过方法执行，直接返回 Task<T>             ║
/// ║     - 缓存未命中时执行方法，await 后写入缓存               ║
/// ║                                                            ║
/// ║  2. 支持所有 TTL 和 SpEL 特性                              ║
/// ║     - ttlSeconds、slidingTtl                              ║
/// ║     - Condition、Unless                                     ║
/// ║     - CachePut、CacheEvict                                  ║
/// ║                                                            ║
/// ║  3. 返回值类型必须与方法的声明一致                         ║
/// ║     - Task<T> → 缓存 T 类型                                ║
/// ║     - Task<List<T>> → 缓存 List<T>                        ║
/// ╚══════════════════════════════════════════════════════════════╝
/// </summary>
public class AsyncCacheService
{
    // ===================================================================
    // 模拟数据
    // ===================================================================
    private static readonly List<UserDto> _users = new()
    {
        new() { Id = 100, Name = "AsyncAlice", Age = 25, Email = "async@example.com" },
        new() { Id = 200, Name = "AsyncBob", Age = 30, Email = "async_bob@example.com" },
        new() { Id = 300, Name = "AsyncCharlie", Age = 35, Email = "async_charlie@example.com" },
    };

    private int _callCount;

    /// <summary>
    /// 获取异步方法实际调用次数
    /// </summary>
    public int CallCount => _callCount;

    // ===================================================================
    // 示例 6.1: 基础异步缓存
    // ===================================================================

    /// <summary>
    /// 【异步 Cacheable】
    ///
    /// 与同步版本的 [Cacheable] 用法完全相同。
    /// 框架会自动 await Task 然后缓存实际的结果值。
    ///
    /// 适用于：
    ///   - EF Core 的异步查询（ToListAsync, FirstOrDefaultAsync）
    ///   - HttpClient 的异步请求
    ///   - 文件 I/O 操作
    ///   - 任何 async/await 场景
    /// </summary>
    [Cacheable(cacheName: "async:users", key: "#id", ttlSeconds: 60)]
    public virtual async Task<UserDto?> GetUserAsync(int id)
    {
        _callCount++;
        Console.WriteLine($"[异步Cacheable] 开始执行异步查询：id={id}");
        // 模拟异步数据库查询（如 EF Core 的 FindAsync）
        await Task.Delay(1500);
        Console.WriteLine($"[异步Cacheable] 查询完成：id={id}");
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 6.2: 异步 CachePut
    // ===================================================================

    /// <summary>
    /// 【异步 CachePut】
    ///
    /// 异步的缓存更新操作。
    /// 方法始终执行，结果始终写入缓存。
    /// </summary>
    [CachePut(cacheName: "async:users", key: "#user.Id", ttlSeconds: 60)]
    public virtual async Task<UserDto?> UpdateUserAsync(UserDto user)
    {
        _callCount++;
        Console.WriteLine($"[异步CachePut] 开始执行异步更新：id={user.Id}");
        await Task.Delay(500); // 模拟异步数据库更新

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
    // 示例 6.3: 异步 CacheEvict
    // ===================================================================

    /// <summary>
    /// 【异步 CacheEvict】
    ///
    /// 异步的缓存驱逐操作。
    /// </summary>
    [CacheEvict(cacheName: "async:users", key: "#id")]
    public virtual async Task DeleteUserAsync(int id)
    {
        _callCount++;
        Console.WriteLine($"[异步CacheEvict] 开始执行异步删除：id={id}");
        await Task.Delay(300); // 模拟异步数据库删除

        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user != null)
        {
            _users.Remove(user);
            Console.WriteLine($"[异步CacheEvict] 已删除用户：id={id}");
        }
    }

    // ===================================================================
    // 示例 6.4: 异步 + 短 TTL + Unless
    // ===================================================================

    /// <summary>
    /// 【异步 + 短 TTL + Unless 组合】
    ///
    /// 异步方法同样支持 Condition、Unless、slidingTtl 等所有特性。
    ///
    /// 本例：TTL=10秒，排除 null 结果的缓存
    /// </summary>
    [Cacheable(cacheName: "async:hot", key: "#id", ttlSeconds: 10,
               Unless = "#result == null")]
    public virtual async Task<UserDto?> GetHotUserAsync(int id)
    {
        _callCount++;
        Console.WriteLine($"[异步热点] 执行异步查询：id={id}");
        await Task.Delay(1000);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 6.5: 异步批量查询（返回集合）
    // ===================================================================

    /// <summary>
    /// 【异步批量查询 + 缓存】
    ///
    /// 返回集合（List<T>）的异步方法同样支持缓存。
    /// 适用于：
    ///   - 首页推荐列表
    ///   - 下拉选项数据
    ///   - 报表统计数据
    /// </summary>
    [Cacheable(cacheName: "async:list", key: "'all'", ttlSeconds: 30)]
    public virtual async Task<List<UserDto>> GetAllUsersAsync()
    {
        _callCount++;
        Console.WriteLine($"[异步列表] 执行批量查询（此操作应只执行一次，之后从缓存读取）");
        await Task.Delay(2000);
        return _users.ToList();
    }
}
