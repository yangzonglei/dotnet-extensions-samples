using Yzl.Extensions.Samples.Cache.Models;

namespace Yzl.Extensions.Samples.Cache.Services;

/// <summary>
/// 【SpEL 表达式 —— Spring Expression Language 风格的缓存键】
///
/// 缓存键支持丰富的 SpEL 表达式语法，这是框架最强大的特性之一。
/// SpEL 表达式以 "#" 开头，后面跟参数名或方法名。
///
/// ╔══════════════════════════════════════════════════════════════╗
/// ║  SpEL 表达式语法大全                                       ║
/// ║                                                            ║
/// ║  #参数名          → 引用方法参数，如 #id、#name           ║
/// ║  #p0, #p1, ...    → 按位置引用参数，#p0 是第一个参数      ║
/// ║  #参数名.属性     → 嵌套属性访问，如 #user.Name           ║
/// ║  #p0.属性.子属性  → 多级嵌套，如 #qo.User.Name            ║
/// ║  #字典名.key      → 字典/动态对象属性，如 #cfg.version    ║
/// ║  #result          → 方法返回值（仅 CachePut/Unless 支持） ║
/// ║  值1:值2          → 组合多个值形成复合键                  ║
/// ╚══════════════════════════════════════════════════════════════╝
/// </summary>
public class SpelKeyService
{
    // ===================================================================
    // 模拟数据
    // ===================================================================
    private static readonly Dictionary<string, object> _appConfig = new()
    {
        ["site_name"] = "MyApp",
        ["version"] = "2.0.0",
        ["maintainer"] = "admin@example.com",
        ["max_upload_size"] = 10485760,    // 10MB
    };

    private static readonly List<UserDto> _users = new()
    {
        new() { Id = 1, Name = "Alice", Age = 25, Email = "alice@example.com" },
        new() { Id = 2, Name = "Bob", Age = 30, Email = "bob@example.com" },
        new() { Id = 3, Name = "Charlie", Age = 35, Email = "charlie@example.com" },
    };

    private int _callCount;
    public int CallCount => _callCount;

    // ===================================================================
    // 示例 3.1: 嵌套属性访问（对象.属性）
    // ===================================================================

    /// <summary>
    /// 【SpEL：嵌套属性访问】
    ///
    /// 当方法参数是对象时，可以通过 "#参数名.属性名" 访问其属性。
    ///
    /// 例如：GetUserByQuery(new QueryQo { UserId = 1, Keyword = "test" })
    ///   → 缓存键为 "spel:query:1:test"
    ///
    /// 等价于 Java Spring 中的 #qo.userId + ":" + #qo.keyword
    ///
    /// 支持多层嵌套： #order.user.address.city
    /// </summary>
    [Cacheable(cacheName: "spel:query", key: "#qo.UserId:#qo.Keyword", ttlSeconds: 60)]
    public virtual UserDto? GetUserByQuery(QueryQo qo)
    {
        _callCount++;
        Console.WriteLine($"[SpEL 嵌套属性] 执行查询：UserId={qo.UserId}, Keyword={qo.Keyword}");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == qo.UserId);
    }

    // ===================================================================
    // 示例 3.2: 字典键访问
    // ===================================================================

    /// <summary>
    /// 【SpEL：字典（Dictionary）键访问】
    ///
    /// 当方法参数是 Dictionary<string, object> 时，
    /// 可以直接通过 "#参数名.key名" 访问字典中的值。
    ///
    /// 例如：GetConfig(config) 其中 config["site_name"] = "MyApp"
    ///   → 缓存键为 "spel:config:MyApp:2.0.0"
    ///
    /// 适用于动态配置、请求头、环境变量等字典结构的数据。
    /// </summary>
    [Cacheable(cacheName: "spel:config", key: "#cfg.site_name:#cfg.version", ttlSeconds: 120)]
    public virtual Dictionary<string, object>? GetConfig(Dictionary<string, object> cfg)
    {
        _callCount++;
        Console.WriteLine($"[SpEL 字典] 获取配置：site={cfg.GetValueOrDefault("site_name")}, " +
                          $"version={cfg.GetValueOrDefault("version")}");
        Thread.Sleep(500);
        return _appConfig;
    }

    // ===================================================================
    // 示例 3.3: 位置参数引用（#p0, #p1, ...）
    // ===================================================================

    /// <summary>
    /// 【SpEL：位置参数引用 #p0, #p1】
    ///
    /// 当参数名不方便记忆时，可以使用位置索引引用参数：
    ///   #p0  = 第一个参数
    ///   #p1  = 第二个参数
    ///   #p2  = 第三个参数
    ///
    /// 这在以下场景特别有用：
    ///   - 方法签名中有多个同类型参数
    ///   - 使用了 [CallerMemberName] 等特殊参数
    ///   - 重构时参数名变化但位置不变
    ///
    /// 本例中 #p0 等价于 #id
    /// </summary>
    [Cacheable(cacheName: "spel:positional", key: "#p0", ttlSeconds: 60)]
    public virtual UserDto? GetByIdPositional(int id)
    {
        _callCount++;
        Console.WriteLine($"[SpEL 位置参数] 执行查询：id={id}（通过 #p0 引用）");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 3.4: 复合 SpEL 表达式（多条件组合）
    // ===================================================================

    /// <summary>
    /// 【SpEL：复杂复合键】
    ///
    /// 支持多级嵌套 + 多参数组合的复杂表达式：
    ///   key: "#qo.UserId:#qo.Keyword:#qo.Page:#qo.PageSize"
    ///
    /// 适用于 RESTful API 的分页查询缓存。
    /// 每页数据不同，缓存键也不同，互不干扰。
    /// </summary>
    [Cacheable(cacheName: "spel:paged", key: "#qo.UserId:#qo.Keyword:#qo.Page:#qo.PageSize", ttlSeconds: 60)]
    public virtual List<UserDto> SearchUsers(QueryQo qo)
    {
        _callCount++;
        Console.WriteLine($"[SpEL 复合键] 分页查询：Page={qo.Page}, Size={qo.PageSize}, " +
                          $"UserId={qo.UserId}, Keyword={qo.Keyword}");
        Thread.Sleep(2000);

        var query = _users.AsEnumerable();

        if (qo.UserId > 0)
            query = query.Where(u => u.Id == qo.UserId);
        if (!string.IsNullOrEmpty(qo.Keyword))
            query = query.Where(u => u.Name.Contains(qo.Keyword, StringComparison.OrdinalIgnoreCase));

        return query.Skip((qo.Page - 1) * qo.PageSize).Take(qo.PageSize).ToList();
    }

    // ===================================================================
    // 示例 3.5: 默认方法名作为缓存区域
    // ===================================================================

    /// <summary>
    /// 【SpEL：省略 cacheName 时使用方法全限定名】
    ///
    /// 当 [Cacheable] 没有指定 cacheName 时，
    /// 框架会自动使用方法的全限定名（命名空间.类名.方法名）作为缓存区域。
    ///
    /// 例如本方法的 cacheName 默认为：
    ///   "Yzl.Extensions.Samples.Cache.Services.SpelKeyService.GetUserByDefaultName"
    ///
    /// 适用于：
    ///   - 快速原型开发
    ///   - 方法级别的缓存隔离
    ///   - 不需要按业务区域组织缓存的场景
    /// </summary>
    [Cacheable(key: "#id", ttlSeconds: 60)]
    public virtual UserDto? GetUserByDefaultName(int id)
    {
        _callCount++;
        Console.WriteLine($"[SpEL 默认名称] 执行查询：id={id}（使用方法全限定名作为 cacheName）");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }

    // ===================================================================
    // 示例 3.6: Redis + 滑动过期组合
    // ===================================================================

    /// <summary>
    /// 【SpEL + Redis + 滑动过期组合】
    ///
    /// 结合 SpEL 表达式、Redis 缓存后端、滑动过期三种能力。
    /// Redis 后端 + slidingTtl=300 + ttl=86400 绝对兜底。
    /// 需要配置 Redis 连接串才能演示 Redis 功能。
    /// </summary>
    [Cacheable(cacheName: "spel:sliding-redis", key: "#id", ttlSeconds: 86400, slidingTtl: 300,
               cacheType: CacheType.Redis)]
    public virtual UserDto? GetUserSlidingRedis(int id)
    {
        _callCount++;
        Console.WriteLine($"[SpEL+Redis+滑动] 执行查询：id={id}（Redis+slidingTtl=300s）");
        Thread.Sleep(1500);
        return _users.FirstOrDefault(u => u.Id == id);
    }
}
