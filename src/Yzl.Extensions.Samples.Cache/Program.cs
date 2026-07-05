using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Samples.Cache.Services;
using Yzl.Extensions.Samples.TestDashboard;

Console.WriteLine("""
                  ╔═══════════════════════════════════════════════════════╗
                  ║           Yzl.Extensions.Cache 测试                 ║
                  ║                                                      ║
                  ║     访问: http://localhost:16605/api/samples          ║
                  ║                                                      ║
                  ║     首次调用有模拟耗时（毫秒级）                     ║
                  ║     后续调用缓存命中 · 立即返回                     ║
                  ║                                                      ║
                  ║     导航页列出所有缓存测试端点，                     ║
                  ║     刷新即可对比耗时变化                             ║
                  ╚═══════════════════════════════════════════════════════╝
                  """);

var builder = WebApplication.CreateBuilder(args);

// ===================================================================
// 注册示例服务（必须先于 AddEnableCaching）
// ===================================================================
//
// 注意：使用缓存注解的服务必须满足以下条件之一：
//   条件 A：方法标记为 virtual（虚方法）—— Castle DynamicProxy 通过继承创建代理
//   条件 B：类实现了接口（interface）—— 通过接口代理
//
// 生命周期建议使用 Transient 或 Scoped：
//   - Transient：每次请求创建新实例，代理自动包装
//   - Singleton：需确保代理的单例性，通常由 AddEnableCaching 内部处理
// ===================================================================
builder.Services.AddTransient<BasicCacheService>();
builder.Services.AddTransient<CacheLifecycleService>();
builder.Services.AddTransient<SpelKeyService>();
builder.Services.AddTransient<ConditionalService>();
builder.Services.AddTransient<ConfigInheritanceService>();
builder.Services.AddTransient<AsyncCacheService>();
builder.Services.AddTransient<SlidingExpirationService>();
builder.Services.AddTransient<RedisCacheService>();
builder.Services.AddTransient<CacheEvictAllService>();

// ===================================================================
// 注册缓存框架（核心步骤，必须在服务注册之后）
// ===================================================================
//
// AddEnableCaching 方法会完成以下工作：
//   1. 注册 MemoryCache（内存缓存提供器）
//   2. 如果 enableRedis = true，注册 RedisCacheProvider
//   3. 注册缓存拦截器（Castle DynamicProxy）
//   4. 注册缓存操作处理器（Cacheable / CachePut / CacheEvict）
//   5. 自动扫描程序集，为标注了缓存注解的类创建动态代理
//
// 参数说明：
//   assemblies: null           → 自动扫描当前应用程序域的所有程序集
//   enableRedis: false         → 不启用 Redis（仅使用内存缓存）
//   enableRedis: true          → 启用 Redis（需配置连接字符串）
//   redisConnectionString      → Redis 连接字符串，从配置文件中读取
// ===================================================================

// 方案 A：仅使用内存缓存（无需 Redis）
// builder.Services.AddEnableCaching(assemblies: null, enableRedis: false);

// 方案 B：启用 Redis 缓存（如已配置 Redis 连接字符串，取消下面注释）
// var redisConn = builder.Configuration.GetConnectionString("Redis");
// builder.Services.AddEnableCaching(assemblies: null, enableRedis: true, redisConnectionString: redisConn);

// 方案 C（推荐）：从配置文件中读取 Redis 连接字符串
// 配置文件格式（参考 tests 项目）：
//   {
//     "redis": {
//       "main-site": "localhost:6379,abortConnect=false"
//     }
//   }
var redisConn = builder.Configuration["redis:main-site"];
if (!string.IsNullOrEmpty(redisConn))
{
    builder.Services.AddEnableCaching(assemblies: null, enableRedis: true, redisConnectionString: redisConn);
    Console.WriteLine("✓ Redis 缓存已启用");
}
else
{
    builder.Services.AddEnableCaching(assemblies: null, enableRedis: false);
    Console.WriteLine("✓ 内存缓存已启用（如需 Redis，请在 appsettings.Development.json 中配置 redis:main-site）");
}

// 注册控制器
builder.Services.AddControllers();

// 关闭 [ApiController] 自动 400 校验（匹配原始测试项目的行为）
// 测试工具直接使用路由模板中的 {id} 字面量进行请求，
// 原始项目无 [ApiController] 所以能正常降级为默认值。
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

// ===================================================================
// 配置 CORS（允许跨域调试）
// ===================================================================
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();
app.MapControllers();

// 首页 — 返回项目概览
app.MapGet("/", () => new
{
    message = "Yzl.Extensions.Cache 缓存框架测试",
    description = "演示 Cacheable / CachePut / CacheEvict / SpEL 表达式 / 滑动过期 / Redis 等缓存能力",
    dashboard = "/dashboard",
    apiEndpoint = "/api/samples",
    note = "访问 /api/samples 可查看所有缓存测试端点。首次调用有模拟耗时（毫秒级），后续命中缓存立即返回"
});

// 测试仪表盘 — 自动发现所有 Controller 路由（通过 [TestDashboardInfo] 特性获取分组名称和排序）
app.MapTestDashboard();

// ===================================================================
// 启动信息
// ===================================================================
app.Run();
