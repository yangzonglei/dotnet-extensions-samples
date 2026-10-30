# Yzl.Extensions.Cache 更新日志

## v0.2.0 [2026/10/23]

> 「更新日志合并」说明：本版本号在发布前做过两轮改动（开发期曾分别写作 `v0.1.4` 与 `v0.3.0`，**两者都未发布过正式包**），
> 现已合并为同一个 `v0.2.0`，下面的「新增 / 性能 / 修复」即两轮改动的最终形态。
> **上一个已发布版本是 `v0.1.3`**，因此其中的「注解可标在接口方法上」「缓存键格式变更」等条目请看下方 v0.1.3 段落。

本版本包含两大部分：

1. **把「选后端」的层次改对了** —— 注解上不再用枚举挑后端，而是写 `cacheManager: typeof(XXX)`，对齐 Java 的
   `org.springframework.cache.annotation`。新增第三种缓存策略从此**不需要改动框架源码** —— 在自己的包里实现
   `ICacheManager`（会被自动注册进容器）即可。Redis 后端同时拆分为独立包 **`Yzl.Extensions.Cache.Redis`**。
2. **滑动过期补上「绝对上限」**（语义修复，见下方升级须知），以及**命中路径性能改造**（消除逐次反射与逐次分配）。

### ⚠️ 升级须知：滑动过期语义变更（v0.1.3「不改 TTL 语义」承诺的例外）

v0.1.3 的性能改动承诺过「不改 TTL 语义」，本版本**有意打破这一承诺**，因此单独声明。这是本次唯一的语义变化，也是唯一必须 bump minor 版本的理由。

**改造前**：`slidingTtl > 0` 时 `ttlSeconds` 被**整条丢弃**。

```csharp
var isSliding = attr.SlidingExpirationSeconds > 0;
var ttl = isSliding ? attr.SlidingExpirationSeconds : attr.TtlSeconds;   // ttlSeconds 没了
```

后果是**热 key 永不淘汰**：

| 后端 | 表现 |
|---|---|
| Memory | 纯滑动过期，不存在任何绝对上限 |
| Redis | 写值只用窗口长度设 TTL，每次命中再 `TouchAsync → PEXPIRE` 无条件续期 |

持续被读的 key 于是永久驻留 —— 陈旧数据不淘汰、内存无界增长。这与两个属性的 XML 文档（`CacheableAttribute.TtlSeconds` / `CacheConfigAttribute.DefaultTtlSeconds` 均写明「作为绝对过期兜底」）也不符。

**改造后**（公式）：

```
绝对过期 = max(ttlSeconds, slidingTtl)
滑动窗口 = slidingTtl
```

- **`ttlSeconds <= slidingTtl` → 行为与改造前逐字节一致**。`ttlSeconds` 默认 300，既有滑动配置（`slidingTtl` 30 / 300）都落在这一类，**不需要做任何调整**。
- **仅当显式把 `ttlSeconds` 调得比滑动窗口长时**才真正加上限 —— 即文档里一直描述的那种用法。

也就是说：**这次修的是「文档承诺了但实现没做」的兜底，不是给既有配置缩短寿命。** 若你在 `[Cacheable(..., ttlSeconds: 60, slidingTtl: 5)]` 这类已经写了 `ttl > slidingTtl` 的配置上运行旧版本，实际寿命上限**远长于 60 秒**；升级后最长寿命**恰好是 60 秒**（窗口内持续被读也不会再续）。请确认这个收紧是可接受的 —— 这也正是该写法的本意。

三个后端的落地方式：

| 后端 | 实现 |
|---|---|
| Memory | `SetAbsoluteExpiration(绝对过期)` + `SetSlidingExpiration(窗口)`（先设绝对，`SetSlidingExpiration` 会重置 `AbsoluteExpiration`，顺序不能反） |
| Redis | 条目键 TTL = 窗口；**伴生 deadline 键** TTL = 绝对过期，写一次后不再改动；续期用一段 Lua 把寿命夹到 `min(窗口, deadline 剩余)` |
| 第三方 `ICacheProvider` | 未实现新增的 `ISlidingCacheProvider` 时自动回退到改造前行为（`SetAsync` 仍收到「窗口」当 TTL，无绝对上限） |

**关于 Redis 的伴生 deadline 键**：为什么不能把上限直接写在条目键上 —— 第一次命中续期就会把「上限」覆盖成窗口值，上限从此永久丢失，`min(窗口, 当前 TTL)` 会随每次命中单调收缩，把「滑动」退化成「从第一次命中起算的固定窗口」。所以上限必须由第二个键独立承载。

伴生键名为 `<条目键>:__yzl_slide_deadline`，与条目键**同前缀**，因此：

- 自带 TTL，到期自动清理；
- `RemoveByPrefixAsync`（按 cacheName 前缀批量驱逐）会一并带走它；
- `RemoveAsync` 也会一并删除（一次变长 `DEL`，不是两次往返）。

写入与删除都不增加 Redis 往返次数。**注意**：幂等重建缓存后，旧键与伴生键都会被覆盖，无需手工清理。

### ⚠️ 升级须知：后端选择方式变更（`CacheType` 枚举 → `cacheManager: typeof(...)`）

**1. `CacheType` 枚举与 `[CacheConfig].DefaultCacheType` 删除**

不留 `[Obsolete]` 垫片——枚举的存在本身就是这次要修的问题。迁移方式：

| 改造前 | 改造后 |
|---|---|
| `[Cacheable("users", "#id", cacheType: CacheType.Redis)]` | `[Cacheable("users", "#id", cacheManager: typeof(RedisCacheManager))]` |
| `[CacheConfig(defaultCacheType: CacheType.Memory)]` | `[CacheConfig()]`（内存是默认，无需声明） |
| `[CacheConfig(defaultCacheType: CacheType.Redis)]` | `[CacheConfig(cacheManager: typeof(RedisCacheManager))]` |

参数**位置**未变（`Cacheable`/`CachePut` 的第 4 位、`CacheEvict` 的第 5 位），所以按位置传参的写法不受影响；按名字传 `cacheType:` 的写法会编译失败（这是好事，清单明确）。

**2. `AddEnableCaching` 去掉 `enableRedis` / `redisConnectionString` 两个参数**

```csharp
// 改造前
var redisConn = builder.Configuration["redis:main-site"];
builder.Services.AddEnableCaching(assemblies: null, enableRedis: true, redisConnectionString: redisConn);

// 改造后 —— Redis 包只要被引用就自动生效，不需要任何 AddRedisCache 之类的调用
builder.Services.AddEnableCaching();
```

配置键仍是 `redis:main-site`，改由 Redis 包**惰性读取**。调用顺序契约不变（仍必须最后调用）。

**3. Redis 连接参数改为惰性读取，且注册期不再需要连接串**

改造前 `AddEnableCaching` 必须拿到 `redisConnectionString`（空则注册期直接抛
`ArgumentNullException`），连接在第一次解析 `RedisCacheProvider` 时建立。现在两件事都推迟：
注册期不读任何 Redis 配置，连接在**第一次真实缓存操作**时才建立。影响：

- 引用了 `Yzl.Extensions.Cache.Redis` 但一个 Redis 注解都没写的应用，不会在启动期因 Redis
  不可达（或压根没配）而失败；
- 连接参数的来源与优先级见 Redis 包的 README（两级查找：按连接名的扁平键 → `spring:data:redis`）。

**4. 不可解析的管理器类型 ⇒ 抛 `InvalidOperationException`（原先是静默降级为内存）**

这是本次最重要的行为变更。改造前若注解声明了 Redis 但 Redis 未注册，框架会**静默**把数据写进内存缓存——问题会在很久以后以数据不一致的形式暴露。

现在首次缓存操作即抛异常，消息区分两种情况并给出可操作指引：类型未实现 `ICacheManager` / 已实现但未注册进 DI。异常会被 `CacheOperationDispatcher` 的过滤器放行（不降级），因此表现为 500 而不是"看起来正常"。

**5. `CacheEvictHandler` 的 provider 解析移到 Condition 评估之后**

`condition` 恒为 `false` 的驱逐方法，即使 `cacheManager` 写错也不会再抛异常——它压根不需要缓存后端。

**6. `[CachePut]` / `[CacheEvict]` 开始继承类级 `[CacheConfig]`**

修复一个不对称缺陷：此前只有 `[Cacheable]` 会继承类级 `[CacheConfig]` 的 `DefaultCacheName` / `DefaultTtlSeconds`，`[CachePut]` 与 `[CacheEvict]` 不会（尽管文档一直声称会）。现在三者一致。

**7. Redis 后端改为「管理器 + provider」两层（`RedisCacheManager` 是新增类型）**

`RedisCacheManager` 在 0.1.x 里**不存在**，因此没有「改造前」可比 —— 0.1.x 是
`cacheType: CacheType.Redis` 直接解析容器里的 `RedisCacheProvider` 单例。本版本才引入管理器
（命名对齐 Java 的 `RedisCacheManager`），由它按「连接名 + 序列化器 + 库索引」构造各自的 provider。
实际需要改的只有注册与注解写法（见上面第 1、2 条）。

管理器同时提供四个 `protected virtual` 重写点（`ConnectionStringKey` / `DatabaseIndex` / `Serializer` / `AllowNullValues`），
`ICacheManager.Provider` 的声明类型仍严格是 `ICacheProvider`（不可放宽，`CacheInvokerCache` 依赖它）。

**8. `RedisConnectionProvider` / `RedisCacheProvider` 不再注册进容器**

provider 现在必须绑定**具体的连接 + 序列化器 + 库索引**，只有管理器（或用户手写的 `ICacheManager`）
才知道该绑哪一套，因此无法再作为单例提供。`GetService<RedisCacheProvider>()` 会返回 `null`。
需要自己构造时走 `IRedisConnectionFactory.Get(key)` 再 `new RedisCacheProvider(...)`。

**9. 管理器改为自动注册（原先要自己注册）**

`AddEnableCaching(assemblies: ...)` 现在会扫描并自动注册范围内所有具体的 `ICacheManager` 实现。
原先「自己写子类 + `[IocService]`/`AddSingleton`」的写法**可以删掉注册代码**（留着也不冲突，显式注册优先）。

反过来，升级后一个**直接实现 `ICacheManager` 的具体类型**只要落在扫描范围内就会被注册进容器——
在 v0.1.3 及更早版本里它是「未注册」，注解点名它会抛 `InvalidOperationException`；升级后则正常解析。
这通常正是你想要的结果，但如果你的代码**依赖**那条异常来暴露配置错误（例如测试里断言「未注册必须炸」），
请在 `AddEnableCaching` 之后显式移除该类型：`services.RemoveAll<YourManager>();`。

**10. Redis 连接层多了一条框架默认值 `abortConnect=false`（连接行为变更）**

本框架现在把一组连接层默认值以连接串片段的形式铺在**用户连接串之前**，其中与 StackExchange.Redis
上游默认值不同的一条是 **`abortConnect=false`**（上游默认是 `true`）。

对既有部署的影响：**「启动瞬间 Redis 不可达」从「抛异常」变成「返回一个已注册、尚未连上的
多路复用器，后台自动重连」**。这是缓存层高可用想要的行为（对齐 Java：Lettuce 不会因为首次连不上
就拒绝创建连接工厂），但它确实改变了失败姿态 —— 原先靠启动即抛来尽早暴露 Redis 配置错误的部署，
升级后不会再在启动期看到异常。

- 想保留旧行为：在连接串或 `spring:data:redis:connection-string` 里显式写 `abortConnect=true`
  （**用户写的值永远优先**）。
- 框架默认值里的其余各项（`connectTimeout` / `syncTimeout` / `asyncTimeout` / `keepAlive` /
  `connectRetry`）与上游默认值相同，显式写出来的目的是**钉住姿态**：将来升级 StackExchange.Redis
  时，上游改默认值不会静默改变线上行为。

### ✨ 新增

**滑动过期绝对上限**

- **`ISlidingCacheProvider`**（可选实现，**零编译期破坏**）：

  ```csharp
  public interface ISlidingCacheProvider
  {
      Task SetSlidingAsync<T>(string key, T value, int slidingWindowSeconds, int absoluteTtlSeconds);
  }
  ```

  `ICacheProvider` **一字未改**（仍是原有 5 个成员）。自定义 Provider 不实现新接口也能正常工作，只是滑动过期没有绝对上限（与改造前一致）。内置的 `MemoryCacheProvider` / `RedisCacheProvider` 均已实现。

**后端选择与多 Redis 后端**

- `ICacheManager`：框架内部接口，`cacheManager: typeof(X)` 的 X 必须实现它。约定 `ICacheProvider Provider { get; }`（类型必须是 `ICacheProvider`，见源码注释）。
- **管理器自动注册**：`AddEnableCaching(assemblies: ...)` 扫描范围内**所有具体的 `ICacheManager` 实现**
  都会自动注册为**单例**，使用者**不需要写 `[IocService]`、`AddSingleton` 或任何注册代码**。两种形态一视同仁：
  继承后端基类（如 `RedisCacheManager`）的**子类**，以及**直接实现 `ICacheManager`** 的类型（第三方后端）。
  你之前用 `[IocService(lifetime: ServiceLifetime.Singleton)]` 手工注册的那些子类，现在可以把特性删掉了。
    - **显式注册优先**：自动注册走 `TryAddSingleton`。想在 `AddEnableCaching` **之前**用别的生命周期
      （`AddScoped<T>()` / `[IocService(lifetime: ...)]`）注册同类型，会自动注册**保留你的**注册。
    - **`sealed` 也可以**：自动注册的过滤口径是「具体、非抽象、非开放泛型、至少有一个公共构造函数」，
      比 `AddBatchServices()` 宽。最后那条是必要的：只有 private / internal 构造函数的类型 DI 永远构造不出来，
      注册它会让 `Development` 下的 `ValidateOnBuild` 在 `Build()` 期直接抛
      `AggregateException("Some services are not able to be constructed …")` —— 宿主起不来，
      而 `Production` 与裸 `BuildServiceProvider()` 的测试都看不见（已实测）。筛掉之后问题退化成
      解析期那句点名的「未在 DI 容器中注册」。
    - **过度注册无副作用**：注册只写服务描述符、不构造实例；`CacheManagerRegistry` 只解析注解里
      `typeof(...)` 点名的类型，没被点名的管理器永远不会被实例化（也不建连）。
- `ICacheManagerRegistry` / `CacheManagerRegistry`：按管理器类型解析后端。公开，供后续消费。
- `MemoryCacheManager`：框架默认的内存管理器，无需任何声明。
- `ICacheServiceConfigurator`：后端包的自注册扩展点。`AddEnableCaching` 会全量扫描程序集并执行所有实现者，因此**引用即生效**。
- `Yzl.Extensions.Cache.Redis` 包：`RedisCacheManager` / `RedisCacheProvider` / `RedisConnectionProvider` / `RedisCacheServiceConfigurator`。
- **一个项目内多个 Redis 后端**：`RedisCacheManager` 提供 `protected virtual` 的
  `ConnectionStringKey`（配置键）/ `DatabaseIndex`（库索引）/ `Serializer`（序列化器）/ `AllowNullValues`（是否缓存 null）四个重写点，
  继承它写一行子类即可连第二个实例或另一个 DB 索引，对齐 Java
  `@Bean("downloadTaskRedisTemplate")` 那种「每个命名模板各带一套连接 + 序列化器」的写法。
  框架源码里不需要提到用户的后端类型。子类**写完之后不需要任何注册代码**（见上面的「管理器自动注册」），
  `sealed` 子类同样可以；`sealed` 只在走 `[IocService] + AddBatchServices()` 那条老路时会被静默跳过，
  而那已经不是缓存管理器的推荐接法了。
- `IRedisConnectionFactory` / `RedisConnectionFactory`：按**连接名**提供连接持有者并去重 ——
  指向同一个连接名的多个管理器复用同一个 `ConnectionMultiplexer`，不会重复建连。
  取用**不建连**，首次读写才建。
- `IRedisCacheSerializer` 与两个内置实现：`NewtonsoftRedisCacheSerializer`（默认，与历史实现
  **逐字节一致**，旧数据继续可读）、`SystemTextJsonRedisCacheSerializer`。
  共同的「信封」逻辑（UTF-8、`> 1024` 字节自动 GZIP、GZIP 魔数判定）在基类 `RedisCacheSerializerBase`。
  ⚠ 序列化器只作用于**复杂类型**，`string`/`int`/`long`/`double`/`bool`/`byte[]` 仍以裸值存放
  （标量路径零分配，且换序列化器不影响标量键）；用户侧的 `null` 也不走序列化器 ——
  provider 写 null 时落 7 字节哨兵 `RedisNullMarker`（见下面 🔧 修复 1）；⚠ **同一个 `cacheName` 上不要换序列化器**，
  两种引擎对同一对象的字节不同，错配表现为「永远不命中」而非报错。

**Redis 连接：结构化配置 + 自定义连接 + 连接层高可用**（`Yzl.Extensions.Cache.Redis`）

- **`spring:data:redis` 配置节**（节名与字段名都与 Java
  `org.springframework.boot.autoconfigure.data.redis.RedisProperties` 逐字对齐）：
  `url` / `host` / `port` / `username` / `password` / `database` / `client-name` /
  `timeout` / `connect-timeout` / `ssl.enabled`，外加一个本包扩展的 `connection-string`
  （原样连接串逃生口，Java 无此键）。
    - **整节只描述一个 Redis**，与 Java 一致 —— 没有 `default` / `connections:<名>` 这类
      Java 里不存在的子节点。多 Redis 走代码（`AddRedisConnection(...)` /
      `IRedisConnectionSource`），等价 Java 自己声明 `@Bean RedisConnectionFactory`。
    - `timeout` / `connect-timeout` 是 **Java `Duration`** 语义：`500ms` / `5s` / `1m` /
      `2h` / `1d`（**无单位按毫秒**，与 Spring Boot 一致）、ISO-8601 `PT5S`，另接受 .NET 的
      `00:00:05`。⚠ **写错直接抛 `FormatException`**，不静默退回默认值。
    - `url` 语义与 Java 逐条一致：它决定 host / port / username / password，而
      `database` **仍只来自 `database` 字段**（URL 路径里的 `/3` 被忽略）。
    - **两级查找**：**按连接名的扁平连接串键**（`redis:main-site` 这种，更具体、优先）→
      `spring:data:redis`（且给了端点）→ 抛「缺配置」的 `InvalidOperationException`。
      **旧键永远有效**，既有部署不改配置也能继续跑；`spring:data:redis` 整节缺失同样合法。
    - 「只配了超时没配地址」与「整节不存在」同样对待，都算未配置 —— 不会带着 Java 的默认
      `localhost:6379` 悄悄连上本机。这是与 Java 的有意差异：那边的默认值服务于自动配置。
    - 结构化字段**始终覆盖** `connection-string` 里的同名项；框架默认值以连接串片段的形式铺在
      用户连接串**之前**，因此用户逐项写下的值总能生效。
- **`IRedisConnectionSource`**：用户自定义连接的缝，语义对应 Java「自己声明一个
  `@Bean RedisConnectionFactory`」。实现接口并注册到 DI（`AddEnableCaching` 之前或之后都行），
  或直接用 `AddRedisConnection(services, name, connection | factory)` 的简写。
    - **`null` = 不认领**，链式交给下一个来源，全部不认领则退到配置（不要用异常表示「不认领」）。
    - 用户来源**优先于配置**：认领了连接名就根本不读配置。
    - 只建一次：工厂在进程内只被调用一次；框架按连接名去重（`StringComparer.Ordinal`）。
    - **释放归属**：用户交进来的连接**归用户**，框架的 `Dispose` 链**不会**释放它
      （避免与容器重复释放）。
    - **刻意不提供**「给 `ConfigurationOptions`、框架建连」这一形态：那与「框架要保证
      `AbortOnConnectFail=false`」冲突 —— 要么悄悄改掉用户显式写的 `true`，要么丢掉高可用姿态。
      这个场景由 `spring:data:redis` 配置节覆盖，且框架默认值前置铺底、用户值后置覆盖。
- **连接参数默认值**（`RedisCacheOptionsResolver.FrameworkDefaults`，显式写出以钉住容错姿态）：
  `abortConnect=false`、`connectTimeout=5000`、`syncTimeout=5000`、`asyncTimeout=5000`、
  `keepAlive=60`、`connectRetry=3`。其中**唯一与 StackExchange.Redis 上游默认值不同的是
  `abortConnect=false`**（上游是 `true`）：启动瞬间 Redis 不可达**不再抛**，而是返回一个已注册、
  尚未连上的多路复用器、后台自动重连 —— 语义上对齐 Java（Lettuce 不会因为首次连不上就拒绝创建
  连接工厂）。用户连接串里显式写 `abortConnect=true` 时**以用户为准**。
  `ClientName` 默认取应用名（`spring:application:name` → 入口程序集名），
  Redis `CLIENT LIST` 里能直接认出是哪个服务。
- **`RedisCacheManager` 新增以 `IConnectionMultiplexer` 为参数的 `protected` 构造函数**，
  用于「连接已经在我手里，只想把它包成一个 manager」的子类（`protected` 是为了让 MS DI
  只看到一个可满足的构造函数）。
- **`RedisCacheOptions` / `RedisSslOptions` / `RedisCacheOptionsResolver`** 公开，
  `RedisCacheOptions.SectionName` 是节名常量。`RedisDurationParser` / `RedisUrl` 是内部实现（Java
  `Duration` 写法与 `url` 键的解析）。

### ⚡ 性能：命中路径消除逐次反射与逐次分配

实测（Apple M1 Pro / .NET 8.0.6，见 `reports/cache-performance-analysis-20261014.md`）：改造前同步缓存命中 **957~1032 ns / 1472 B**，而它底下真正读一次缓存只要 **24~28 ns / 24 B** —— 框架开销是底层存储的 **35~40 倍**，高 QPS 下是实打实的 GC 压力。

| 项 | 现状 | 改法 |
|---|---|---|
| 拦截器路由判定（P1-1） | 每次调用读 `MethodInvocationTarget`（96 B）+ `IsDefined`（120 B），最多执行两次，合计 ~500 ns / 240 B —— 而它想省掉的 `MethodMetadataCache.GetOrAdd` 只要 11 ns / 0 B | 按 `(方法, 目标类型)` 缓存路由计划（含**否定结果**）；热路径退化成一次元组字典查询 |
| 同步读取（P0-1 / P0-3） | 每次命中 `MakeGenericMethod` + `Invoke` + `object[]` + 两次 `PropertyInfo.GetValue` 装箱整个 `CacheResult<T>` | 新增 `ISyncCacheProvider.TryGetValue(string, Type, out object?)`，非泛型同步读取；未实现的 Provider 自动回退，回退路径的泛型闭包与属性读取也改为编译委托 |
| SpEL 键生成（P0-2） | 每次 `Parse` 分配 `Dictionary` + `$"p{i}"` 拼接 + `string?[]`，实测 190~213 ns / 536 B | 编译期生成「键计划」（字面量 / 参数下标 / 属性链），渲染时两遍写 `string.Create`；**`SpElRegex()` 匹配方式一字未动，键格式不变** |
| 键前缀（P1-3） | 无 `cacheName` 时每次现拼 `$"{DeclaringType.FullName}.{Method.Name}"`，`StringBuilder` 也不给容量 | 前缀在元数据构建期预计算，`StringBuilder` 用预计算容量构造 |
| 异步路径（P1-2） | 每次调用重做 `.GetMethod(...).MakeGenericMethod(...)` | 按结果类型缓存编译后的委托 |

**键格式、命中/未命中判定、公共 API 成员签名全部未变**，因此这次改造**不需要清缓存**（与 v0.1.3 的键格式变更不同）。

此外，后端选择方式改造（`cacheManager: typeof(...)`）**也没有拖慢命中路径**：`ICacheProvider` 仍是恰好 5 个方法，`CacheInvokerCache` 与 `MethodMetadataCache` 均未改动，零分配契约不变；新增的唯一每调用开销是一次 `ConcurrentDictionary<Type, ICacheProvider>` 无锁读（比原先的 `IServiceProvider.GetRequiredService<T>()` 更便宜）。

### 🔧 修复

1. **返回值 `null` 现在照常写入缓存（对齐 Spring/Java，写侧语义反转）**（`CacheOperationHelper`）

   ⚠ **这一条与本文档早期草稿写反了，以下为准。**

   框架原先在写侧拦 null：同步入口 `WriteCache` 有 `if (invocation.ReturnValue == null) return;`，
   异步收口 `WriteCacheAsync<T>` 有 `if (value is null) return Task.CompletedTask;`。
   结果是返回 `null` 的方法**永远不写缓存**，缓存穿透场景（查不到的 id 每次打 DB）挡不住。

   这与 Spring/Java **相反**。Java 的 `AbstractValueAdaptingCache.toStoreValue` 在
   `userValue == null` 时写 `NullValue.INSTANCE` 哨兵（默认 `allowNullValues = true`，
   `CacheAspectSupport.canPutToCache` 也**不做** null 判断），因此 `@Cacheable` 方法返回 `null`
   时 null **会**进缓存，下次调用是**命中**。

   现在两处守卫都已删除，与 Java 逐条对齐。**值类型的 `default(T)`（`0` / `false`）不受影响。**

    - **逐方法 opt-out**：注解上写 `unless = "#result == null"` —— 这是 Spring 官方文档的惯用法，
      也是本框架**唯一**的逐方法 opt-out，此前的「不需要写，框架自动不缓存 null」的说法作废。
    - **store 级 opt-out**：`RedisCacheManager.AllowNullValues`（重写点，默认 `true`）与
      `MemoryCacheProvider.AllowNullValues`（属性，默认 `true`）。置为 `false` 后写 null 抛
      `InvalidOperationException`，消息对齐 Java 的
      `"is configured to not allow null values but null was provided"`。
      注意本框架刻意让「写缓存失败不影响业务返回值」，所以**它表现为一条 warning 而非 500**。

   > **redis 需要真哨兵**：`RedisValue.Null` 在 Redis 里同时表示「键不存在」与「值是 null」，
   > 直接写它会让「缓存了 null」表现为未命中，与 Memory 后端语义分裂；
   > 滑动过期的 Lua 路径还会因 `RedisValue.AssertNotNull()` 直接抛。
   > 因此新增 7 字节哨兵 `RedisNullMarker`（`0xC0 0xFF 'Y' 'Z' 'L' 0x00 0x01`），
   > 等价于 Spring Data Redis 的 `RedisCache.BINARY_NULL_VALUE`。
   >
   > **读侧语义未变**：缓存里**已有**的 `null` 条目仍算命中（`CacheResult.HasValue`
   > 区分「未命中」与「缓存 null」）—— 本次只是让写侧也照此办理。
   >
   > **不需要清缓存**：存量键不受影响；历史上异步缺陷期写进去的空串键读回仍是未命中，
   > 会自动重算并覆盖，自愈。
   >
   > **已知残留**：应用**自己**写入的、恰好等于上述 7 个字节的 `byte[]` / `ReadOnlyMemory<byte>`
   > 会被读成 `null`（`0xC0 0xFF` 是无效 UTF-8 前导字节，合法 JSON / 标量 / GZIP 信封都不会以此开头）。
   > 读侧哨兵判定排在 `byte[]` 分支**之前**，顺序不能调。

2. **`cacheName` 与 `key` 同时为空时缓存键多一个尾冒号**（`CacheKeyGenerator`）
   `[Cacheable("", "")]` 时前缀退化成方法全限定名，分隔符判定只看前缀末位，
   于是稳定生成 `Ns.Cls.M:`。该配置下键里没有任何可区分维度，分隔符没有「分隔」的对象，
   只留下一个尾冒号。现收敛为 `Ns.Cls.M`。

   **只收敛这一种组合**：`key` 非空却渲染成空串（如写成 `#nope`）时一律保持原样 ——
   那些场景下尾冒号是既有键字节的一部分（`t:users:` / `t:unknown-var:7:`），
   改成不带冒号等于静默改变存量键。

3. **建连失败的自愈不再依赖 DI 的实现细节**（`RedisConnectionProvider`，可用性缺陷）

   0.1.x 用 `services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(cs))`
   注册连接。它能「失败后自愈」，靠的是 MS DI 的一条行为：单例工厂抛异常时结果**不被缓存**，
   下一次解析会重新调用工厂（已实测：工厂连续抛 3 次，3 次解析各调用 1 次）。
   但这不是框架自己的契约，而且最自然的「只建一次」写法会把它彻底破坏 —— `Lazy<T>` 默认的
   `ExecutionAndPublication` 会**永久缓存工厂抛出的异常**（实测：3 次访问只调用 1 次工厂）：
   开机瞬间 Redis 抖动一次，该后端就在整个进程生命周期内失效。

   本版本把这条语义**显式**交给 `RedisMultiplexerHolder`：**成功才发布，失败不留痕**。
   建连失败时异常照常上抛，但 `_connection` 保持为 `null`，下一次缓存操作会重新建连，
   **不需要重启进程**。

   同时保住两条：
    - **成功后不再进工厂**：热路径退化成一次 `volatile` 读；
    - **并发首访只建一次**：慢路径加锁 + 二次检查。实测用 `LazyInitializer.EnsureInitialized`
      或 `Lazy(PublicationOnly)` 这类「不缓存异常」的替代品**没有**这个保护，16 个并发首访会
      真的建出多条连接（各一套 TCP + 认证 + 后台重连线程）。

   释放归属同时明确：**框架自己建的连接由框架释放**，**用户通过 `IRedisConnectionSource` /
   `AddRedisConnection` 交进来的连接框架绝不释放**（重复释放只释放一次，
   `Dispose` 后再访问会重新建连）。

## v0.1.3 [2026/09/30]

本版本包含三部分：**新增「缓存注解可直接标在接口方法上」的能力**、修复四个**静默失效**的缺陷（不报错、不打日志，线上表现为缓存命中率偏低）、消除一处**性能回退** —— 无缓存注解方法的纯代理开销。

### ✨ 新增：注解支持标在接口方法上

与 Java/Spring 写法一致。改造前这样写是**静默失效**的 —— 不报错、不打日志，表现为缓存命中率偏低。

```csharp
[FeignClient(name: "cache-demo-api", url: "http://localhost:16600")]
public interface ICacheDemoFeignClient
{
    // ✅ 直接标在接口方法上即可生效，不必再包一层 virtual 服务类
    [Cacheable(cacheName: "feign:users", key: "#id", ttlSeconds: 60)]
    [Get("/api/test/users/{id}/slow")]
    Task<UserDto> GetByIdSlow([PathVariable("id")] long id);
}
```

- 适用于**任何已注册进 DI 容器**的接口服务，不限于 OpenFeign：只要接口方法上带
  `[Cacheable]` / `[CachePut]` / `[CacheEvict]`，`AddEnableCaching` 就会在原注册之上
  叠一层缓存代理。
- 命中缓存时只设置返回值、不调用 `Proceed()`，因此向下的拦截器（如 OpenFeign 发 HTTP）
  根本不执行 —— **HTTP 一个字节都不会发出**。
- 内层实例严格按原服务描述符的形状构造，不改变原有构造语义：
  `ImplementationFactory` 直接调用（OpenFeign 走这条）、`ImplementationInstance` 复用实例、
  `ImplementationType` 用 `ActivatorUtilities` 构造。

### ⚠️ 升级须知：调用顺序

新能力在**调用 `AddEnableCaching` 的那一刻**扫描容器里已有的服务描述符，因此必须**最后调用**：

```csharp
builder.Services.AddBatchServices();        // 1. 业务服务
builder.Services.AddFeignStarter(...);      // 2. OpenFeign 客户端接口
builder.Services.AddEnableCaching(...);     // 3. 必须最后
```

晚于 `AddEnableCaching` 注册的接口不会被包住，且是**静默**失效。

### ⚠️ 升级须知：缓存键格式变更

`cacheName` 含 `:` 时生成的键发生了变化，**存量缓存读不到**，上线时需清一次缓存：

| 场景 | 修复前 | 修复后 |
|---|---|---|
| `cacheName:"users"` + `#id=1` | `users:1` | `users:1`（不变） |
| `cacheName:"t:users"` + `#id=1` | `t:users1` | `t:users:1` |
| `cacheName:"t:users"` + 无参字面量 key | `t:usersall` | `t:users:all` |
| `cacheName:"users:"` + `#id=42` | `users::42` | `users:42` |
| cacheName 为空（方法全限定名） | `Ns.Cls.M:1` | `Ns.Cls.M:1`（不变） |

清理方式（只影响开发/测试实例时可直接 `FLUSHDB`；生产按业务前缀删除）：

```bash
redis-cli -h <host> -p <port> --scan --pattern "<cacheName 前缀>:*" | xargs -r redis-cli -h <host> -p <port> del
```

`RedisCacheProvider.RemoveByPrefixAsync` 用 `cacheName` 作前缀扫描，新旧键都能命中，不受影响。

> 性能改动（`CacheProxyGenerationHook`）**不改键格式、不改 TTL 语义、不改命中/未命中行为**，本身无需清缓存；清缓存的要求仅来自上面的键格式变更。

### 🔧 修复

1. **SpEL 属性链键生成脏串或抛异常**（`SpelKeyParser`）
   `string.Create` 的缓冲区长度原本按**根对象 `ToString().Length`** 估算，而实际写入的是**属性链解析结果**。估算偏大 → 键中残留 `\0` 填充（如 `t:users:42\0\0\0`，永远读不回来）；偏小 → 抛 `ArgumentException: Destination is too short`。
   现改为先把各段解析到 `string?[]` 暂存数组，再按实际值累加精确长度，长度**按构造成立**，与写入内容严格一致；同时每段只解析一次。

2. **`cacheName` 含 `:` 时缓存键缺分隔符**（`CacheKeyGenerator`）
   分隔符判断看的是 `cacheName` 本身而非**已拼好的键**，导致 `cacheName:"t:users"` 生成 `t:users1`。现统一抽为 `AppendSeparator`，判断拼接结果的末位，三处调用路径（拦截器两个重载 + 手动简化重载）行为一致。

3. **async 方法防击穿完全失效**（`CacheableHandler` / 新增 `AsyncStampedeGuard`）
   `Monitor` 锁条带在 `Proceed()` 处持有，但 async 方法在**首个 await 就返回调用方**，锁随即释放且缓存仍为空，N 个并发未命中会全部穿透真实执行。
   新增按 `cacheKey` 的「在途登记表」：首个调用者（leader）原子登记并执行方法，完成后把结果/异常**发布**给等待者；后到者（follower）只等待发布，拿到同一份结果或同一异常，不重读缓存、不重入代理。同步路径的锁条带不受影响。

4. **`ValueTask<T>` 被当作同步返回值**（`MethodMetadataCache` 等）
   `typeof(Task).IsAssignableFrom(typeof(ValueTask<T>))` 为 `false`，于是框架走同步分支，把**装箱的 `ValueTask<T>` 结构体本身**写进缓存，后续调用重复 `await` 一个已消费的 `ValueTask`（违反单次消费约定，属未定义行为）。
   现识别 `Task` / `Task<T>` / `ValueTask` / `ValueTask<T>` 四种返回类型：`Task<T>` 与 `ValueTask<T>` 都缓存 `T` 本身，命中时分别用 `Task.FromResult<T>` / `ValueTask.FromResult<T>` 构造全新的已完成值；并发等待路径按返回类型返回 `Task<T>` 或 `ValueTask<T>`，避免 Castle 拆箱失败。

### ⚡ 性能：无注解方法不再进入拦截链

**改造前**：`CacheInterceptor.Intercept` 的第一行 `invocation.MethodInvocationTarget ?? invocation.Method` 每次读取分配 **96 B**、耗时约 150–205 ns；叠加 Castle 代理本身 136 B 的地板价，导致「不带缓存注解的方法」每次调用要付 **226.8 ns / 232 B**（直调只要 3.9 ns / 0 B）。对注解稀疏的服务（类里大量方法不需要缓存），这是唯一变差的地方。

**改造后**：新增 `CacheProxyGenerationHook`（`IProxyGenerationHook`），在**代理生成期**离线判定哪些方法需要拦截 —— 判定为 `false` 的方法，其代理实现**直接调用目标，拦截链根本不存在**（不是「进拦截器后判空 `Proceed`」）。

| 场景 | 改造前 | 改造后 |
|---|---|---|
| 无注解方法 | 226.8 ns / 232 B | **4.74 ns / 0 B**（与直调 4.76 ns 不可分辨） |
| 有注解方法 | 完整缓存管道 | 不变（零回退） |

判定口径与运行时严格一致：接口代理用 `Type.GetInterfaceMap` 把接口方法映射到**实现方法**再判注解；class 代理直接判实现方法。在此基础上，本版本同时把口径扩展为「**实现方法 ∪ 接口方法**」并集判定 —— 即「注解标在接口方法上」也生效，见上文。

### 📌 已知限制（非缺陷，行为不变）

- 非泛型 `Task` / `ValueTask`（无返回值）没有可缓存的 `T`，每次真实执行。
- `void` 方法没有可缓存的值，不写缓存。这不是新增限制，而是**补上了守卫**：
  原先它由「返回 null 就不写」的守卫顺带挡住，该守卫删除后必须显式判定
  `metadata.ReturnType == typeof(void)`，否则 `CacheInvokerCache` 会执行
  `SetAsyncTemplate.MakeGenericMethod(typeof(void))` 并抛异常（被外层 catch 吞成每次调用一条 warning）。
- leader 的方法若永不完成，在途登记表条目会一直留存、同 key 的后续调用持续等待（与 FusionCache 行为一致，本版本不引入超时）。
- `CacheEvict` 对 async 方法仍是「等 `RemoveAsync` 完成再 `Proceed`」——无正确性问题，仅占用一个线程。
- 「注解标在接口方法上」这条路径**不处理键控注册**（`IsKeyedService`）的接口。
- `Scoped` 接口在 `ImplementationType` 分支下，每次解析都会新建内层对象（代理本身仍按作用域生命周期缓存），与原生的「作用域内复用同一实例」有差异。属已知退化，不影响缓存正确性。
- 判定被提前到代理生成期并由 Castle 缓存（`ProxyGenerator` 是 DI 单例，全应用共用代理类型缓存），因此 hook 的 `Equals`/`GetHashCode` 必须反映判定结果。已用单元测试锁定。
- `GetMethods()` 只返回公共方法，因此**显式接口实现**的缓存注解仍不生效（与改造前一致；接口方法上的注解也不会因并集判定而流向实现方法）。

## v0.1.2 [2026/06/29]

### 🔧 修复
1. **更新命名空间引用**：`Yzl.Extensions.Common` → `Yzl.Extensions.Core`，适配 Core 包重命名
2. **更新文档和 README 中的命名空间示例**：同步 `Yzl.Extensions.Common.Ioc` → `Yzl.Extensions.Core.Ioc`

## v0.1.1 [2026/06/26]
1. 重构 CacheInterceptor：
    - 拆分为 CacheInterceptor（薄拦截层）、CacheOperationDispatcher（调度器）、CacheableHandler/CachePutHandler/CacheEvictHandler（独立处理器）
    - 新增 ICacheOperationHandler 接口，支持自定义处理器扩展
2. 性能优化：
    - 新增 MethodMetadataCache 缓存方法级反射元数据，消除热路径反射开销（属性查找提速 15 倍）
    - 预编译 SpEL 表达式为固定段列表，消除每请求 Regex 开销（SpEL 解析提速 2 倍）
    - 新增类型属性反射缓存，消除每次 GetProperties + GetCustomAttribute
    - 新增 string.Create 预分配字符串，减少 StringBuilder 堆分配
3. 新增特性：
    - 新增 CachePutAttribute：始终执行方法并写入缓存
    - 新增 CacheConfigAttribute：类级别缓存默认配置，支持字段级覆盖
    - 新增滑动过期支持（slidingTtl/SlidingExpirationSeconds），配合绝对 TTL 兜底
    - 新增 ICacheProvider.TouchAsync 接口，支持 Redis KeyExpire 滑动延期
    - MemoryCacheProvider 写入时支持 SlidingExpiration 选项
    - ttl / slidingTtl 简写属性，新项目无需使用 ttlSeconds / slidingExpirationSeconds
    - 新增 IOrdered 接口，支持处理器排序
4. 正确性修复：
    - 修复 ICacheProvider.GetAsync 返回 CacheResult&lt;T&gt;，支持缓存 null 值（区分"未命中"和"缓存 null"）
    - 修复 CacheEvictAttribute 声明为 abstract 导致无法使用的问题
    - 修复代理注册扫描条件，现在同时扫描 Cacheable/CachePut/CacheEvict 三种属性
    - 修复 CacheOperationHelper 异步写缓存使用 ContinueWith，异常不影响业务返回值
    - 移除 GetCacheProvider 静默吞异常逻辑，改为 Logger 记录后抛出
5. 新增 Condition/Unless 条件缓存：
    - Cacheable/CachePut/CacheEvict 新增 Condition 属性，表达式为 true 时才执行缓存逻辑
    - Cacheable/CachePut 新增 Unless 属性，表达式为 true 时抑制缓存写入
    - 新增 SpelConditionEvaluator：支持比较运算、逻辑运算、字符串/数值/布尔比较
    - 组合使用：condition + unless 可同时作用于同一方法
    - Condition 和 Unless 为空时零额外开销
    - 异步 Task{T} 方法通过 continuation 支持 unless 评估
6. 支持单文件发布
7. 依赖Yzl.Extensions.Common v0.1.4

## v0.1.0
1. 初始化发布