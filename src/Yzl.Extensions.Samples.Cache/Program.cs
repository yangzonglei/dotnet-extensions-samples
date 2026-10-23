using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Cache.Redis;
using Yzl.Extensions.Http.OpenFeign;
using Yzl.Extensions.Http.OpenFeign.Serializer;
using Yzl.Extensions.Samples.Cache.Managers;
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
//
// ── 使用 [IocService] + AddBatchServices() 自动扫描注册 ──
// 每个服务类都标注了 [IocService(lifetime: ServiceLifetime.Transient)]，
// AddBatchServices() 会扫描 App.Assemblies（当前解决方案中的项目程序集），
// 把带 [IocService] 特性的类按其 Order 顺序注册进 DI 容器。
//
// 相比逐个手写 AddTransient<T>()，这种方式新增服务时无需改动 Program.cs。
// 也可以显式指定程序集：AddBatchServices(typeof(BasicCacheService).Assembly)
// 或指定程序集名：AddBatchServices("Yzl.Extensions.Samples.Cache")
// ===================================================================
builder.Services.AddBatchServices();

// ===================================================================
// 注册 OpenFeign（用于第十一章：远程调用 + Cacheable）
// ===================================================================
//
// 扫描 App.Assemblies 中所有标注了 [FeignClient] 的接口，
// 为其生成动态代理并注册进 DI 容器 —— 之后可以直接注入
// 接口类型（如 ICacheDemoFeignClient），无需手写 HttpClient。
//
// SerializerType：显式指定 JSON 序列化器。不指定时框架会回退到
// NewtonsoftFeignSerializer；本示例与 OpenFeign 示例项目保持一致，
// 使用 System.Text.Json。
//
// ⚠ 顺序要求：AddFeignStarter 必须在 AddEnableCaching <b>之前</b>调用。
//   原因见下面的 AddEnableCaching 注释 —— 后者在调用时会扫描
//   已注册进容器的接口服务，给「注解标在接口方法上」的接口叠加缓存代理；
//   晚于它注册的接口不会被包住，而且是静默失效。
// ===================================================================
builder.Services.AddFeignStarter(builder.Configuration, options =>
{
    options.SerializerType = typeof(SystemTextJsonFeignSerializer);
});
Console.WriteLine("✓ OpenFeign 已启用（目标：http://localhost:16600）");

// ===================================================================
// 注册缓存框架（核心步骤，必须最后调用）
// ===================================================================
//
// AddEnableCaching 方法会完成以下工作：
//   1. 注册 MemoryCache（框架自带的内存后端，即默认的 MemoryCacheManager）
//   2. 扫描并执行各后端包提供的 ICacheServiceConfigurator（v0.2.0 起的自注册入口）——
//      本项目引用了 Yzl.Extensions.Cache.Redis，于是 RedisCacheManager 在这里被注册。
//      「引用即生效」：不需要任何 AddRedisCache() 调用，也不需要给本方法传参数。
//   3. 注册缓存拦截器（Castle DynamicProxy）
//   4. 注册缓存操作处理器（Cacheable / CachePut / CacheEvict）
//   5. 自动扫描程序集，为标注了缓存注解的类创建动态代理
//   6. 扫描「已注册进容器的接口服务」，为注解标在接口方法上的接口
//      叠一层缓存代理 —— OpenFeign 的客户端接口（如 ICacheDemoFeignClient）
//      走的就是这条路径，于是 [Cacheable] 可以直接写在接口方法上
//
// ⚠ 因为第 6 步依赖「此刻容器里已经有什么」，本方法必须最后调用：
//     AddBatchServices() → AddFeignStarter(...) → AddEnableCaching(...)
//   晚于本方法注册的接口不会被包住，且是静默失效。
//
// 参数说明：
//   assemblies: null → 自动扫描当前应用程序域的所有程序集
//
// 后端选择（v0.2.0 起）：
//   不再由 AddEnableCaching 的参数决定，而是由注解上的 typeof 决定 ——
//     不写 cacheManager                        → 框架自带内存后端
//     cacheManager: typeof(RedisCacheManager)  → Redis 后端（连接首次使用时才建立）
//
// 连接从哪来（第十章，两条路可以混用）：
//   ① 配置：注解上的 cacheManager 是一个 RedisCacheManager 子类，它只重写
//      ConnectionStringKey；真正的连接参数全在 spring:data:redis 配置节里
//      —— 见 appsettings.json 与 Managers/ConfigBackendRedisManager.cs。
//   ② 用户注入：注册一个 IRedisConnectionSource（见下面那行 AddSingleton），
//      由它自己建连接并认领某个连接名 —— 见 Managers/SamplesInjectedConnectionSource.cs。
//      这个来源是「用户 @Bean」的语义，路径上【优先于配置】：只要它认领了连接名，
//      框架连配置都不会去读。
//
// ⚠ 连接名打错 / 没配时【不会启动即失败，也不会返回 500】：
//   连接解析异常是 InvalidOperationException，但读路径被 CacheableHandler.TryReadCache
//   的 catch (Exception) 吞掉、写路径被 CacheOperationHelper.WriteCache 的同类 catch 吞掉
//   ⇒ 表现为「缓存永远不命中」（elapsedMs 一直是你方法里的耗时），不是报错。
//   真正会 500 的只有「cacheManager 写的类型没注册」——那条 InvalidOperationException
//   会穿透 CacheOperationDispatcher 的降级过滤器。
// ===================================================================

// ===================================================================
// 第十章：用户自定义连接的缝（Java「自己声明一个 @Bean RedisConnectionFactory」）
// ===================================================================
//
// 这是本样例里唯一一处显式的缓存相关注册。它演示诉求第 2 条 ——
// 「客户端实现一个框架里的接口，来创建一个连接（只创建一次）」。
//
// 本来源认领 appsettings.json 里那个 "redis:samples-injected" 连接名，
// 并用完全由自己决定的参数建连（db 8 / 自己的 ClientName）—— 因此它是
// 「用户连接优先于配置」这条契约的活证据（配置里同一个连接名写的是 db 7）。
//
// ⚠ 注册位置【不敏感】：框架会按注册顺序依次询问容器里所有 IRedisConnectionSource，
//   放在 AddEnableCaching() 之前或之后都行。
// ⚠ 但【不要】把它注册成 Transient/Scoped —— 契约要求「同一个连接名只创建一次」，
//   而 DI 单例是「只建一次」最省事的实现方式（本类自己也有 lock + 二次检查）。
// ⚠ 连接由用户创建 ⇒ 归用户释放。注册成单例并由容器释放是推荐做法；
//   框架的 Dispose 链刻意不碰用户交进来的连接（避免与容器重复释放）。
builder.Services.AddSingleton<IRedisConnectionSource, SamplesInjectedConnectionSource>();

builder.Services.AddEnableCaching();
Console.WriteLine("✓ 缓存已启用（内存后端默认可用；Redis 连接按需建立：配置走 spring:data:redis，用户来源优先）");

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
    note = "访问 /api/samples 可查看所有缓存测试端点。首次调用有模拟耗时（毫秒级），后续命中缓存立即返回",
    feignDemo = "第十一章演示 OpenFeign 远程调用 + Cacheable：GET /api/samples/feign/{id}（需先启动 Samples.Api：16600）"
});

// 测试仪表盘 — 自动发现所有 Controller 路由（通过 [TestDashboardInfo] 特性获取分组名称和排序）
app.MapTestDashboard();

// ===================================================================
// 启动信息
// ===================================================================
app.Run();
