using Yzl.Extensions.Samples.Cache.Models;

namespace Yzl.Extensions.Samples.Cache.Services;

/// <summary>
/// 【基础用法 —— [Cacheable] 缓存注解】
///
/// [Cacheable] 是缓存框架最核心的特性。
/// 当方法被标记为 [Cacheable] 时：
///   1. 框架会根据 key 先去缓存中查找数据
///   2. 如果缓存命中，直接返回缓存数据，跳过方法执行
///   3. 如果缓存未命中，执行方法体，将返回值存入缓存后再返回
///
/// 本服务演示 [Cacheable] 最基础的使用方式，包括：
///   - 简单 ID 缓存键（基础用法）
///   - 自定义 TTL 过期时间
///   - 多种数据模型的缓存
/// </summary>
public class BasicCacheService
{
    // ===================================================================
    // 模拟数据源（实际项目中替换为数据库查询）
    // ===================================================================
    private static readonly List<UserDto> _users = new()
    {
        new() { Id = 1, Name = "Alice", Age = 25, Email = "alice@example.com" },
        new() { Id = 2, Name = "Bob", Age = 30, Email = "bob@example.com" },
        new() { Id = 3, Name = "Charlie", Age = 35, Email = "charlie@example.com" },
        new() { Id = 4, Name = "Diana", Age = 28, Email = "diana@example.com" },
    };

    private static readonly List<ProductDto> _products = new()
    {
        new() { Id = 1, Name = "笔记本电脑", Price = 5999m, Stock = 100, Category = "电子产品" },
        new() { Id = 2, Name = "机械键盘", Price = 399m, Stock = 200, Category = "外设" },
    };

    // 用于统计方法实际被调用的次数（验证缓存是否生效）
    private int _getUserCallCount;
    private int _getProductCallCount;
    private int _getUserLegacyCallCount;

    /// <summary>
    /// 获取用户调用次数（用于验证缓存命中率）
    /// </summary>
    public int GetUserCallCount => _getUserCallCount;

    /// <summary>
    /// 获取产品调用次数（用于验证缓存命中率）
    /// </summary>
    public int GetProductCallCount => _getProductCallCount;

    /// <summary>
    /// 获取传统简写属性测试调用次数
    /// </summary>
    public int GetUserLegacyCallCount => _getUserLegacyCallCount;

    // ===================================================================
    // 示例 1.1: 基础缓存 —— 使用 ID 作为缓存键
    // ===================================================================

    /// <summary>
    /// 【基本用法：Cacheable + 简单 SpEL 键】
    ///
    /// cacheName: "users"          —— 缓存区域名称，类似于 Redis 的 key 前缀
    /// key: "#id"                  —— SpEL 表达式，引用方法参数 id 的值
    /// ttlSeconds: 60              —— 缓存过期时间（秒），60 秒后自动失效
    ///
    /// 最终的缓存键格式为：users:{id}
    /// 例如：GetUser(1) 的缓存键为 "users:1"
    ///
    /// 执行流程：
    ///   1. 首次调用 GetUser(1) → 缓存未命中 → 执行方法体（模拟 1.5 秒） → 结果写入缓存 → 返回
    ///   2. 再次调用 GetUser(1) → 缓存命中 → 跳过方法体 → 直接返回缓存结果
    ///   3. 60 秒后缓存过期 → 再次调用 GetUser(1) → 回到步骤 1
    /// </summary>
    [Cacheable(cacheName: "users", key: "#id", ttlSeconds: 60)]
    public virtual UserDto? GetUser(int id)
    {
        _getUserCallCount++;
        // 模拟耗时的数据库查询（如：EF Core 查询）
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 1.2: 短 TTL 缓存 —— 适用于频繁变化的数据
    // ===================================================================

    /// <summary>
    /// 【短 TTL：适用于频繁变化的数据】
    ///
    /// TTL = 10 秒，非常适合以下场景：
    ///   - 热点数据的短时间缓存
    ///   - 需要快速反映数据库变化的场景
    ///   - 高并发读取但写入频繁的数据
    ///
    /// 验证方法：10 秒内反复请求同一 ID，观察响应时间变化
    ///   第一次 → 慢（约 1.5 秒）
    ///   后续请求 → 快（毫秒级返回）
    ///   10 秒后 → 又变慢（缓存过期）
    /// </summary>
    [Cacheable(cacheName: "users", key: "#id", ttlSeconds: 10)]
    public virtual UserDto? GetUserShortTtl(int id)
    {
        _getUserCallCount++;
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 1.3: 长 TTL 缓存 —— 适用于基本不变的数据
    // ===================================================================

    /// <summary>
    /// 【长 TTL：适用于基本不变的数据】
    ///
    /// TTL = 86400 秒 = 24 小时，适用于：
    ///   - 字典数据（省市区、分类信息）
    ///   - 配置信息
    ///   - 用户基础信息（姓名、头像等）
    ///
    /// 也可以使用 TtlEnum 中的常量，更加语义化：
    ///   ttlSeconds: (int)TtlEnum.OneDay
    /// </summary>
    [Cacheable(cacheName: "products", key: "#id", ttlSeconds : 86400)]
    public virtual ProductDto? GetProduct(int id)
    {
        _getProductCallCount++;
        Thread.Sleep(1000);
        return _products.FirstOrDefault(p => p.Id == id);
    }

    // ===================================================================
    // 示例 1.4: 字符串缓存键 —— 非数字类型的键
    // ===================================================================

    /// <summary>
    /// 【字符串缓存键：通过 Name 字段查询】
    ///
    /// 缓存键不仅限于数字 ID，也可以是字符串、枚举等任何类型。
    /// 这里使用用户姓名作为缓存键：
    ///   key: "#name"
    ///
    /// 注意：缓存的键值最终会转为字符串，所以 "Alice" 和 "alice" 是不同的键。
    /// </summary>
    [Cacheable(cacheName: "users:name", key: "#name", ttlSeconds: 60)]
    public virtual UserDto? GetUserByName(string name)
    {
        _getUserCallCount++;
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u =>
            u.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    // ===================================================================
    // 示例 1.5: 组合键 —— 多条件查询
    // ===================================================================

    /// <summary>
    /// 【多参数组合键】
    ///
    /// 支持多个参数组合作为缓存键，用冒号分隔：
    ///   key: "#minAge:#maxAge"
    ///
    /// 例如 GetUsersByAgeRange(25, 35) 的缓存键为 "users:age-range:25:35"
    ///
    /// 适用于：
    ///   - 分页查询（page:size）
    ///   - 条件筛选（category:minPrice:maxPrice）
    ///   - 时间范围（startDate:endDate）
    /// </summary>
    [Cacheable(cacheName: "users:age-range", key: "#minAge:#maxAge", ttlSeconds: 60)]
    public virtual List<UserDto> GetUsersByAgeRange(int minAge, int maxAge)
    {
        _getUserCallCount++;
        Thread.Sleep(2000);
        return _users.Where(u => u.Age >= minAge && u.Age <= maxAge).ToList();
    }

    // ===================================================================
    // 示例 1.6: 简写属性兼容性 — ttl 替代 ttlSeconds
    // ===================================================================

    /// <summary>
    /// 【简写属性：ttl 替代 ttlSeconds】
    ///
    /// 验证 ttl 简写属性的兼容性。
    /// 新项目可以完全使用 ttl 替代 ttlSeconds，效果完全一致。
    /// </summary>
    [Cacheable(cacheName: "users:legacy", key: "#id", ttlSeconds: 60)]
    public virtual UserDto? GetUserLegacy(int id)
    {
        _getUserLegacyCallCount++;
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 1.7: 简写属性兼容性 — slidingTtl 替代 slidingExpirationSeconds
    // ===================================================================

    /// <summary>
    /// 【简写属性：slidingTtl 替代 slidingExpirationSeconds】
    ///
    /// 验证 slidingTtl 简写属性的兼容性。
    /// 新项目可以完全使用 slidingTtl 替代 slidingExpirationSeconds。
    /// </summary>
    [Cacheable(cacheName: "users:legacy-sliding", key: "#id", ttlSeconds: 86400, slidingTtl: 300)]
    public virtual UserDto? GetUserLegacySliding(int id)
    {
        _getUserLegacyCallCount++;
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }
}
