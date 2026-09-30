# Yzl.Extensions.Cache 更新日志

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