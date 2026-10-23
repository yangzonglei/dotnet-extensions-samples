# Yzl.Extensions.Cache.Redis 更新日志

## v0.2.0 [2026/10/23]

首次发布。本包从 `Yzl.Extensions.Cache` 中拆出，成为 Redis 后端的独立 NuGet 包。
上一个已发布版本是 `Yzl.Extensions.Cache` **0.1.3**（Redis 实现在该包内），升级步骤见下方「升级须知」。

> **版本号为什么是 0.2.0 而不是 0.1.0**：本包与 `Yzl.Extensions.Cache` 0.2.0 配套发布，
> 版本号与之对齐。此前 Redis 实现位于 `Yzl.Extensions.Cache` 包内的
> `Yzl.Extensions.Cache.Providers` 命名空间下，从未单独发布过包。

**引用即生效**：包内含一个 `ICacheServiceConfigurator`，`AddEnableCaching()` 会扫描程序集并执行它。
因此只要项目引用了本包，注解上写 `cacheManager: typeof(RedisCacheManager)` 就能用 ——
**不需要** `AddRedisCache()` 之类的注册调用，也**不需要**给 `AddEnableCaching` 加参数。

### 🚀 新功能

1. **`RedisCacheManager` —— 「选后端」的落点**

   ```csharp
   [Cacheable("users", "#id", ttlSeconds: 300, cacheManager: typeof(RedisCacheManager))]
   public virtual UserDto GetUser(int id) => ...;
   ```

   命名对齐 Java 的 `RedisCacheManager`：管理器负责「选后端」，具体读写由背后的
   `RedisCacheProvider` 完成。提供四个 `protected virtual` 重写点：

   | 重写点 | 默认值 | 作用 |
      |---|---|---|
   | `ConnectionStringKey` | `redis:main-site` | 连接名：扁平配置键；没有该键时回落到 `spring:data:redis` |
   | `DatabaseIndex` | `null` | 库索引；`null` = 沿用连接串 / 配置里的 `database` |
   | `Serializer` | `null` = `NewtonsoftRedisCacheSerializer` | 复杂类型的序列化器 |
   | `AllowNullValues` | `true` | 是否允许把 `null` 写入缓存（store 级开关） |

   继承它写一个一行子类就能连第二个实例或另一个 DB 索引：

   ```csharp
   public class DownloadTaskRedisManager(IRedisConnectionFactory f) : RedisCacheManager(f)
   {
       protected override string ConnectionStringKey => "redis:download-task";
       protected override int?   DatabaseIndex       => 13;
   }
   ```

   **子类不需要任何注册代码**（`sealed` 子类同样是）：核心包 `AddEnableCaching(assemblies: ...)`
   会扫描并自动注册范围内的所有具体 `ICacheManager` 实现。
   这与 Java `@Bean("downloadTaskRedisTemplate")` 那种「每个命名模板各带一套连接 + 序列化器」
   是同一个形态 —— 只是 .NET 这边的「名字」是 `cacheManager` 上的那个 `Type`。

   `DatabaseIndex` 的优先级是**代码覆盖配置**：子类显式给了值就以代码为准。
   这一点与参考的 Java 实现**有意不同**：那边 `downloadTaskRedisTemplate` 声明了
   `@Value("${spring.data.redis.database}")` 却给连接工厂传了字面量 `0`，配置被静默忽略。本实现不复制那个缺陷。

2. **`IRedisConnectionFactory` / `RedisConnectionFactory` —— 按连接名去重**

   指向同一个 `ConnectionStringKey` 的多个管理器复用同一个 `ConnectionMultiplexer`
   （序列化器与库索引由各自的 `RedisCacheProvider` 承担，因此「同一连接 + 不同序列化器 / 不同库索引」是允许的）。
   取用**不建连**，首次读写才建。连接名的数量应当是常数级（等于后端数量），**不要**用业务 ID 当连接名。

3. **`IRedisConnectionSource` / `AddRedisConnection(...)` —— 用户自定义连接**

   对齐 Java「自己声明一个 `@Bean RedisConnectionFactory`」：

   ```csharp
   // 已有连接实例：框架只借用，不释放
   services.AddRedisConnection("redis:report", myMultiplexer);

   // 自己建，工厂在进程内只被调用一次
   services.AddRedisConnection("redis:cas-auth", sp =>
       ConnectionMultiplexer.Connect(sp.GetRequiredService<IConfiguration>()["redis:cas-auth"]!));
   ```

   契约：

   | 契约 | 说明 |
      |---|---|
   | **`null` = 不认领** | 交给链上的下一个来源，全部不认领则退到配置。不要用抛异常表示「不认领」 |
   | **用户连接优先** | 只要某个来源认领了连接名，**连配置都不会去读** |
   | **只创建一次** | 框架按连接名去重（`StringComparer.Ordinal`），但「只创建一次」最终由实现保证 |
   | **释放归属** | 交进来的连接**归用户**，框架的 `Dispose` 链**不会**释放它 |
   | **失败不留痕** | 工厂抛异常后框架不记住失败，下一次缓存操作会重新调用它 |

   **刻意不提供**「给 `ConfigurationOptions`、框架建连」这一形态：那个形态在语义上承诺「原样使用」，
   与「框架要保证 `AbortOnConnectFail=false`」直接冲突 —— 要么悄悄改掉用户显式写的 `true`，
   要么丢掉高可用姿态。两个都不能接受。这个场景由 `spring:data:redis` 覆盖，且那边框架默认值前置铺底、
   用户值后置覆盖，做得更好。

4. **`spring:data:redis` 配置节 —— 逐字段对齐 Java `RedisProperties`**

   节名与字段名都与 `org.springframework.boot.autoconfigure.data.redis.RedisProperties` 一致：

   ```yaml
   spring:
     data:
       redis:
         host: 10.0.0.1
         port: 6379
         database: 0
         password: xxx
         client-name: my-service
         timeout: 5s
         connect-timeout: 5s
         ssl:
           enabled: false
   ```

   | 配置键 | 默认 | 说明 |
      |---|---|---|
   | `url` | — | `redis://user:password@host:port`。给出它时由它决定 host/port/用户名/密码；`rediss://` 隐含 TLS；URL 路径里的库索引**被忽略**（与 Java 一致） |
   | `host` | `localhost` | 给出它时由它决定端点（连接串里的端点被整体替换，不是追加） |
   | `port` | `6379` | 只给 `port` 不给 `host` 时，覆盖连接串里那个端点的端口（主机保留） |
   | `username` | — | Redis 6+ ACL 用户名。只给 `password` 时是传统 `AUTH` |
   | `password` | — | 以属性赋值生效、不经连接串解析，**因此可以包含逗号** |
   | `database` | `0` | 默认库索引（映射 `defaultDatabase`） |
   | `client-name` | 应用名 | Redis `CLIENT LIST` 里显示的客户端名。连接串里的关键字是 `name=`，不是 `clientName=` |
   | `timeout` | `5000ms` | 命令超时，**同步与异步共用**（Java 的 Lettuce 也只有一个命令超时） |
   | `connect-timeout` | `5000ms` | 建连超时 |
   | `ssl.enabled` | `false` | 是否启用 TLS。是**嵌套节点**，不是扁平的 `ssl` |
   | `connection-string` | — | **本包扩展（Java 无）**：原样连接串逃生口，本节没建模的 StackExchange.Redis 参数写这里 |

    - **整节只描述「一个」Redis**，与 Java 完全一致 —— 没有 `default:` / `connections:<名>` 这类
      Java 里不存在的子节点。多 Redis 走代码（第 3 条的 `IRedisConnectionSource` / `AddRedisConnection`）。
    - **`Duration` 写法**（`timeout` / `connect-timeout`）：`500ms` / `5s` / `1m` / `2h` / `1d`
      （**无单位按毫秒**，与 Spring Boot 一致）、ISO-8601 `PT5S`，另接受 .NET 的 `00:00:05`。
      **写错直接抛 `FormatException`，不静默退回默认值。**
    - **`url` 语义对齐 `LettuceConnectionConfiguration.getStandaloneConfig`**：它决定
      host / port / username / password，而 `database` **仍只来自 `database` 字段**
      （Java 那边是先按 URL 设 host/port/user/pass，再统一 `setDatabase(properties.getDatabase())`）。
    - ⚠ **`connection-string` / `client-name` 的键名带连字符**：绑定器只做**大小写**不敏感匹配，
      不做连字符到驼峰的转换 —— 写成 `connectionString` 绑不上，且**不抛异常、不报错**，整条连接串**静默丢失**。

5. **两级查找（`RedisCacheOptionsResolver`）**

    1. **按连接名的扁平连接串键** `configuration[连接名]`（即 `redis:main-site` 这种，既有部署的形态）
    2. `spring:data:redis` 这一节（且给了端点）
    3. 都没有 ⇒ 抛「缺配置」的 `InvalidOperationException`

   扁平键排在前面，是因为它**只对那一个连接名生效**、比全局唯一的 `spring:data:redis` 更具体 ——
   与 Java 里「自己声明的 `@Bean RedisConnectionFactory` 优先于自动配置」同一方向。
   于是给全局配置节加一节，不会把既有部署里指向别的实例的连接悄悄改道。

   第 2 级要求「给了端点」（`url` / `host` / `connection-string` 之一）：**只配了超时没配地址**
   与**整节不存在**同样对待，都算未配置。这是本包**有意与 Java 不同**的一处 ——
   Java 那套默认值（没写 host 就是 `localhost`）服务于「什么都不配也能启动」的自动配置，
   本包没有自动配置，凭空补一个 `localhost` 只会把「你忘了配地址」变成「连上了但数据不对」。

6. **框架连接默认值**

   以连接串片段的形式铺在**用户连接串之前**，因此用户逐项写下的值总能生效：

   ```
   abortConnect=false, connectTimeout=5000, syncTimeout=5000, asyncTimeout=5000, keepAlive=60, connectRetry=3
   ```

   这些值大多与 StackExchange.Redis 自身默认值相同，**仍然显式写出来**是为了把连接层的容错姿态
   钉在本框架这一侧：将来升级 StackExchange.Redis 时，上游改默认值不会静默改变线上行为。
   唯一与上游默认值**不同**的是 `abortConnect=false`（上游是 `true`）：
   启动瞬间 Redis 不可达**不再抛**，而是返回一个已注册、尚未连上的多路复用器、后台自动重连 ——
   语义上对齐 Java（Lettuce 不会因为首次连不上就拒绝创建连接工厂）。用户显式写 `abortConnect=true`
   时**以用户为准**（那时连不上仍会抛）。

   `ClientName` 默认取应用名（`spring:application:name` → 入口程序集名），
   Redis `CLIENT LIST` 里能直接看出是哪个服务连上来的。

7. **序列化器：`IRedisCacheSerializer` 与两个内置实现**

   | 实现 | 说明 |
      |---|---|
   | `NewtonsoftRedisCacheSerializer` | 默认。与拆包前的私有实现**逐字节一致**（含 `> 1024` 字节自动 GZIP、`CompressionLevel.Optimal`、GZIP 魔数 `0x1F 0x8B` 判定），旧缓存数据继续可读 |
   | `SystemTextJsonRedisCacheSerializer` | .NET 官方自带，不引入 Newtonsoft 运行时依赖 |

   共同的「信封」逻辑（UTF-8、1KB 阈值 GZIP、魔数判定）在基类 `RedisCacheSerializerBase`，
   子类只实现 `ToJson` / `FromJson`。

   ⚠ **序列化器只作用于复杂类型**：`string` / `int` / `long` / `double` / `bool` / `byte[]` /
   `ReadOnlyMemory<byte>` 由 `RedisCacheProvider` 直接读写裸值，不经过序列化器。
   这样做是为了标量路径零分配、换序列化器不影响标量键、redis-cli 里能直接看到原文
   （等价于 Java `StringRedisSerializer` 的效果）。

   ⚠ **不要在同一个 `cacheName` 上换序列化器**：两个实现对同一对象产生的字节不同
   （浮点最直观：Newtonsoft 写 `1.0`，System.Text.Json 写 `1`；枚举、命名策略同理），
   换过去旧数据读不回来，且失败方式是**静默的** —— 反序列化异常被 `CacheableHandler.TryReadCache`
   吞成未命中，表现为「缓存永远不命中」而不是报错。

8. **`RedisNullMarker` —— 「缓存了 null」的真哨兵**

   用户侧的 `null` 不走序列化器：provider 写 null 时落 **7 字节哨兵**
   `0xC0 0xFF 'Y' 'Z' 'L' 0x00 0x01`，读回时识别它并还原成 `null` / `default(T)`。
   等价于 Spring Data Redis 的 `RedisCache.BINARY_NULL_VALUE`。

   > **为什么不能直接写 `RedisValue.Null`**：Redis 里它同时表示「键不存在」与「值是 null」，
   > 两者必须分开，否则「缓存了 null」会表现为未命中，与 Memory 后端语义分裂。
   > 另外滑动过期的 Lua 路径把值当 `ARGV` 传递，`RedisValue.AssertNotNull()` 会对空值直接抛 ——
   > 哨兵必须是真实字节序列，且长度非零（`RedisValue.HasValue` 的定义是「非零长度或可解释为整数」）。
   >
   > **已知残留**：应用**自己**写入的、恰好等于这 7 个字节的 `byte[]` / `ReadOnlyMemory<byte>`
   > 会被读成 null。`0xC0 0xFF` 是无效 UTF-8 前导字节（合法 JSON、标量、GZIP 信封都不会以此开头），
   > 所以唯一的撞车路径就是「应用把这两个字节当数据」。读侧哨兵判定排在 `byte[]` 分支**之前**，
   > 顺序不能调（放错位置的症状是「缓存了 null 的方法读回一个 7 字节数组」，不抛异常、只静默返回错值）。

9. **`RedisCacheServiceConfigurator` —— 自注册入口**

   `AddEnableCaching()` 会扫描程序集并执行所有 `ICacheServiceConfigurator`，因此**引用本包即生效**。
   它只做三件事：绑定 `spring:data:redis`（推迟到 options 求值时再取 `IConfiguration`）、
   注册解析器与连接工厂、注册 `RedisCacheManager` 基类。用户写的子类由核心包自动注册。

   > **扫描为什么不用 `App.Assemblies`（带 `FromSolution`）**：那条路只看 `DependencyContext` 里
   > `type == "project"` 的库，以 **NuGet 包形态**引用的本包会被排除 —— 自注册在发布形态下静默失效，
   > 而本地源码引用（`type == "project"`）永远测不出来。因此这里与 Actuator 的
   > `CreateConfigurators()` 一致，走不带 `FromSolution` 的全量扫描。

10. **连接层高可用：`RedisMultiplexerHolder`**

    | 项 | 做法 |
        |---|---|
    | 开机瞬间 Redis 不可达 | `abortConnect=false`（框架默认）：不抛，返回已注册未连上的多路复用器，后台自动重连 |
    | 建连失败后的自愈 | **成功才发布，失败不留痕**。刻意不用 `Lazy(ExecutionAndPublication)`：它会把异常永久缓存 ⇒ 一次抖动 = 该后端到进程重启为止一直失效 |
    | 并发首访 | 慢路径加锁 + 二次检查 ⇒ 工厂**恰好被调用一次**，不会因为并发启动建出多条连接 |
    | 用户交进来的连接 | 框架只借用，**不释放** |
    | 不做的 | Redis Sentinel / Cluster（Java 侧无先例）、Actuator Redis 健康检查（对齐 Java：不让 Redis 抖动把 health 拖红）、连接池 / `ClientResources`（SE.Redis 是异步多路复用，没有等价项） |

11. **迁移过来的既有能力**

    `RedisCacheProvider` 与 `RedisSlidingTtlScript` 随包迁移。**键格式与存量数据完全兼容**：
    标量裸值存放、`> 1024` 字节 GZIP 信封（`CompressionLevel.Optimal`、魔数 `0x1F 0x8B`）、
    键的拼装规则都与 0.1.3 一致，**存量键与缓存不需要清**。

    内部改动集中在三处：序列化器抽成可替换的 `IRedisCacheSerializer`、库索引与
    `allowNullValues` 变成构造参数、连接由惰性持有者提供。

    ⚠ **滑动过期的绝对上限与伴生键 `<key>:__yzl_slide_deadline` 是本版本新增的**，
    0.1.3 里没有 `ISlidingCacheProvider`，`TouchAsync` 只是无条件续期（热 key 永不过期）。
    升级后滑动条目会开始写第二个键 —— 这是新键，不与存量数据冲突。

### ⚠️ 升级须知：从 `Yzl.Extensions.Cache` 0.1.x 迁移

1. **类型搬到新包、新命名空间**

   | 改造前（0.1.x） | 改造后 |
      |---|---|
   | `Yzl.Extensions.Cache.Providers.RedisCacheProvider` | `Yzl.Extensions.Cache.Redis.RedisCacheProvider` |

   需要 `using Yzl.Extensions.Cache.Redis;`，并在项目里引用新包。
   `ICacheProvider` 仍留在核心包、命名空间不变 —— 自己实现的 provider 不受影响。
   （`ISyncCacheProvider` / `ISlidingCacheProvider` 是 v0.2.0 新增的**可选**接口，
   在 0.1.x 里并不存在；不实现它们也能正常工作，只是滑动过期没有绝对上限。）

2. **`AddEnableCaching` 不再接收 Redis 参数**

   ```csharp
   // 改造前
   builder.Services.AddEnableCaching(assemblies: null, enableRedis: true,
                                     redisConnectionString: builder.Configuration["redis:main-site"]);

   // 改造后 —— 引用本包即生效
   builder.Services.AddEnableCaching();
   ```

   配置键仍是 `redis:main-site`，改由本包**惰性读取**。

3. **注解上不再写 `cacheType:`**

   | 改造前 | 改造后 |
      |---|---|
   | `[Cacheable("users", "#id", cacheType: CacheType.Redis)]` | `[Cacheable("users", "#id", cacheManager: typeof(RedisCacheManager))]` |
   | `[CacheConfig(defaultCacheType: CacheType.Redis)]` | `[CacheConfig(cacheManager: typeof(RedisCacheManager))]` |

   参数**位置**未变，按位置传参的写法不受影响。

4. **`RedisCacheManager` 是**新增**类型（没有「改造前」，无需迁移）**

   0.1.x 没有管理器这一层：注解写 `cacheType: CacheType.Redis`，`RedisCacheProvider` 由
   `AddEnableCaching(enableRedis: true, redisConnectionString: ...)` 注册成容器单例。
   v0.2.0 才引入 `RedisCacheManager`（命名对齐 Java 的 `RedisCacheManager`），
   由它按「连接名 + 序列化器 + 库索引」构造各自的 `RedisCacheProvider`。
   所以不存在「继承过某个旧 `RedisCacheManager`」的代码，按第 2、3 条改注册与注解写法即可。

   管理器上还有一个 `protected` 构造函数 `(IConnectionMultiplexer, IRedisCacheSerializer?, int?, bool)`，
   用于「连接已经在我手里」的场景。**之所以是 `protected` 而非 `public`**：DI 在容器里同时存在
   `IRedisConnectionFactory` 与 `IConnectionMultiplexer` 时会看到两个「参数个数不同但都可满足」的
   公共构造函数，`GetRequiredService<RedisCacheManager>()` 直接抛 `The following constructors are ambiguous`
   （已实测）；`protected` 对 MS DI 完全不可见。

   ⚠ 这条构造上的 `serializer` / `databaseIndex` / `allowNullValues` 是**显式参数**且**优先于**
   对应的重写属性 —— 与「连接串写 `defaultDatabase`」那条规则（代码覆盖配置）方向相反，
   因为构造函数参数比子类上的 `virtual` 属性更接近调用点。不打算用参数写值就别传。

5. **`RedisConnectionProvider` / `RedisCacheProvider` 不再注册进容器**

   provider 必须绑定**具体的连接 + 序列化器 + 库索引**，只有管理器（或用户手写的 `ICacheManager`）
   才知道该绑哪一套。`GetService<RedisCacheProvider>()` 会返回 `null` ——
   请走 `IRedisConnectionFactory.Get(name)` 再 `new`。

6. **连接参数改为惰性读取、首次读写才建连**

   改造前：`AddEnableCaching(enableRedis: true, redisConnectionString: ...)` 在**注册期**就需要连接串
   （空则立即抛 `ArgumentNullException`），多路复用器在第一次解析 `RedisCacheProvider` 时建立 ——
   即「有缓存调用就会建连」，哪怕那次调用的 key 其实从内存命中不了。影响：

    - 引用了本包但一个 Redis 注解都没写的应用，不会在启动期因 Redis 不可达而失败（改造前它得先
      把一个连接串塞给 `AddEnableCaching`，否则注册期就抛）；
    - 连接参数（host / port / database / 超时 ……）全部来自配置，注册期不读取任何配置。

   释放归属同时明确下来：**框架自己建的连接由框架释放**；用户通过 `RedisConnectionProvider` /
   `AddRedisConnection` 交进来的连接**框架不释放**（那份连接通常也是容器单例，由容器自己释放）。
   改造前只有一个容器单例，释放由 MS DI 负责，没有这条歧义。

7. **`abortConnect=false` 成为框架默认值（连接行为变更）**

   「启动瞬间 Redis 不可达」从「抛异常」变成「返回一个已注册、尚未连上的多路复用器，后台自动重连」。
   原先靠启动即抛来尽早暴露 Redis 配置错误的部署，升级后不会再在启动期看到异常。
   想保留旧行为：在连接串或 `spring:data:redis.connection-string` 里显式写 `abortConnect=true`。

### 🔧 修复

1. **建连失败的自愈不再依赖 DI 的实现细节**

   0.1.x 的形态是 `services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(cs))`。
   它能「失败后自愈」，靠的其实是 MS DI 的一条行为：单例工厂抛异常时结果**不被缓存**，
   下一次解析重新调用工厂（已实测：工厂连续抛 3 次，3 次解析各调用 1 次）。这不是本框架的契约，
   而且最容易想到的「只建一次」写法会把它彻底破坏 —— `Lazy<T>` 默认的
   `ExecutionAndPublication` 会**永久缓存工厂抛出的异常**（实测：3 次访问只调用 1 次工厂）：
   一次开机抖动（或网络切换、DNS 未就绪）＝该后端到进程重启为止一直失效。

   本版本把这条语义**显式**交给 `RedisMultiplexerHolder`：**成功才发布，失败不留痕**。
   建连失败时异常照常上抛，但内部引用保持为 `null`，下一次缓存操作会重新建连，**不需要重启进程**；
   同时保住另外两条：成功后不再进工厂（热路径退化成一次 `volatile` 读）、并发首访只建一次
   （慢路径加锁 + 二次检查）。

   > `LazyInitializer.EnsureInitialized` 与 `Lazy(PublicationOnly)` 都不缓存异常，
   > 但它们**没有**「成功后不再重入」的快速路径语义 —— 实测 16 个并发首访会真的建出
   > 多条连接（各一套 TCP + 认证 + 后台重连线程）。

2. **`RedisValue.Null` 无法区分「缓存了 null」与「键不存在」**

   改为 7 字节哨兵（见新功能第 8 条）。0.1.3 的写侧对 null 是分叉的：同步入口有
   `if (invocation.ReturnValue == null) return;` 守卫，异步路径却把 `null` 原样交给 `SetAsync`
   （`ConvertToRedisValue(null)` 返回 `RedisValue.Null`）。而 `RedisValue.Null` 在 Redis 里
   同时表示「键不存在」与「值是 null」—— 只要空值真的落了库，读回就无法与未命中区分，
   与 Memory 后端「缓存了 null 也是命中」的语义分裂。

   v0.2.0 起写侧对齐 Spring（null 照常写入，见核心包更新日志），因此哨兵成为**必需**而非可选。

3. **滑动写入遇到 null 值时的崩溃**（滑动写入本身是 v0.2.0 新增的能力）

   滑动写入把值当 Lua 的 `ARGV` 传，`RedisValue.AssertNotNull()` 对空值直接抛
   `ArgumentException("A null value is not valid in this context")`。哨兵是真实字节序列，
   天然规避这条路径。0.1.3 的 `TouchAsync` 只发 `EXPIRE`、不传值，所以不会遇到这个坑；
   新的 `SetSlidingAsync` 必须同时写值与 TTL，才会触达。

### 📌 已知限制（行为取舍，不是缺陷）

- **不在容器内提供 `IConnectionMultiplexer` 单例**。默认路径的连接由 `RedisConnectionFactory`
  按连接名持有与释放；容器里注册一个全局单例会让「多后端各连各的」失去落点。
  只有走「直接给连接」那条路时才需要自己注册。
- **`RedisConnectionFactory` 不得实现 `ICacheServiceConfigurator`**。`AddEnableCaching` 会
  `Activator.CreateInstance` 扫描到的**所有**实现者，把工厂写进去会导致同一类型被当成两种角色实例化。
- **不支持 Redis Sentinel / Cluster 的拓扑配置**。Java 侧无先例，且 SE.Redis 的拓扑写法
  与 Lettuce 差异过大，直接映射会得到一个似是而非的配置面。
- **`connection-string` 里不能写含逗号的密码**。连接串用逗号分隔参数、无法转义 ——
  改用 `password` 字段（以属性赋值生效，不经连接串解析）。
- **不支持 IPv6 字面量以外的特殊端点语法**（Unix socket 等）。SE.Redis 的端点语法与 Java 不同，
  本项目只对齐两种写法都支持的形态。
- **打错配置键的后果与「管理器类型没注册」刻意不同**：前者在首次触达连接时抛
  `InvalidOperationException`，但读/写路径都会 `catch (Exception)` 吞掉 ⇒ **缓存静默不生效**，不是 500；
  后者穿透 `CacheOperationDispatcher` 的降级过滤器 ⇒ **接口 500**。
  第二种是部署配置问题（改配置就能修），降级比 500 更合适，但它**不会报错** ——
  新增后端时务必确认连接名真的配得上；缺配置时异常消息里会带上**那个连接名**与 `cacheManager`。

### 验证

- 全量单测 519 通过 / 0 失败，其中本包相关约 113 例：多 Redis 后端（15）、
  配置绑定与两级查找（4 + 27）、`url` / `Duration` 解析、连接来源（8）、
  多路复用器持有者的失败不留痕与并发首建（10）、null 哨兵（10）、
  序列化器字节一致性（15）、惰性连接（7）、框架默认值（7）、滑动过期脚本（10）。
- samples 以 `-p:UseProjectReference=true` 构建通过；起宿主对真实 Redis 端到端验证：
  配置节路径落在 `database=5` 且 `CLIENT LIST` 出现配置的 `client-name`；
  用户来源路径压过同名扁平键（配置写 db 7、实际落 db 8，db 7 为空）；
  用户来源建连计数保持 1。
- 键格式、命中/未命中判定未变，本版本**不需要清缓存**。

---

## 相关文档

- 用法与坑位说明：`README.md`（本目录）
- 核心包的整体语义（注解、SpEL、null 策略、性能基线）：`../Yzl.Extensions.Cache/README.md`
  与 `../Yzl.Extensions.Cache/Yzl.Extensions.Cache.md`
- 核心包更新日志：`../Yzl.Extensions.Cache/ReleaseNotes.md`
