using Yzl.Extensions.Samples.Cache.Models;

namespace Yzl.Extensions.Samples.Cache.Services;

/// <summary>
/// 【CachePut 和 CacheEvict —— 缓存更新与驱逐】
///
/// 完整的缓存生命周期管理包含三个操作：
///   1. [Cacheable]   — 读取缓存（缓存未命中时执行方法并写入）
///   2. [CachePut]    — 更新缓存（方法始终执行，结果始终写入缓存）
///   3. [CacheEvict]  — 驱逐缓存（从缓存中删除指定条目）
///
/// ╔══════════════════════════════════════════════════════════════╗
/// ║ [Cacheable] vs [CachePut] 的核心区别：                     ║
/// ║                                                            ║
/// ║ [Cacheable]                                                ║
/// ║   ├─ 缓存命中 → 不执行方法，直接返回缓存                   ║
/// ║   └─ 缓存未命中 → 执行方法，写入缓存，返回结果             ║
/// ║                                                            ║
/// ║ [CachePut]                                                 ║
/// ║   └─ 始终执行方法，始终将结果写入缓存                      ║
/// ║       （用于更新操作，保证缓存与数据源同步）                ║
/// ╚══════════════════════════════════════════════════════════════╝
/// </summary>
public class CacheLifecycleService
{
    // ===================================================================
    // 模拟数据源
    // ===================================================================
    private static readonly List<UserDto> _users = new()
    {
        new() { Id = 1, Name = "Alice", Age = 25, Email = "alice@example.com",
                CreatedAt = new DateTime(2024, 1, 1) },
        new() { Id = 2, Name = "Bob", Age = 30, Email = "bob@example.com",
                CreatedAt = new DateTime(2024, 1, 2) },
        new() { Id = 3, Name = "Charlie", Age = 35, Email = "charlie@example.com",
                CreatedAt = new DateTime(2024, 1, 3) },
    };

    private int _callCount;

    /// <summary>
    /// 获取服务方法总调用次数（仅统计实际执行的方法，不含缓存命中的请求）
    /// </summary>
    public int CallCount => _callCount;

    // ===================================================================
    // 示例 2.1: 查询用户（Cacheable）
    // ===================================================================

    /// <summary>
    /// 【Cacheable — 查询】
    ///
    /// 标准的读取操作。
    /// 缓存已存在时直接从缓存返回，否则查询数据源并写入缓存。
    /// </summary>
    [Cacheable(cacheName: "lifecycle", key: "#id", ttlSeconds: 300)]
    public virtual UserDto? GetUser(int id)
    {
        _callCount++;
        Thread.Sleep(1500); // 模拟数据库查询
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 2.2: 更新用户（CachePut）
    // ===================================================================

    /// <summary>
    /// 【CachePut — 更新】
    ///
    /// ⚠️ 注意：与 [Cacheable] 不同，CachePut 总是执行方法体！
    /// 即使缓存中已有数据，也会重新执行并刷新缓存。
    ///
    /// 使用场景：
    ///   - 更新数据库记录后同步更新缓存
    ///   - 需要保证缓存与数据库一致的操作
    ///
    /// 适用场景举例：
    ///   - 修改用户信息后，更新缓存中的用户数据
    ///   - 商品价格调整后，刷新缓存中的价格信息
    ///
    /// ⚠️ 注意：
    /// 并不是所有 [CachePut] 方法都需要返回值。
    /// 若方法返回 void/Task，则 CachePut 不写入缓存。
    /// 若方法有返回值，则结果会写入缓存。
    /// </summary>
    [CachePut(cacheName: "lifecycle", key: "#user.Id", ttlSeconds: 300)]
    public virtual UserDto? UpdateUser(UserDto user)
    {
        _callCount++;
        Thread.Sleep(500); // 模拟数据库更新

        var existing = _users.FirstOrDefault(u => u.Id == user.Id);
        if (existing != null)
        {
            existing.Name = user.Name;
            existing.Age = user.Age;
            existing.Email = user.Email;
            existing.UpdatedAt = DateTime.Now;
            return existing;
        }

        // 如果是新用户，添加到列表
        _users.Add(user);
        return user;
    }

    // ===================================================================
    // 示例 2.3: 删除用户（CacheEvict — 单条）
    // ===================================================================

    /// <summary>
    /// 【CacheEvict — 删除单条缓存】
    ///
    /// CacheEvict 用于在数据变更时清除缓存，保证下次读取时获取最新数据。
    ///
    /// 当执行 DeleteUser(1) 时：
    ///   1. 先执行方法体（从数据源中删除用户）
    ///   2. 再清除缓存中键为 "lifecycle:1" 的条目
    ///
    /// ⚠️ 注意：CacheEvict 的 cacheName 是必需参数！
    ///   CacheEvictAttribute(string cacheName, ...)
    ///   必须与对应的 Cacheable 使用相同的 cacheName。
    ///
    /// 执行顺序：先执行方法 → 再驱逐缓存
    /// 即使方法抛出异常，缓存驱逐也会执行。
    /// </summary>
    [CacheEvict(cacheName: "lifecycle", key: "#id")]
    public virtual void DeleteUser(int id)
    {
        _callCount++;
        Thread.Sleep(300); // 模拟数据库删除

        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user != null)
        {
            _users.Remove(user);
            Console.WriteLine($"[DeleteUser] 已从数据源删除用户 ID={id}");
        }
    }

    // ===================================================================
    // 示例 2.4: 全量刷新（CachePut 重新加载）
    // ===================================================================

    /// <summary>
    /// 【CachePut — 刷新缓存】
    ///
    /// 如果数据源发生了变化（例如其他系统修改了数据），
    /// 可以通过 CachePut 强制刷新指定缓存。
    ///
    /// 本示例模拟重新从数据库加载用户信息并更新缓存。
    /// </summary>
    [CachePut(cacheName: "lifecycle", key: "#id", ttlSeconds: 300)]
    public virtual UserDto? RefreshUser(int id)
    {
        _callCount++;
        Console.WriteLine($"[RefreshUser] 从数据库重新加载用户 ID={id}");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }
}
