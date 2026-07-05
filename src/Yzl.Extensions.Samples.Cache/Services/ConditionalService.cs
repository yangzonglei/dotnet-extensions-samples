using Yzl.Extensions.Samples.Cache.Models;

namespace Yzl.Extensions.Samples.Cache.Services;

/// <summary>
/// 【Condition 和 Unless —— 条件缓存控制】
///
/// 框架提供了两个条件表达式来控制缓存行为，它们都在方法执行前后评估 SpEL 表达式：
///
/// ╔══════════════════════════════════════════════════════════════════╗
/// ║  Condition（前置条件） vs Unless（后置排除）                   ║
/// ║                                                                ║
/// ║  Condition = "表达式"                                          ║
/// ║    ├─ 在方法执行前评估                                         ║
/// ║    ├─ 只能引用方法参数（#id, #user, #p0 等）                   ║
/// ║    ├─ true  → 正常使用缓存                                    ║
/// ║    └─ false → 跳过缓存，每次执行方法                           ║
/// ║                                                                ║
/// ║  Unless = "表达式"                                             ║
/// ║    ├─ 在方法执行后评估                                         ║
/// ║    ├─ 可以引用方法返回值（#result）                             ║
/// ║    ├─ true  → 执行结果不写入缓存                               ║
/// ║    └─ false → 结果正常写入缓存                                 ║
/// ║                                                                ║
/// ║  两者可以组合使用，先评估 Condition，再评估 Unless             ║
/// ╚══════════════════════════════════════════════════════════════════╝
/// </summary>
public class ConditionalService
{
    // ===================================================================
    // 模拟数据
    // ===================================================================
    private static readonly List<UserDto> _users = new()
    {
        new() { Id = 1, Name = "Alice", Age = 25, Email = "alice@example.com" },
        new() { Id = 2, Name = "Bob", Age = 30, Email = "bob@example.com" },
        new() { Id = 5, Name = "Eve", Age = 18, Email = "eve@test.com" },
        new() { Id = 99, Name = "SkipUser", Age = 50, Email = "skip@test.com" },
    };

    private int _conditionCallCount;
    private int _unlessCallCount;
    private int _combinedCallCount;
    private int _complexCallCount;
    private int _putCallCount;
    private int _evictCallCount;
    private int _errorCorrectionCallCount;
    private int _conditionAsyncUnlessCallCount;

    public int ConditionCallCount => _conditionCallCount;
    public int UnlessCallCount => _unlessCallCount;
    public int CombinedCallCount => _combinedCallCount;
    public int ComplexCallCount => _complexCallCount;
    public int PutCallCount => _putCallCount;
    public int EvictCallCount => _evictCallCount;
    public int ErrorCorrectionCallCount => _errorCorrectionCallCount;
    public int ConditionAsyncUnlessCallCount => _conditionAsyncUnlessCallCount;

    // ===================================================================
    // 示例 4.1: Condition —— 仅对特定数据启用缓存
    // ===================================================================

    /// <summary>
    /// 【Condition：前置条件判断】
    ///
    /// 当 Condition = "#id > 10" 时：
    ///   - id > 10  → 正常缓存（方法只执行一次，后续命中缓存）
    ///   - id <= 10 → 每次都执行方法（不缓存）
    ///
    /// 使用场景：
    ///   - 只缓存热点数据（如访问量大的用户 ID）
    ///   - 只缓存特定状态的数据（如已审核的订单）
    ///   - A/B 测试中只缓存对照组的数据
    ///
    /// ⚠️ 注意：Condition 中引用的参数必须存在，不存在会记录 WRN 日志并跳过缓存。
    ///   例如 Condition = "#nonExist > 0" 会导致条件评估失败，缓存逻辑降级（每次都执行方法）。
    /// </summary>
    [Cacheable(cacheName: "condition:demo", key: "#id", ttlSeconds: 60,
               Condition = "#id > 10")]
    public virtual UserDto? GetUserWithCondition(int id)
    {
        _conditionCallCount++;
        Console.WriteLine($"[Condition] 执行方法：id={id}（Condition=#id>10，当前id={id}，缓存={(id > 10 ? "启用" : "跳过")}）");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 4.2: Unless —— 排除特定返回值
    // ===================================================================

    /// <summary>
    /// 【Unless：后置排除条件】
    ///
    /// Unless 在方法执行后评估，可以引用返回值 #result。
    /// 当 Unless = "#result == null" 时：
    ///   - 返回值不为 null → 写入缓存
    ///   - 返回值为 null  → 不写入缓存
    ///
    /// 使用场景：
    ///   - 查询结果为空时不缓存（避免缓存"空值"）
    ///   - 特定状态的订单不缓存（如 "已取消"）
    ///   - 错误结果不缓存
    ///
    /// ⚠️ #result 仅在 Unless 中可用，在 Condition 中不可用！
    /// </summary>
    [Cacheable(cacheName: "condition:unless", key: "#id", ttlSeconds: 60,
               Unless = "#result == null")]
    public virtual UserDto? GetUserWithUnless(int id)
    {
        _unlessCallCount++;
        var user = _users.FirstOrDefault(u => u.Id == id);
        Console.WriteLine($"[Unless] 执行方法：id={id}，返回={(user == null ? "null（不缓存）" : $"{user.Name}（缓存）")}");
        Thread.Sleep(1500);
        return user;
    }

    // ===================================================================
    // 示例 4.3: Condition + Unless 组合使用
    // ===================================================================

    /// <summary>
    /// 【Condition + Unless：组合条件控制】
    ///
    /// Condition 和 Unless 可以同时使用，实现更精细的缓存控制：
    ///
    /// Condition = "#id > 0", Unless = "#result == null || #result.Age > 40"
    ///
    /// 执行流程：
    ///   1. 先评估 Condition: "#id > 0"
    ///      - false → 跳过缓存，直接执行方法并返回
    ///      - true  → 进入步骤 2
    ///   2. 从缓存查找，未命中则执行方法
    ///   3. 后评估 Unless: "#result == null || #result.Age > 40"
    ///      - true  → 执行结果不写入缓存
    ///      - false → 结果写入缓存
    ///
    /// 本例语义：只缓存年龄 <= 40 的用户数据
    /// </summary>
    [Cacheable(cacheName: "condition:combined", key: "#id", ttlSeconds: 60,
               Condition = "#id > 0",
               Unless = "#result == null || #result.Age > 40")]
    public virtual UserDto? GetUserCombined(int id)
    {
        _combinedCallCount++;
        var user = _users.FirstOrDefault(u => u.Id == id);
        var shouldCache = user != null && user.Age <= 40;
        Console.WriteLine($"[组合] 执行方法：id={id}，返回={user?.Name ?? "null"}，" +
                          $"年龄={user?.Age}，写入缓存={shouldCache}");
        Thread.Sleep(1500);
        return user;
    }

    // ===================================================================
    // 示例 4.4: CachePut + Condition + Unless
    // ===================================================================

    /// <summary>
    /// 【CachePut + Condition + Unless】
    ///
    /// CachePut 同样支持 Condition 和 Unless：
    ///   - Condition 决定是否执行缓存写入
    ///   - Unless 决定是否排除写入结果
    ///
    /// Condition = "#result != null"：只有方法返回非 null 时才考虑缓存
    /// Unless = "#result.Name == 'skip'"：如果用户名为 skip 则不缓存
    ///
    /// ⚠️ CachePut 方法始终执行！条件只控制是否写入缓存，不影响方法执行。
    /// </summary>
    [CachePut(cacheName: "condition:put", key: "#user.Id", ttlSeconds: 60,
              Condition = "#result != null",
              Unless = "#result.Name == 'skip'")]
    public virtual UserDto? ConditionalUpdateUser(UserDto user)
    {
        _putCallCount++;
        var existing = _users.FirstOrDefault(u => u.Id == user.Id);
        if (existing != null)
        {
            existing.Name = user.Name;
            existing.Age = user.Age;
            existing.Email = user.Email;
            existing.UpdatedAt = DateTime.Now;
        }
        Console.WriteLine($"[CachePut条件] 执行更新：id={user.Id}，新名称={user.Name}，" +
                          $"{(existing == null ? "跳过缓存（结果为null）" : user.Name == "skip" ? "跳过缓存（名称为skip）" : "写入缓存")}");
        return existing ?? user;
    }

    // ===================================================================
    // 示例 4.5: CacheEvict + Condition
    // ===================================================================

    /// <summary>
    /// 【CacheEvict + Condition：条件性驱逐】
    ///
    /// CacheEvict 也支持 Condition 来控制是否执行驱逐。
    /// 例如 Condition = "#id > 0"：只在 ID 有效时执行缓存驱逐。
    /// </summary>
    [CacheEvict(cacheName: "condition:put", key: "#id", Condition = "#id > 0")]
    public virtual void ConditionalEvictUser(int id)
    {
        _evictCallCount++;
        Console.WriteLine($"[CacheEvict条件] 执行驱逐：id={id}（Conditon=#id>0，驱逐缓存={(id > 0 ? "执行" : "跳过")}）");
        Thread.Sleep(200);
    }

    // ===================================================================
    // 示例 4.6: 复杂条件表达式
    // ===================================================================

    /// <summary>
    /// 【复杂条件：多条件组合 + 位置参数】
    ///
    /// Condition 和 Unless 支持使用 #p0、#p1 等位置参数引用。
    ///
    /// Condition = "#p0 > 0 && #p0 < 100"：只缓存 ID 在 1-99 范围内的数据
    /// Unless = "#result.Email == 'skip@test.com'"：排除特定邮箱的用户
    ///
    /// 支持比较运算符： >, <, >=, <=, ==, !=
    /// 支持逻辑运算符： &&, ||
    /// </summary>
    [Cacheable(cacheName: "condition:complex", key: "#id", ttlSeconds: 60,
               Condition = "#p0 > 0 && #p0 < 100",
               Unless = "#result.Email == 'skip@test.com'")]
    public virtual UserDto? GetUserComplex(int id)
    {
        _complexCallCount++;
        var user = _users.FirstOrDefault(u => u.Id == id);
        var skipCache = user?.Email == "skip@test.com";
        Console.WriteLine($"[复杂条件] 执行方法：id={id}，返回={user?.Name ?? "null"}，" +
                          $"{(skipCache ? "跳过缓存（skip邮箱）" : "正常缓存")}");
        Thread.Sleep(1500);
        return user;
    }

    // ===================================================================
    // 示例 4.7: 异步方法 + Unless
    // ===================================================================

    /// <summary>
    /// 【异步方法 + Unless 组合】
    ///
    /// async Task{T} 方法同样支持 Unless。
    /// Unless = "#result == null"：返回 null 时不写入缓存。
    ///
    /// 验证异步方法和条件缓存的组合使用。
    /// </summary>
    [Cacheable(cacheName: "condition:async-unless", key: "#id", ttlSeconds: 60,
               Unless = "#result == null")]
    public virtual async Task<UserDto?> GetUserAsyncUnless(int id)
    {
        _conditionAsyncUnlessCallCount++;
        var user = _users.FirstOrDefault(u => u.Id == id);
        Console.WriteLine($"[异步Unless] 执行方法：id={id}，返回={(user == null ? "null（不缓存）" : $"{user.Name}（缓存）")}");
        await Task.Delay(1500);
        return user;
    }

    // ===================================================================
    // 拼写错误容错（Error Correction）
    // ===================================================================

    /// <summary>
    /// 【Condition 拼写错误验证】
    ///
    /// Condition 中 #res111ult 拼写错误（不存在此变量），应解析为 null：
    ///   null != null → false → condition=false → 永远跳过缓存
    ///
    /// 期望日志输出：
    ///   WRN SpEL 条件表达式引用了不存在的变量 '#res111ult'，解析为 null
    ///
    /// 行为验证：
    /// - 每次调用都执行方法体（1.5s），永不缓存
    /// </summary>
    [Cacheable(cacheName: "condition:typo-condition", key: "#id", ttlSeconds: 300,
        Condition = "#res111ult != null")]
    public virtual UserDto? GetUserWithTypoInCondition(int id)
    {
        _errorCorrectionCallCount++;
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    /// <summary>
    /// 【Unless 拼写错误验证】
    ///
    /// Unless 中 #res111ult 拼写错误（不存在此变量），应解析为 null：
    ///   null == null → true → unless=true → 永远不写缓存
    ///
    /// 期望日志输出：
    ///   WRN SpEL 条件表达式引用了不存在的变量 '#res111ult'，解析为 null
    ///
    /// 行为验证：
    /// - 首次 1.5s，后续仍然 1.5s（永不缓存）
    /// </summary>
    [Cacheable(cacheName: "condition:typo-unless", key: "#id", ttlSeconds: 300,
        Unless = "#res111ult == null")]
    public virtual UserDto? GetUserWithTypoInUnless(int id)
    {
        _errorCorrectionCallCount++;
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    /// <summary>
    /// 【属性链中根变量拼写错误】
    ///
    /// Condition 中 #usre.Age 的根变量 #usre 不存在：
    ///   根变量 null → null > 0 → false → condition=false → 永远跳过缓存
    ///
    /// 期望日志输出：
    ///   WRN SpEL 条件表达式引用了不存在的变量 '#usre'（属性链 '#usre.Age'），解析为 null
    ///
    /// 行为验证：
    /// - 每次调用都执行方法体（1.5s），永不缓存
    /// </summary>
    [Cacheable(cacheName: "condition:typo-chain", key: "#id", ttlSeconds: 300,
        Condition = "#usre.Age > 0")]
    public virtual UserDto? GetUserWithTypoInPropertyChain(int id)
    {
        _errorCorrectionCallCount++;
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }
}
