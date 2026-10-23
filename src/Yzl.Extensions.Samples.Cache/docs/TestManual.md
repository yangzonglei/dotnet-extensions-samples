# Cache 测试手册

本手册验证 `Yzl.Extensions.Cache` 的全部缓存能力：`[Cacheable]` / `[CachePut]` / `[CacheEvict]`、SpEL 键表达式、`Condition` / `Unless` 条件缓存、滑动过期、`[CacheConfig]` 继承、Redis 与内存双提供器。

- 示例项目：`src/Yzl.Extensions.Samples.Cache`（端口 **16605**）
- 所有测试端点统一前缀 **`/api/samples`**
- 导航页：<http://localhost:16605/api/samples>（HTML，列出全部端点）
- 仪表盘：<http://localhost:16605/dashboard>
- 统计接口：`GET /api/samples/stats`（汇总各服务计数器，**受服务生命周期限制恒为 0，见 §1 说明**）

> ⚠️ **判定缓存是否命中，请以 `elapsedMs` 为准。** 本示例的服务全部用
> `[IocService(lifetime: ServiceLifetime.Transient)]` 注册，每个 HTTP 请求都会新建实例，
> 因此响应里的 `callCount` 只能反映**本次请求是否真正执行了方法体**（未命中 `1` / 命中 `0`），
> 不具备跨请求的累计语义；`/api/samples/stats` 与各 `*-count` 端点同理，永远返回 `0`。
> 详见 §1 末尾的说明。

### 在仪表盘上测试 POST 端点

手册里所有 `curl -X POST ... -d "..."` 的用例，都可以直接在 <http://localhost:16605/dashboard>
面板上完成：写方法（`POST`/`PUT`/`PATCH`/`DELETE`）的路由条下会自动展开该端点需要的参数。

- `[FromForm]` 参数 → 预填好参数名的「Form 参数」键值行（如 2.2 的 `id=1&name=Alice&age=25&email=test@test.com`），
  改完值点「▶ 测试」即以 `application/x-www-form-urlencoded` 发送
- `[FromQuery]` / `[FromHeader]` 参数 → 对应的「Query 参数」/「请求头参数」行
- 路径模板里的 `{id}` → 已有的「URL 参数」输入框，无需手工拼 URL
- 复杂类型（`[FromBody]`）→ 预填好 JSON 骨架的文本框，以 `application/json` 发送
- 端点未声明任何可绑定参数（自行读 `Request.Body`）→ 提供「原始请求体」文本框，内容原样发送，留空则不发送 body

> 面板只会把**填写了参数名的行**拼进请求，所以默认预填值可以直接使用；改动后无需重启服务。


---

## 0. 启动

```bash
cd /Users/yangzonglei/Documents/code/github.com/dotnet/dotnet-extensions-samples

# 清理残留进程
lsof -ti:16605 | xargs kill -9 2>/dev/null

# 启动（默认：仅内存缓存）
dotnet run --project src/Yzl.Extensions.Samples.Cache
```

启动日志只有一行（Redis 不再影响启动）：

```text
✓ 缓存已启用（内存后端默认可用；Redis 连接按需建立：配置走 spring:data:redis，用户来源优先）
```

### 关于 Redis

`Program.cs` 里只有一行，不含任何 Redis 参数：

```csharp
builder.Services.AddEnableCaching();
```

Redis 后端由 `Yzl.Extensions.Cache.Redis` 包提供，**引用即生效**：只要项目引用了该包，
包内的 `RedisCacheServiceConfigurator` 就会在 `AddEnableCaching()` 扫描时被调用，
把 `RedisCacheManager` 注册进容器 —— 不需要任何 `AddRedisCache()` 之类的调用。

注解里显式写 `cacheManager: typeof(RedisCacheManager)` 的方法才会走 Redis：

```csharp
[Cacheable(cacheName: "redis:users", key: "#id", ttlSeconds: 300, cacheManager: typeof(RedisCacheManager))]
```

**连接是首次使用时才建立的。** 连接参数有两个来源（第十章详述）：

1. `appsettings.json` 的 `spring:data:redis` 节 —— 与 Java `RedisProperties` 同形的结构化配置
   （`host` / `port` / `database` / `timeout` / `connect-timeout` / `client-name`）。
   **这一节只描述一个 Redis**，10.1 的 `redis:samples-config` 就落在它上面。**刻意不含任何凭据。**
2. **按连接名的扁平连接串键** —— `appsettings.json` 里的 `redis:samples-injected`，
   以及 `appsettings.Development.json` 里的 `redis:main-site`、`redis:cas-auth`、
   `redis:data-protect`。旧形态，**永不失效**，且**优先于**上面那一节（第八章用的就是它）。

> ⚠️ **该文件在仓库里、且是本地示例凭据**：`appsettings.Development.json` 由 git 跟踪
> （`.gitignore` 里没有任何 `appsettings` 规则），里面是**本地示例实例**的连接串。
> 换成生产实例时请改走 `appsettings.Development.local.json` 之类的本地覆盖文件，
> 或环境变量 —— 不要把自己的生产凭据写进这个被跟踪的文件。
> 本手册与所有样例代码、接口响应都**不回显任何连接串或密码**。

> ⚠️ **缺配置【不会 500，只会静默不命中】。** 连接名两级查找全部落空时，
> `RedisConnectionProvider` 抛的 `InvalidOperationException` 会被读路径
> （`CacheableHandler.TryReadCache`）与写路径（`CacheOperationHelper.WriteCache`）
> 的 `catch (Exception)` 吞掉 ⇒ 表现是「**缓存永远不命中**：`elapsedMs` 一直是方法体耗时」，
> **不是** HTTP 500。判定连接通没通只看 `elapsedMs`，看状态码会得出错误结论。
> 唯一会 500 的是「注解里 `cacheManager` 写的类型没注册」—— 那条 `InvalidOperationException`
> 会穿透 `CacheOperationDispatcher` 的降级过滤器（见第十章 10.5）。

> ⚠️ **Redis 配置的可见性取决于环境**：扁平连接串键写在 `appsettings.Development.json` 里，
> 而 ASP.NET Core 按 `DOTNET_ENVIRONMENT` / `ASPNETCORE_ENVIRONMENT` 决定是否加载该文件。
> （`appsettings.json` 里的 `spring:data:redis` 节**任何环境都读得到**，后者才是推荐形态。）
>
> | 启动方式 | 环境 | 扁平键 `redis:main-site` | Redis 端点表现 |
> |---|---|---|---|
> | `dotnet run`（读 `Properties/launchSettings.json`） | Development | 读到 | 正常连接 Redis |
> | `dotnet bin/Debug/net8.0/….dll`（无环境变量） | Production | **读不到** | **不报错**：第八章的 `redis/*` 端点每次都是 ~1500ms（缓存静默失效），HTTP 仍是 200 |
>
> **内存注解不受环境影响**：只有显式写了 `cacheManager: typeof(RedisCacheManager)`
> （如第八章 8.1）的方法才会走到 Redis 解析；没写 `cacheManager` 的方法一律走
> 框架自带的 `MemoryCacheManager`，与有没有 Redis 配置完全无关。

### 服务注册方式

本示例用 `[IocService]` 特性 + `AddBatchServices()` 批量扫描注册服务，而非逐个手写 `AddTransient<T>()`：

```csharp
builder.Services.AddBatchServices();               // 扫描 [IocService] 并注册
// …第十一章还需要 AddFeignStarter(...) 注册 [FeignClient] 接口…
builder.Services.AddEnableCaching();   // ← 必须最后调用
builder.Services.AddControllers();
```

> ⚠️ **`AddEnableCaching` 必须最后调用。** 它在调用时会扫描此刻容器里**已有**的服务
> （包括 `AddFeignStarter` 注册的 OpenFeign 接口）来叠加缓存代理，
> 晚于它注册的接口不会被包住，且是**静默**失效。详见 [第十一章](#10-第十一章openfeign--cacheable远程调用结果缓存)。

> ⚠️ 缓存注解依赖 Castle DynamicProxy，因此注解落在**类方法**上时该方法必须是 `virtual`（本示例除第十一章外全部满足）；
> 也可把注解**直接标在接口方法上**（接口须已注册进 DI），此时无需实现类、无需 `virtual` —— 第十一章即此写法。

---

## 1. 第一章：基础 Cacheable 用法

`[Cacheable(cacheName, key, ttlSeconds)]` —— 命中缓存则直接返回，未命中则执行方法体并写入缓存。方法体里有 `Thread.Sleep(1500)` 模拟数据库查询，因此**首次调用约 1500ms，命中后约 0ms**，这是本手册所有用例的核心判定依据。

| # | 端点 | 缓存键 | TTL | 期望 |
|---|------|--------|-----|------|
| 1.1 | `GET /api/samples/basic/1` | `users:1` | 60s | 第 1 次 ~1500ms，第 2 次 ~0ms |
| 1.2 | `GET /api/samples/basic/short-ttl/1` | `users:1` | 10s | 10s 内命中；10s 后重新执行（~1500ms） |
| 1.3 | `GET /api/samples/basic/by-name?name=Alice` | `users:name:Alice` | 60s | 字符串键，第 2 次命中 |
| 1.4 | `GET /api/samples/basic/age-range?minAge=18&maxAge=30` | `users:age-range:18:30` | 60s | 组合键，不同参数组合各自独立缓存 |
| 1.5 | `GET /api/samples/basic/legacy/1` | `users:legacy:1` | 60s | `ttlSeconds` 写法，第 2 次命中 |
| 1.6 | `GET /api/samples/basic/legacy-sliding/1` | `users:legacy-sliding:1` | 86400s + 滑动 300s | `slidingTtl` 写法，第 2 次命中 |

```bash
# 1.1 基础缓存：连续请求两次，对比 elapsedMs
curl -s "http://localhost:16605/api/samples/basic/1" | jq '{elapsedMs, callCount, cacheKey}'
curl -s "http://localhost:16605/api/samples/basic/1" | jq '{elapsedMs, callCount, cacheKey}'

# 1.2 短 TTL：10 秒后再请求，应重新执行方法体
curl -s "http://localhost:16605/api/samples/basic/short-ttl/1" | jq '.elapsedMs'
sleep 11
curl -s "http://localhost:16605/api/samples/basic/short-ttl/1" | jq '.elapsedMs'

# 1.3 字符串键
curl -s "http://localhost:16605/api/samples/basic/by-name?name=Alice" | jq

# 1.4 组合键
curl -s "http://localhost:16605/api/samples/basic/age-range?minAge=18&maxAge=30" | jq
curl -s "http://localhost:16605/api/samples/basic/age-range?minAge=20&maxAge=40" | jq  # 另一次真实调用

# 1.5 / 1.6 简写属性
curl -s "http://localhost:16605/api/samples/basic/legacy/1" | jq
curl -s "http://localhost:16605/api/samples/basic/legacy-sliding/1" | jq
```

> **关于响应中的 `callCount`：** 本示例的服务均为
> `[IocService(lifetime: ServiceLifetime.Transient)]`，每个请求拿到的是**新实例**，
> 因此 `callCount` 表示「本次请求是否真正执行了方法体」——未命中时方法体执行一次得到 `1`，
> 命中时方法体被拦截器跳过、计数保持 `0`。它是**单次请求的指示器，不是跨请求的累计计数器**。
> 这与 `elapsedMs` 的结论一致（两者互相印证），但不要指望它在多次请求间递增。

---

## 2. 第二章：CachePut & CacheEvict

| 注解 | 行为 |
|------|------|
| `[Cacheable]` | 命中则**不执行**方法体，直接返回缓存 |
| `[CachePut]` | **总是执行**方法体，并把结果写入缓存 |
| `[CacheEvict]` | 执行方法体后删除指定缓存条目 |

| # | 端点 | 演示内容 | 期望 |
|---|------|---------|------|
| 2.1 | `GET /api/samples/lifecycle/{id}` | `Cacheable` 查询 | 第 1 次 ~1500ms，第 2 次 ~0ms |
| 2.2 | `POST /api/samples/lifecycle/update` | `CachePut` 更新（~500ms） | **每次都执行**，两次 `elapsedMs` 都非 0 |
| 2.3 | `POST /api/samples/lifecycle/delete` | `CacheEvict` 删除（~300ms） | 删除后再次查询 2.1 会重新执行 |
| 2.4 | `GET /api/samples/lifecycle/refresh/{id}` | `CachePut` 刷新（~1500ms） | 每次都执行，并覆盖缓存 |

```bash
# 2.1 先查询并缓存
curl -s "http://localhost:16605/api/samples/lifecycle/1" | jq '{elapsedMs, callCount}'
curl -s "http://localhost:16605/api/samples/lifecycle/1" | jq '{elapsedMs, callCount}'   # 命中 ~0ms

# 2.2 CachePut：连续两次，两次都 ~500ms（始终执行方法体）
curl -s -X POST "http://localhost:16605/api/samples/lifecycle/update" \
  -d "id=1&name=Bob&age=30&email=bob@test.com" | jq '{elapsedMs, operation}'
curl -s -X POST "http://localhost:16605/api/samples/lifecycle/update" \
  -d "id=1&name=Bob&age=30&email=bob@test.com" | jq '{elapsedMs, operation}'

# 2.3 CacheEvict：删除后 2.1 应重新执行（~1500ms）
curl -s -X POST "http://localhost:16605/api/samples/lifecycle/delete" -d "id=1" | jq
curl -s "http://localhost:16605/api/samples/lifecycle/1" | jq '{elapsedMs, callCount}'

# 2.4 CachePut 刷新
curl -s "http://localhost:16605/api/samples/lifecycle/refresh/1" | jq '{elapsedMs, callCount}'
```

**关键断言：** 2.2 与 2.4 的 `elapsedMs` **两次都非 0** —— 这正是 `CachePut` 与 `Cacheable` 的核心差异。

> 2.2 / 2.3 也可以直接在 <http://localhost:16605/dashboard> 面板执行：`lifecycle/update` 行下会自动列出
> `id`/`name`/`age`/`email` 四个预填的 Form 输入框（见文首「在仪表盘上测试 POST 端点」）。

---

## 3. 第三章：SpEL 键表达式

| # | 端点 | SpEL 键表达式 | 说明 |
|---|------|--------------|------|
| 3.1 | `GET /api/samples/spel/query?userId=1&keyword=abc` | `#qo.UserId:#qo.Keyword` | 对象属性参与拼键 |
| 3.2 | `GET /api/samples/spel/config` | `#cfg.site_name:#cfg.version` | `Dictionary` 索引式访问 |
| 3.3 | `GET /api/samples/spel/positional/1` | `#p0` | 位置参数 `#p0` / `#p1` |
| 3.4 | `GET /api/samples/spel/default-name/1` | `#id`（无 `cacheName`） | 未指定 `cacheName` 时走默认区域 |
| 3.5 | `GET /api/samples/spel/benefit/1/Normal` | `#userId:#level` | **枚举参数**按枚举值区分缓存 |
| 3.6 | `GET /api/samples/spel/sliding-redis/1` | `#id` + `slidingTtl` + `RedisCacheManager` | SpEL + 滑动 + Redis 组合 |

> 编号规则：一节的编号与**端点**一一对应（`SpelKeyService.SearchUsers` 的复合键示例只有源码、没有端点，故不占编号）。

```bash
# 3.1 对象属性拼键：不同 keyword 命中不同缓存
curl -s "http://localhost:16605/api/samples/spel/query?userId=1&keyword=abc" | jq '{cacheKey, elapsedMs}'
curl -s "http://localhost:16605/api/samples/spel/query?userId=1&keyword=abc" | jq '{cacheKey, elapsedMs}'
curl -s "http://localhost:16605/api/samples/spel/query?userId=1&keyword=xyz" | jq '{cacheKey, elapsedMs}'

# 3.2 Dictionary 键
curl -s "http://localhost:16605/api/samples/spel/config" | jq

# 3.3 位置参数
curl -s "http://localhost:16605/api/samples/spel/positional/1" | jq '{cacheKey, elapsedMs}'
curl -s "http://localhost:16605/api/samples/spel/positional/1" | jq '{cacheKey, elapsedMs}'

# 3.4 默认 cacheName
curl -s "http://localhost:16605/api/samples/spel/default-name/1" | jq

# 3.5 枚举参数参与拼键（详见下节）
curl -s "http://localhost:16605/api/samples/spel/benefit/1/Normal" | jq '{cacheKey, elapsedMs}'

# 3.6 SpEL + 滑动 + Redis（需启用 Redis）
curl -s "http://localhost:16605/api/samples/spel/sliding-redis/1" | jq
```

### 3.5 枚举参数：能否按枚举值命中缓存？

**结论：可以。** 缓存键由 `SpelKeyParser.ResolveValue` 解析，简单键走
`val?.ToString() ?? ""`，枚举的 `ToString()` 返回**成员名**：

| 调用 | 缓存键 |
|------|--------|
| `GetUserBenefit(1, UserLevel.Normal)` | `spel:benefit:1:Normal` |
| `GetUserBenefit(1, UserLevel.Gold)` | `spel:benefit:1:Gold` |

两个枚举成员的键不同 → 各自独立缓存，互不命中；同一枚举值重复调用 → 命中。

```bash
# Normal：首次未命中（~1500ms），二次命中（~0ms）
curl -s "http://localhost:16605/api/samples/spel/benefit/1/Normal" | jq '{cacheKey, elapsedMs, callCount}'
curl -s "http://localhost:16605/api/samples/spel/benefit/1/Normal" | jq '{cacheKey, elapsedMs, callCount}'
# 同 userId 换枚举值 → 不同键 → 重新执行
curl -s "http://localhost:16605/api/samples/spel/benefit/1/Gold" | jq '{cacheKey, elapsedMs, callCount}'
# 回到 Normal → 仍命中，说明各枚举值的缓存互不干扰
curl -s "http://localhost:16605/api/samples/spel/benefit/1/Normal" | jq '{cacheKey, elapsedMs, callCount}'
# 同枚举值换 userId → 也是不同键 → 重新执行
curl -s "http://localhost:16605/api/samples/spel/benefit/2/Gold" | jq '{cacheKey, elapsedMs, callCount}'
```

> ⚠️ 解析用的是 `ToString()`。若给枚举成员加了 `[EnumMember]` / `[JsonStringEnumMemberName]`
> 或重写了 `ToString`，键里存的是显示名而非成员名 —— 结论不变，只要
> 「不同枚举值 → 不同 ToString 结果」仍成立。
>
> 另外，枚举成员名若含 `:`，会与键的分隔符混淆（`ToString` 结果原样拼接），
> 命名时需避开。

---

## 4. 第四章：Condition & Unless

`Condition` 决定**是否使用/写入缓存**，`Unless` 决定**是否排除本次结果**（`#result` 仅在 `Unless` 中可用）。

| # | 端点 | 表达式 | 期望 |
|---|------|--------|------|
| 4.1 | `GET /api/samples/condition/cacheable/{id}` | `Condition = "#id > 10"` | `id=99` → 第 2 次命中；`id=5` → 每次都 ~1500ms |
| 4.2 | `GET /api/samples/condition/unless/{id}` | `Unless = "#result == null"` | `id=1` 命中；`id=999`（null）不写入缓存 |
| 4.3 | `GET /api/samples/condition/combined/{id}` | `Condition = "#id > 0"` + `Unless = "#result == null \|\| #result.Age > 40"` | 年龄 ≤ 40 才缓存 |
| 4.4 | `POST /api/samples/condition/put` | `CachePut` + `Condition = "#result != null"` + `Unless = "#result.Name == 'skip'"` | `name=skip` 时不写缓存 |
| 4.5 | `POST /api/samples/condition/evict` | `CacheEvict` + `Condition = "#id > 0"` | `id<=0` 时跳过驱逐 |
| 4.6 | `GET /api/samples/condition/complex/{id}` | `Condition = "#p0 > 0 && #p0 < 100"` + `Unless = "#result.Email == 'skip@test.com'"` | 组合运算符 `&&` / `==` |
| 4.7 | `GET /api/samples/condition/async-unless/{id}` | 异步方法 + `Unless = "#result == null"` | 异步方法同样支持条件缓存 |

```bash
# 4.1 Condition 命中（注意：种子数据只有 id=1/2/5/99，用 99 而非不存在的 20）
curl -s "http://localhost:16605/api/samples/condition/cacheable/99" | jq '{elapsedMs, callCount}'
curl -s "http://localhost:16605/api/samples/condition/cacheable/99" | jq '{elapsedMs, callCount}'  # ~0ms

# 4.1 Condition 不命中：id=5 ≤ 10 → 每次都 ~1500ms
curl -s "http://localhost:16605/api/samples/condition/cacheable/5" | jq '{elapsedMs, callCount}'
curl -s "http://localhost:16605/api/samples/condition/cacheable/5" | jq '{elapsedMs, callCount}'

# 4.2 Unless：null 结果不缓存
curl -s "http://localhost:16605/api/samples/condition/unless/1" | jq '{elapsedMs, callCount}'
curl -s "http://localhost:16605/api/samples/condition/unless/999" | jq '{elapsedMs, callCount}'
curl -s "http://localhost:16605/api/samples/condition/unless/999" | jq '{elapsedMs, callCount}'  # 仍 ~1500ms

# 4.3 组合条件
curl -s "http://localhost:16605/api/samples/condition/combined/1" | jq '{elapsedMs, callCount}'

# 4.4 CachePut + Unless
curl -s -X POST "http://localhost:16605/api/samples/condition/put" -d "id=1&name=Alice" | jq
curl -s -X POST "http://localhost:16605/api/samples/condition/put" -d "id=1&name=skip"  | jq

# 4.5 CacheEvict + Condition
curl -s -X POST "http://localhost:16605/api/samples/condition/evict" -d "id=1"  | jq
curl -s -X POST "http://localhost:16605/api/samples/condition/evict" -d "id=-1" | jq

# 4.6 复杂条件
curl -s "http://localhost:16605/api/samples/condition/complex/1" | jq '{elapsedMs, callCount}'

# 4.7 异步 + Unless
curl -s "http://localhost:16605/api/samples/condition/async-unless/1" | jq '{elapsedMs, callCount}'
curl -s "http://localhost:16605/api/samples/condition/async-unless/1" | jq '{elapsedMs, callCount}'  # ~0ms
```

### 4.8 拼写错误容错（重点）

SpEL 中引用了**不存在的变量**时，框架不会抛异常，而是记录 `WRN` 日志并把该变量解析为 `null`，随后按表达式的自然语义降级：

| # | 端点 | 错误表达式 | 解析结果 | 行为 |
|---|------|-----------|---------|------|
| 4.8 | `GET /api/samples/condition/typo-condition/{id}` | `Condition = "#res111ult != null"` | `null != null` → `false` | condition=false → **永不缓存**，每次都 ~1500ms |
| 4.9 | `GET /api/samples/condition/typo-unless/{id}` | `Unless = "#res111ult == null"` | `null == null` → `true` | unless=true → **永不写缓存**，每次都 ~1500ms |
| 4.10 | `GET /api/samples/condition/typo-chain/{id}` | `Condition = "#usre.Age > 0"` | 根变量 `#usre` 为 null → `null > 0` → `false` | condition=false → **永不缓存** |

```bash
# 4.8 Condition 拼写错误：连续两次都应 ~1500ms
curl -s "http://localhost:16605/api/samples/condition/typo-condition/1" | jq '{elapsedMs, callCount, note}'
curl -s "http://localhost:16605/api/samples/condition/typo-condition/1" | jq '{elapsedMs, callCount}'

# 4.9 Unless 拼写错误
curl -s "http://localhost:16605/api/samples/condition/typo-unless/1" | jq '{elapsedMs, callCount, note}'
curl -s "http://localhost:16605/api/samples/condition/typo-unless/1" | jq '{elapsedMs, callCount}'

# 4.10 属性链根变量拼写错误
curl -s "http://localhost:16605/api/samples/condition/typo-chain/1" | jq '{elapsedMs, callCount}'
curl -s "http://localhost:16605/api/samples/condition/typo-chain/1" | jq '{elapsedMs, callCount}'
```

**控制台应出现（`WRN` 级别）：**

```text
WRN SpEL 条件表达式引用了不存在的变量 '#res111ult'，解析为 null
WRN SpEL 条件表达式引用了不存在的变量 '#usre'（属性链 '#usre.Age'），解析为 null
```

### 4.11 诊断计数器

| 端点 | 统计对象 |
|------|---------|
| `GET /api/samples/condition/call-count` | `GetUserWithCondition` 执行计数 |
| `GET /api/samples/condition/unless-count` | `GetUserWithUnless` 执行计数 |
| `GET /api/samples/condition/combined-count` | `GetUserCombined` 执行计数 |
| `GET /api/samples/condition/async-unless-count` | `GetUserAsyncUnless` 执行计数 |

```bash
curl -s "http://localhost:16605/api/samples/condition/call-count" | jq
```

> ⚠️ 这些端点读的是 `ConditionalService` 实例字段，而该服务是 `Transient` 生命周期
> ——诊断请求与业务请求**不是同一个实例**，因此 `executedTimes` 恒为 `0`，**不能用来判定缓存命中**。
> 请改用 §4.1–§4.7 各端点的 `elapsedMs` 字段。

---

## 5. 第五章：CacheConfig 继承

类级 `[CacheConfig]` 提供默认 `cacheName` / `ttlSeconds` / `cacheManager`，方法级注解可逐项覆盖：

```csharp
[CacheConfig(defaultCacheName: "config-demo", defaultTtlSeconds: 120)]
public class ConfigInheritanceService
{
    [Cacheable(key: "#id")]                                            // 全部继承
    [Cacheable(cacheName: "custom-name", key: "#id")]                  // 覆盖 cacheName
    [Cacheable(key: "#id", ttlSeconds: 30)]                            // 覆盖 ttlSeconds
    [Cacheable(cacheName: "fully-custom", key: "#id", ttlSeconds: 600, slidingTtl: 60)]  // 全部覆盖
}
```

| # | 端点 | 生效配置 | 期望 |
|---|------|---------|------|
| 5.1 | `GET /api/samples/config/default/{id}` | `config-demo` / 120s | 继承类级配置，第 2 次命中 |
| 5.2 | `GET /api/samples/config/custom-name/{id}` | `custom-name` / 120s | `cacheName` 被覆盖 |
| 5.3 | `GET /api/samples/config/custom-ttl/{id}` | `config-demo` / 30s | `ttlSeconds` 被覆盖 |
| 5.4 | `GET /api/samples/config/fully-custom/{id}` | `fully-custom` / 600s + 滑动 60s | 全部覆盖 |

```bash
for p in default custom-name custom-ttl fully-custom; do
  echo "--- config/$p ---"
  curl -s "http://localhost:16605/api/samples/config/$p/1" | jq '{cacheKey, ttl, elapsedMs}'
  curl -s "http://localhost:16605/api/samples/config/$p/1" | jq '{elapsedMs}'
done
```

---

## 6. 第六章：异步缓存

异步方法（`Task<T>` / `ValueTask<T>`）与同步方法用法完全一致，`Condition` / `Unless` / `slidingTtl` 全部适用。
非泛型 `Task` / `ValueTask`（无返回值）没有可缓存的 `T`，每次都会真实执行。

| # | 端点 | 注解 | 期望 |
|---|------|------|------|
| 6.1 | `GET /api/samples/async/{id}` | `Cacheable(cacheName:"async:users", key:"#id", ttlSeconds:60)` | 第 1 次 ~1500ms，第 2 次 ~0ms。**id 存在与否都会命中**（见下方 ⚠️） |
| 6.2 | `POST /api/samples/async/update` | `CachePut(key:"#user.Id", ttlSeconds:60)` | 每次执行（~500ms），并刷新 6.1 的缓存 |
| 6.3 | `GET /api/samples/async/all` | `Cacheable(key:"'all'", ttlSeconds:30)` | 常量键（~2000ms → ~0ms） |
| 6.4 | `GET /api/samples/async/hot/{id}` | `Cacheable(..., unless = "#result == null")` | **6.1 的对照组**：不存在的 id 每次都 ~1500ms（null 不写缓存） |
| — | `GET /api/samples/async/call-count` | — | 异步方法执行计数（Transient 下恒为 0，见 §1） |

> ⚠️ **v0.2.0 起框架默认缓存 `null`（对齐 Spring/Java），语义与 0.1.x 相反。**
> `GetUserAsync` 返回 `null`（id 不在种子数据 `100/200/300` 里）时，这个 `null`
> **同样会被写进缓存**，第二次调用是**命中**（`elapsedMs ≈ 0`，`callCount` 不增）。
> 要退回「null 不写缓存」的旧行为，用**逐方法** opt-out —— 即 6.4 的
> `unless = "#result == null"`；这就是 `async/hot/{id}` 存在的意义。
> 本样板早期的测试手册与 `test.sh` 用的是 `async/1` 并断言「两次都是 ~1500ms」，
> 那是改造前的口径，已随本次语义反转一并修正。

```bash
# 6.1 异步 Cacheable（id=100 是 AsyncAlice，存在 → 第二次命中）
curl -s "http://localhost:16605/api/samples/async/100" | jq '{elapsedMs, callCount}'
curl -s "http://localhost:16605/api/samples/async/100" | jq '{elapsedMs, callCount}'   # ~0ms

# 6.1 的 null 半场：id=777 不在种子数据里 → 返回 null → null 也写缓存 → 第二次同样命中
curl -s "http://localhost:16605/api/samples/async/777" | jq '{data, elapsedMs}'       # ~1500ms
curl -s "http://localhost:16605/api/samples/async/777" | jq '{data, elapsedMs}'       # ~0ms（命中）

# 6.4 对照组：同一个不存在的 id，因 unless = "#result == null" 每次真实执行
curl -s "http://localhost:16605/api/samples/async/hot/1" | jq '{data, elapsedMs}'     # ~1500ms
curl -s "http://localhost:16605/api/samples/async/hot/1" | jq '{data, elapsedMs}'     # 仍 ~1500ms

# 6.2 异步 CachePut：始终执行（~500ms），并把结果写回 async:users:1
curl -s -X POST "http://localhost:16605/api/samples/async/update" -d "id=1&name=AsyncBob&age=28" | jq
curl -s -X POST "http://localhost:16605/api/samples/async/update" -d "id=1&name=AsyncBob&age=28" | jq
# 6.2 刚把 id=1 写进缓存，所以下面这次命中（~0ms）；这是「CachePut 用实体覆盖空结果」的直接证据
curl -s "http://localhost:16605/api/samples/async/1" | jq '{data, elapsedMs}'

# 6.3 常量键缓存集合
curl -s "http://localhost:16605/api/samples/async/all" | jq '{elapsedMs, callCount}'
curl -s "http://localhost:16605/api/samples/async/all" | jq '{elapsedMs, callCount}'  # ~0ms

# 6.4 诊断计数
curl -s "http://localhost:16605/api/samples/async/call-count" | jq
```

> 6.2 的键 `key:"#user.Id"` 是**属性链表达式**，解析出的结果干净地参与拼接，生成的键为 `async:users:1`（不含 `\0` 填充），
> 因此写回后 6.1 的下一次读能真正命中。`cacheName` 与 `key` 之间框架只补一个 `:`，
> 所以 `cacheName:"async:users"` 自身含 `:` 也不会生成 `async:users1`。
>
> 6.3 的键是 SpEL 字符串常量 `'all'` —— 注意单引号，否则会被当作变量名。
>
> `AsyncCacheService.DeleteUserAsync`（`CacheEvict`，~300ms）在服务层已实现，但**未暴露 HTTP 端点**，因此无对应 curl 用例；其行为可参考 2.3 的同步版本。

### 6.5 并发防击穿验证（异步方法）

异步方法同样有防击穿：同一未命中 key 的并发调用只执行一次，其余调用复用 leader 的结果。
用一个临时进程内计数不好断言，这里用「耗时」间接验证 —— 16 个并发请求同一未命中的 key，
若击穿防护失效，`GetUserAsync` 会被执行 16 次（各 ~1500ms）；生效则只执行 1 次。

```bash
# 先让缓存过期 / 换一个未被访问过的 id，再并发打
for i in $(seq 1 16); do curl -s "http://localhost:16605/api/samples/async/7" & done; wait
curl -s "http://localhost:16605/api/samples/async/call-count" | jq
```

服务台日志里 `[异步Cacheable] 开始执行异步查询：id=7` 应**只出现一次**（16 个并发请求共享同一次执行）。

> 该防护只对同一实例内的并发调用生效（登记表在进程内）；
> 多实例部署时各实例各自登记一次，跨实例的去重需要靠 Redis 分布式锁，不在本包范围内。

---

## 7. 第七章：滑动过期

| # | 端点 | 配置 | 期望 |
|---|------|------|------|
| 7.1 | `GET /api/samples/sliding/fixed/{id}` | `ttlSeconds: 10`（**固定 TTL 对照**） | 10 秒后必定过期，无论期间是否访问 |
| 7.2 | `GET /api/samples/sliding/basic/{id}` | `ttlSeconds: 86400` + `slidingTtl: 30` | 每次访问续期 30 秒，持续访问则一直命中 |

**对照实验（核心）：**

```bash
# 7.1 固定 TTL：第 3 秒访问一次，第 11 秒仍会过期（因为固定 TTL 不续期）
curl -s "http://localhost:16605/api/samples/sliding/fixed/1" | jq '.elapsedMs'   # ~1500
sleep 3
curl -s "http://localhost:16605/api/samples/sliding/fixed/1" | jq '.elapsedMs'   # ~0（命中）
sleep 8
curl -s "http://localhost:16605/api/samples/sliding/fixed/1" | jq '.elapsedMs'   # ~1500（已过期）

# 7.2 滑动过期：每 3 秒访问一次，超过 30 秒仍保持命中
for i in $(seq 1 12); do
  printf "第 %2d 次(第 %2d 秒): " "$i" "$((i*3))"
  curl -s "http://localhost:16605/api/samples/sliding/basic/1" | jq -c '.elapsedMs'
  sleep 3
done
```

**关键断言：** 7.2 全程 `elapsedMs ≈ 0`（滑动续期生效）；7.1 在最后一次（第 11 秒）出现 ~1500ms。

---

## 8. 第八章：Redis 缓存

> 需先在 `appsettings.Development.json` 配置 `redis:main-site` 连接字符串并重启。
> **缺配置时本章不会 500，而是全部静默不命中**（每次 ~1500ms，见 §0「关于 Redis」）。
> **8.5 额外需要 `redis:cas-auth`**（第二个 Redis 后端）；只缺它时 8.1~8.4 完全正常，8.5 的缓存静默失效。
> **8.6 复用 `redis:main-site`**，但通过 `DatabaseIndex` 把库索引改成 1 / 2 —— **不需要任何新配置键**。

| # | 端点 | 配置 | 说明 |
|---|------|------|------|
| 8.1 | `GET /api/samples/redis/{id}` | `RedisCacheManager`, TTL 300s | 数据落在 Redis，多实例共享 |
| 8.2 | `GET /api/samples/redis/sliding/{id}` | Redis + `slidingTtl: 300` + 绝对上限 86400s | Redis 滑动过期 |
| 8.3 | `POST /api/samples/redis/update` | `CachePut` + `RedisCacheManager` | 更新 DB 同时刷新 Redis |
| 8.4 | `GET /api/samples/redis/memory/{id}` | `MemoryCacheManager` | 与 8.1 对比：进程内缓存，多实例不共享 |
| 8.5 | `GET /api/samples/redis/multi/{id}` | `RedisCacheManager` + `CasAuthRedisManager` | **一个项目两个 Redis 后端**（db 11 + db 12，两套序列化器） |
| 8.6 | `GET /api/samples/redis/default-backend/{id}` | 类级 `[CacheConfig]` → `Db1RedisManager` + 方法级覆盖 | **Service 级默认后端**（自定义 db 1），方法 B 覆盖为 db 2、方法 C 覆盖为内存 |

```bash
# 8.1 Redis 读写（命中时仍有一次 Redis 往返，~100ms 属正常，非 0ms）
curl -s "http://localhost:16605/api/samples/redis/1" | jq '{cacheManager, cacheKey, elapsedMs}'
curl -s "http://localhost:16605/api/samples/redis/1" | jq '{elapsedMs}'   # 命中，约 50–150ms

# 8.4 对照：Memory 提供器（命中为 0ms 量级）
curl -s "http://localhost:16605/api/samples/redis/memory/1" | jq '{cacheManager, elapsedMs}'
curl -s "http://localhost:16605/api/samples/redis/memory/1" | jq '{elapsedMs}'

# 8.3 更新
curl -s -X POST "http://localhost:16605/api/samples/redis/update" -d "id=1&name=RedisBob&age=33" | jq
```

用 `redis-cli` 直接验证键是否落库：

```bash
redis-cli -h <host> -p <port> --scan --pattern "redis:users:*"
```

> 键格式为 `{cacheName}:{key}`，且 `cacheName` 与 `key` 之间**只保留一个 `:`** —— `cacheName` 自身含 `:`（`redis:users`）不会退化成 `redis:users1`。

**关键断言：** 8.1 与 8.4 使用**不同的缓存区域**（`redis:users` vs `memory:users`），因此各自的首次调用都会真实执行（~1500ms），第二次才命中。

---

### 8.5 一个项目里同时使用两个 Redis 后端

服务层的方法**只有 `cacheManager` 一个参数不同**（`Services/MultiRedisCacheService.cs`）：

| | 方法 | `cacheManager` | 配置键 / 库 | 序列化器 | 缓存键 |
|---|---|---|---|---|---|
| A | `GetUserFromMainSite` | `typeof(RedisCacheManager)` | `redis:main-site` / db 11 | Newtonsoft（默认） | `multi:main:{id}` |
| B | `GetUserFromCasAuth` | `typeof(CasAuthRedisManager)` | `redis:cas-auth` / db 12 | System.Text.Json | `multi:cas:{id}` |
| C | `GetSampleFromMainSite` | `typeof(RedisCacheManager)` | `redis:main-site` / db 11 | Newtonsoft | `multi:sample:{id}` |
| D | `GetSampleFromCasAuth` | `typeof(CasAuthRedisManager)` | `redis:cas-auth` / db 12 | System.Text.Json | `multi:cas-sample:{id}` |

C/D 与 A/B 的区别只在于返回类型：`MultiBackendSampleDto`（含 `double`）而不是 `UserDto`。
`UserDto` 里全是 string / int / DateTime / null，**两个引擎的输出恰好相同**，
拿它做序列化对照会看不出任何差异 —— 所以用 C/D 这组来看字节。

`CasAuthRedisManager` 就是**全部的自定义代码** —— 继承 `RedisCacheManager` 后重写两个 `protected virtual` 属性：

```csharp
// src/Yzl.Extensions.Samples.Cache/Managers/CasAuthRedisManager.cs
public class CasAuthRedisManager(IRedisConnectionFactory connectionFactory)
    : RedisCacheManager(connectionFactory)
{
    protected override string ConnectionStringKey => "redis:cas-auth";
    protected override IRedisCacheSerializer? Serializer => new SystemTextJsonRedisCacheSerializer();
}
```

框架源码里**没有任何一处**提到 `CasAuthRedisManager`，`Program.cs` 也不需要注册 ——
**继承即注册**：`AddEnableCaching(assemblies: ...)` 会扫描范围内的所有具体 `ICacheManager` 实现并自动注册为单例，
`sealed` 子类也一样（`[IocService]`/`AddBatchServices()` 都不参与）。
对照 Java，等价于 `RedisConfig.java` 里声明两个 `@Bean` 模板、注解上用 `@Qualifier` 选名字。

```bash
# 第一次：两个后端都是 miss，各 ~1500ms
curl -s "http://localhost:16605/api/samples/redis/multi/1" \
  | jq '{callCount, main: .backends[0].elapsedMs, cas: .backends[1].elapsedMs}'
# → {"callCount":2,"main":1504,"cas":1502}

# 第二次：两个后端都命中，个位数毫秒
curl -s "http://localhost:16605/api/samples/redis/multi/1" \
  | jq '{callCount, main: .backends[0].elapsedMs, cas: .backends[1].elapsedMs}'
# → {"callCount":2,"main":2,"cas":1}      ← callCount 不再增长 = 两个后端都命中了
```

用 `redis-cli` 到**两个库**分别核对（`-h` / `-p` 按 `appsettings.Development.json` 的连接串）：

```bash
redis-cli -h <host> -p <port> -n 11 --scan --pattern "multi:*"
redis-cli -h <host> -p <port> -n 12 --scan --pattern "multi:*"
```

**关键断言（三条）：**

1. **两个库互不可见**：`multi:main:1` / `multi:sample:1` 只出现在 db 11，`multi:cas:1` / `multi:cas-sample:1` 只出现在 db 12。`redis-cli -n 11 KEYS 'multi:cas*'` 应为空。
2. **换管理器 = 换后端**：这一点靠「只改一个 `cacheManager` 参数就落到另一个库」证明。管理器是「选后端」的唯一落点。
3. **序列化器真的生效了** —— 用 `multi:sample` 这组复杂类型样本直接看字节：

```bash
redis-cli -h <host> -p <port> -n 11 GET "multi:sample:1"
# → {"Id":1,"Name":"ScoreSample","Score":1.0}          ← Newtonsoft 保留小数位

redis-cli -h <host> -p <port> -n 12 GET "multi:cas-sample:1"
# → {"Id":1,"Name":"ScoreSample","Score":1}            ← System.Text.Json 最简往返
```

> ⚠️ **别拿 `UserDto`（8.5-A/B）去比文本差异。** 两个引擎对 `string` / `int` / `DateTime` / `null`
> 的默认输出**恰好相同**（含 ISO-8601 格式），看上去会得出「两个后端一样」的错觉。
> 差异只出现在**浮点**这类地方 —— 所以服务里专门加了 `MultiBackendSampleDto` 这组样本。
> 这条事实由 `RedisCacheSerializerTests.BuiltInSerializers_ProduceDifferentText_ForFloatingPoint` 钉住。

> ⚠️ **同一个 `cacheName` 上不要换序列化器。** 两种引擎对同一对象产生的字节不同，错配的后果是反序列化异常被 `CacheableHandler.TryReadCache` 吞成「未命中」——表现为「缓存永远不命中」，**不是报错**。本样例给两个后端用了不同的 `cacheName`（`multi:main` / `multi:cas`、`multi:sample` / `multi:cas-sample`），所以互不干扰。

> ⚠️ **只缺 `redis:cas-auth` 时不会 500。** 8.5-B/-D 那两路的异常会被读/写两条缓存路径吞掉（与 8.1 打错键的后果同类），表现为 `cas.elapsedMs` 一直是 ~1500 —— 这**不是**「缓存被禁用」，而是「配置没生效」，请检查键名。

> ⚠️ **标量绕过序列化器。** `string` / `int` / `long` / `double` / `bool` / `byte[]` 与 `null`
> 由 `RedisCacheProvider` 直接写裸值，不经过序列化器 —— 所以「换序列化器不影响标量键」，
> 这也是 Java `StringRedisSerializer` 的语义。要做序列化对照，**必须用复杂类型**。

---

### 8.6 Service 级默认后端（自定义 db 1）+ 方法级覆盖

8.1~8.5 都是**每个方法各自写 `cacheManager`**。本节把「选后端」提到**类**上：
类级 `[CacheConfig]` 指定一个自定义的 db 1 管理器，方法上不写 `cacheManager` 就继承它。

服务在 `Services/DefaultBackendCacheService.cs`，三个方法**故意只差「写没写 `cacheManager`」**：

```csharp
[CacheConfig(defaultCacheName: "svc:default", cacheManager: typeof(Db1RedisManager))]
[IocService(lifetime: ServiceLifetime.Transient)]
public class DefaultBackendCacheService
{
    [Cacheable(key: "#id")]                                              // A：不写 cacheManager
    public virtual UserDto? GetDefaultBackend(int id) { ... }

    [Cacheable(key: "#id", cacheManager: typeof(Db2RedisManager))]        // B：覆盖为 db 2
    public virtual UserDto? GetFromDb2(int id) { ... }

    [Cacheable(cacheName: "svc:memory", key: "#id",
               cacheManager: typeof(MemoryCacheManager))]                 // C：覆盖为内存
    public virtual UserDto? GetFromMemory(int id) { ... }
}
```

| | 方法 | `cacheManager` 参数 | 实际后端 | `cacheName` | 缓存键 |
|---|---|---|---|---|---|
| A | `GetDefaultBackend` | **不写** → 继承类级值 | `redis:main-site` / **db 1** | `svc:default`（继承） | `svc:default:{id}` |
| B | `GetFromDb2` | `typeof(Db2RedisManager)` | `redis:main-site` / **db 2** | `svc:default`（继承） | `svc:default:{id}` |
| C | `GetFromMemory` | `typeof(MemoryCacheManager)` | 进程内存，无 Redis | `svc:memory`（覆盖） | `svc:memory:{id}` |

两个管理器的全部代码就是**一行重写**（`Managers/Db1RedisManager.cs` / `Db2RedisManager.cs`）：

```csharp
public class Db1RedisManager(IRedisConnectionFactory f) : RedisCacheManager(f)
{
    protected override int? DatabaseIndex => 1;
}
```

> ℹ️ **为什么不是再写一个连接串。** `DatabaseIndex` 是 `RedisCacheManager` 的四个重写点之一
> （`ConnectionStringKey` / `DatabaseIndex` / `Serializer` / `AllowNullValues`），
> 语义是「同一个连接串 + 代码里指定库号」。代码里给了值，**连接串里的 `defaultDatabase` 就被忽略** ——
> `redis:main-site` 的 `defaultDatabase` 是 11，但 A 实际落在 db 1。好处是三个管理器
> （`RedisCacheManager` / `Db1RedisManager` / `Db2RedisManager`）**共享同一个连接**（工厂按键去重），
> 不重复建连，只是各自 `GetDatabase(n)` 取不同的库。

```bash
# id 取 1000~1999 之间（服务端按 id 现场造数据）。
# ⚠️ 区间外的 id（例如 /1）返回 null —— v0.2.0 起 null 也会被缓存，所以第二次同样是命中；
#    想看到「真实值命中」而不是「空结果命中」，id 才需要落在区间内。
ID=1001

# 第一次：三个方法都是 miss，各 ~1500ms
curl -s "http://localhost:16605/api/samples/redis/default-backend/$ID" \
  | jq '{callCount, a: .methods[0].elapsedMs, b: .methods[1].elapsedMs, c: .methods[2].elapsedMs}'
# → {"callCount":3,"a":1506,"b":1504,"c":1503}

# 第二次：三个方法都命中
curl -s "http://localhost:16605/api/samples/redis/default-backend/$ID" \
  | jq '{callCount, a: .methods[0].elapsedMs, b: .methods[1].elapsedMs, c: .methods[2].elapsedMs}'
# → {"callCount":3,"a":2,"b":1,"c":0}     ← callCount 不再增长 = 三条路径都命中了
```

用 `redis-cli` 核对「A 与 B 同名键、不同库」：

```bash
# 两个库各有同名键 —— 值不同（同一次请求里 A 先写，B 后写）
redis-cli -h <host> -p <port> -n 1  KEYS 'svc:*'     # → svc:default:$ID
redis-cli -h <host> -p <port> -n 2  KEYS 'svc:*'     # → svc:default:$ID

# 连接串的 defaultDatabase(11) 被 DatabaseIndex 覆盖 —— 这里必须为空
redis-cli -h <host> -p <port> -n 11 KEYS 'svc:*'     # → (empty)

# C 走内存，Redis 里根本没有 svc:memory:*
redis-cli -h <host> -p <port> -n 1  KEYS 'svc:memory*'   # → (empty)
```

**关键断言（三条）：**

1. **方法级覆盖真的生效**：A 与 B 的 `cacheName` / `key` 完全相同（继承自同一个类级配置），
   只差 `cacheManager`。如果方法级覆盖没生效，B 会跟 A 撞在 db 1 的同一个键上，
   第二次调用就会出现「只有一个方法重新执行」而不是三个都命中。两个库都能查到同名键，即证据。
2. **`DatabaseIndex` 优先于连接串的 `defaultDatabase`**：`svc:*` 只出现在 db 1 / db 2，
   db 11 里没有。
3. **方法 C 必须显式写 `typeof(MemoryCacheManager)`**（见下面的坑）。

> ⚠️ **「方法 A 不标记 `Cacheable`」的准确含义是「不标记 `cacheManager` 这个参数」。**
> 这是个很容易读错的写法，两种写法后果差别很大：
>
> ```csharp
> [Cacheable(key: "#id")]                       // ✅ 加了注解，不写 cacheManager
> public virtual UserDto? GetDefaultBackend(int id) { ... }
> // → 代理生效，cacheManager 继承类级 [CacheConfig] 的 Db1RedisManager
>
> public virtual UserDto? GetDefaultBackend(int id) { ... }   // ❌ 完全没有注解
> // → 方法根本不被代理，没有缓存；类级 [CacheConfig] 对它完全无效
> ```
>
> 原因是类级 `[CacheConfig]` 的合并发生在 `MethodMetadataCache.ResolveCacheConfig`，
> 而它**只有在方法上存在 `Cacheable` / `CachePut` / `CacheEvict` 时才会被触达**；
> 同时 `CacheExtensions.AddEnableCaching` 也只为「含缓存注解方法的类」建代理。
> 一句话：**`[CacheConfig]` 本身不产生缓存，它只提供默认值。**

> ⚠️ **方法 C 不能靠「不写 `cacheManager`」来表达「用内存」。** 合并逻辑是
> `cacheManager ??= configAttr.CacheManager` —— 不写 = 继承类级值 = 还是 db 1 的 Redis。
> 「不写」与「要框架默认的内存后端」是两件事，想回内存必须显式写
> `cacheManager: typeof(MemoryCacheManager)`。（`MemoryCacheManager` 在
> `Yzl.Extensions.Cache.Managers` 命名空间，样例里由 `using` 引入。）

> ⚠️ **`key` 不参与继承。** `[CacheConfig]` 只有 `DefaultCacheName` / `CacheManager` /
> `DefaultTtlSeconds` / `DefaultSlidingExpirationSeconds` 四个字段，**没有 key 对应的字段** ——
> key 是方法级 SpEL 表达式，每个方法必须自己写（A/B/C 都写了 `key: "#id"`）。
> 同理，方法 A/B 没写 `ttlSeconds`，继承的是类级的 300 秒。

> ⚠️ **想让 8.6 演示「真实值命中」，id 要落在 `1000~1999`。** `DefaultBackendCacheService.IdBase` /
> `IdUpper` 定义了服务端「造数据」的区间，区间内按 id 现场造一条记录，区间外返回 `null`。
> **v0.2.0 起框架默认缓存 `null`（对齐 Spring/Java）**，所以 `redis/default-backend/1` 这类
> 区间外的 id **第二次同样是命中**（A/B/C 三条路径都是）——只是命中的是空结果，
> 正是「缓存穿透防护」生效的表现，不再是改造前的「两次都 ~1500ms」。
> 8.6 的自动化脚本取 `1000 + 时间戳 % 1000`：既落在区间内（命中真实值），
> 又不会撞上上一轮残留在 db 1 / db 2 的 `svc:default:{id}`（TTL 300 秒）；
> 同时 8.6 也补跑了一个区间外 id 的对照断言，专门验证「空结果也落缓存」。
>
> 顺带说明 A/B 的命中耗时差异：两个都走 Redis，每次命中都有一次网络往返，
> 实测 **34ms / 51ms**（`test.sh` 给这两个断言放宽到 `≤200ms`）；C 走进程内，命中是 `0ms`。

---

## 9. 第九章：CacheEvict AllEntries

| # | 端点 | 注解 | 期望 |
|---|------|------|------|
| 9.1 | `GET /api/samples/evict-all/{id}` | `Cacheable(cacheName:"evict-all", key:"#id", ttlSeconds:300)` | 第 1 次 ~1500ms，第 2 次 ~0ms |
| 9.2 | `POST /api/samples/evict-all/evict-single/{id}` | `CacheEvict(key:"#id")` 逐条清除 | 只清 `{id}` 一条，其他 id 仍命中 |
| 9.3 | `POST /api/samples/evict-all/clear-all` | `CacheEvict(allEntries: true)` 内存后端 | ⚠️ **内存缓存下为空操作**，见下方说明 |
| 9.4 | `GET /api/samples/evict-all/redis/{id}` + `POST /api/samples/evict-all/clear-all-redis` | `CacheEvict(allEntries: true, cacheManager: typeof(RedisCacheManager))` | Redis 后端**真删**：清完再查是 ~1500ms |

> ⚠️ **`allEntries` 在内存提供器上不生效。**
> `CacheEvictHandler` 在 `AllEntries = true` 时调用 `ICacheProvider.RemoveByPrefixAsync(cacheName)`，
> 而 `MemoryCacheProvider` 的实现是 `return Task.CompletedTask;`（`IMemoryCache` 原生不支持按前缀遍历删除）：
>
> ```csharp
> // src/Yzl.Extensions.Cache/Providers/MemoryCacheProvider.cs
> public Task RemoveByPrefixAsync(string prefix)
> {
>     // MemoryCache doesn't natively support prefix removal
>     return Task.CompletedTask;
> }
> ```
>
> 因此调用 9.3 后，`evict-all/{id}` 仍会命中缓存。
> 真正生效的是 `RedisCacheProvider.RemoveByPrefixAsync`（`SCAN prefix*` + `DEL`），
> 即 **9.4** —— 它用独立的 `redis:evict-all` 区域落在 db 11，
> 提供 `GET evict-all/redis/{id}`（写入）与 `POST evict-all/clear-all-redis`（真删）两个端点。
> 内存 9.3 与 Redis 9.4 的这一组对照，是本章最值得看的差异。

```bash
# 9.1 缓存两个不同 id
curl -s "http://localhost:16605/api/samples/evict-all/1" | jq '.elapsedMs'   # ~1500
curl -s "http://localhost:16605/api/samples/evict-all/2" | jq '.elapsedMs'   # ~1500

# 9.2 只清 id=1：id=1 重新加载，id=2 仍命中
curl -s -X POST "http://localhost:16605/api/samples/evict-all/evict-single/1" | jq
curl -s "http://localhost:16605/api/samples/evict-all/1" | jq '.elapsedMs'   # ~1500
curl -s "http://localhost:16605/api/samples/evict-all/2" | jq '.elapsedMs'   # ~0

# 9.3 整区清除（内存缓存下为空操作，两个 id 仍命中）
curl -s -X POST "http://localhost:16605/api/samples/evict-all/clear-all" | jq
curl -s "http://localhost:16605/api/samples/evict-all/1" | jq '.elapsedMs'   # ~0（内存缓存下未失效）
curl -s "http://localhost:16605/api/samples/evict-all/2" | jq '.elapsedMs'   # ~0（同上）

# 9.4 Redis 对照：写入 → 命中 → 整区真删 → 重新加载
ID=$(( 2000 + $(date +%s) % 1000 ))
curl -s "http://localhost:16605/api/samples/evict-all/redis/$ID" | jq '.elapsedMs'  # ~1500
curl -s "http://localhost:16605/api/samples/evict-all/redis/$ID" | jq '.elapsedMs'  # 命中（Redis 往返，~40ms）
curl -s -X POST "http://localhost:16605/api/samples/evict-all/clear-all-redis" | jq
curl -s "http://localhost:16605/api/samples/evict-all/redis/$ID" | jq '.elapsedMs'  # ~1500 ← 真的删掉了
# 核对：SCAN 'redis:evict-all*' 应为空（键落在 redis:main-site 的 db 11）
```

---

## 第十章：Redis 连接（配置 / 注入 / 高可用）

第八章演绎的是**「注解上选哪个后端」**；本章往下走一层，演绎**「连接从哪来」**。
对应 Java 的两处可扩展点：改 `spring.data.redis.*` 配置，或自己声明一个
`@Bean RedisConnectionFactory`。

| # | 端点 | 说明 |
|---|------|------|
| 10.1 | `GET /api/samples/connection/config/{id}` | 连接来自 `spring:data:redis` 配置节（一行子类，只换连接名）→ **db 5** |
| 10.2 | `GET /api/samples/connection/injected/{id}` | 连接由用户自己建并注入（`IRedisConnectionSource`）→ **db 8**（配置里写的 7 被跳过） |
| 10.3 | `GET /api/samples/connection/injected-connection-count` | 「同一个连接名只建一次」的计数器证据 |
| 10.4 | `GET /api/samples/connection/options` | 客户端自己用 `[ConfigurationProperties]` 读配置（节点形状与 Java `RedisProperties` 同形） |
| 10.5 | `GET /api/samples/connection/ha/{id}` | 连接层高可用：杀 Redis → 不 500（降级）→ 起回来能自愈，**不用重启宿主** |
| 10.6 | `GET /api/samples/connection/names` | 连接名一览 + 两级查找顺序（用户来源优先于配置） |

> 本章 id 取 **`2000~2999`**（`CacheConnectionService.IdBase` / `IdUpper`），与 8.6 的
> `1000~1999` 刻意错开，避免 TTL 300 秒内两边残留互相干扰。
> 所有 `redis-cli` 示例针对 **`localhost:6399`**（本地可丢弃实例）—— 见 §0 的说明。

### 10.1 连接来自配置节（Java：改 `spring.data.redis.*`）

全部的自定义代码就是一行子类：

```csharp
// src/Yzl.Extensions.Samples.Cache/Managers/ConfigBackendRedisManager.cs
public class ConfigBackendRedisManager(IRedisConnectionFactory f) : RedisCacheManager(f)
{
    protected override string ConnectionStringKey => Name;   // "redis:samples-config"
}
```

代码里**没有任何连接参数**：`host` / `port` / `database` / `timeout` / `connect-timeout` /
`client-name` 全在 `appsettings.json` 的 `spring:data:redis` 节里。改配置就能换实例、
换库、调超时 —— **不需要重新编译，也不需要改一行 C#**。

节点形状与 Java 的 `RedisProperties` **逐字段一致**：这一节只描述**一个** Redis，
没有 `default` / `connections` 这些 Java 里不存在的中间层。

```jsonc
// appsettings.json（片段；凭据一律不写在这里）
"spring": {
  "data": {
    "redis": {
      "host": "localhost",
      "port": 6399,
      "database": 5,
      "connection-string": "abortConnect=false,connectRetry=3",   // 本包扩展的逃生口
      "connect-timeout": "2s",                                    // Java Duration 写法
      "timeout": "2s",
      "client-name": "samples-cache-config-backend"
    }
  }
},
// 连接名 redis:samples-injected 走扁平键（既有形态，且优先于上面这一节）
"redis:samples-injected": "localhost:6399,defaultDatabase=7,abortConnect=false"
```

```bash
ID=$(( 2000 + $(date +%s) % 1000 ))
curl -s "http://localhost:16605/api/samples/connection/config/$ID" | jq '{elapsedMs, configuredDatabase, redisCli}'
# → elapsedMs ~1500（未命中），configuredDatabase 5
curl -s "http://localhost:16605/api/samples/connection/config/$ID" | jq '.elapsedMs'   # 命中，个位数
redis-cli -p 6399 -n 5 KEYS 'conn:config*'     # → conn:config:users:$ID
redis-cli -p 6399 -n 7 KEYS 'conn:config*'     # → (empty)
```

### 10.2 连接由用户自己建（Java：自己声明一个 `@Bean`）

这一路**完全不继承** `RedisCacheManager`，而是直接实现 `ICacheManager`；
连接由用户自己的 `IRedisConnectionSource` 提供：

```csharp
// Managers/InjectedConnectionCacheManager.cs —— 注意 Provider 的声明类型必须是 ICacheProvider
public class InjectedConnectionCacheManager(IRedisConnectionFactory connectionFactory) : ICacheManager
{
    public ICacheProvider Provider { get; } = new RedisCacheProvider(
        connectionFactory.Get(SamplesInjectedConnectionSource.ConnectionName),
        new SystemTextJsonRedisCacheSerializer(),
        databaseIndex: null);          // null ⇒ 用户连接的 DefaultDatabase(8) 说了算
}
```

它**不需要任何注册代码**：核心包自动注册的口径是「实现 `ICacheManager` 的具体类型」，
跟继不继承 `RedisCacheManager` 无关；`sealed`、没有 `[IocService]` 都不影响。

**「用户连接优先于配置」是可验证的**：配置里同一个连接名的扁平键写的是 `defaultDatabase=7`，
而数据实际落在 **db 8** —— 因为 `RedisConnectionProvider.Create()` 的顺序是
**① 用户来源 → ② 配置 → ③ 抛错**，第 ① 步命中后压根不读配置。

```bash
curl -s "http://localhost:16605/api/samples/connection/injected/$ID" | jq '{elapsedMs, actualDatabase, configuredDatabaseForSameName}'
# → actualDatabase 8，configuredDatabaseForSameName 7 —— 这个不一致就是证据
redis-cli -p 6399 -n 7 KEYS 'conn:injected*'   # → (empty)   配置被跳过
redis-cli -p 6399 -n 8 KEYS 'conn:injected*'   # → conn:injected:users:$ID   用户连接生效
```

### 10.3 「只创建一次」的证据

```bash
curl -s "http://localhost:16605/api/samples/connection/injected-connection-count" | jq '.connectCount'
# 连续访问 10.2 若干次后再看，仍是 1（并发打也只有一个）
```

计数在**用户自己的来源**里，不是框架内部 —— 「只创建一次」最终由实现保证，
框架只按连接名去重（`RedisConnectionFactory` 用 `StringComparer.Ordinal` 缓存持有者）。
本样例的来源内部用 `lock` + 二次检查，且**工厂在锁内、发布之前完成**：
失败不留痕，成功才发布。刻意没用 `Lazy<IConnectionMultiplexer>(ExecutionAndPublication)`
—— 它会**永久缓存工厂抛出的异常**，第一次失败之后工厂再也不被调用。

> ⚠️ **释放归属：用户交进来的连接归用户。** 本样例把来源注册成 DI 单例并实现 `IDisposable`，
> 由容器负责释放；框架的 `Dispose` 链刻意不碰用户连接，避免重复释放。

### 10.4 客户端自己读配置

`SamplesRedisConfig` 标了 `[ConfigurationProperties("spring:data:redis")]`，
**没有任何注册代码** —— 全局配置扫描（`AddFeignStarter` 内部会调用）自动把它绑进 DI。

```bash
curl -s "http://localhost:16605/api/samples/connection/options" | jq
```

重点看两个字段：

| 字段 | 结论 |
|------|------|
| `bound` | 直接读到的 `host` / `port` / `database` / `clientName` / `timeout` / `connectTimeout`。**节点是平铺的**，与 Java `spring.data.redis.*` 同形 |
| `shape` | 说明为什么这里没有 `default` / `connections` 层：Java 里那一节也只描述一个 Redis；多个 Redis 走 `@Bean`（本框架是 `AddRedisConnection(...)`） |

> ⚠️ **kebab-case 必须显式声明。** `client-name` / `connect-timeout` 这类带连字符的键要标
> `[ConfigurationKeyName(...)]`，裸写 `ClientName` 是**静默**绑不上（不报错）。
>
> ℹ️ **Duration 只在框架侧解析。** `timeout` / `connect-timeout` 在框架的
> `RedisCacheOptions` 里是 Java `Duration` 语义（`2s` / `500ms` / `PT5S`，无单位按毫秒）；
> 本样例的配置类只把原样字符串读出来。

> 🔒 **本端点与样例配置类都刻意不含 `Password` 字段** —— 凭据不进日志、不进接口响应、不进文档。

### 10.5 连接层高可用（缓存层自愈）

```bash
ID=$(( 2000 + $(date +%s) % 1000 ))
curl -s "http://localhost:16605/api/samples/connection/ha/$ID" | jq '{elapsedMs, likelyHit}'  # ~1500 / false
curl -s "http://localhost:16605/api/samples/connection/ha/$ID" | jq '{elapsedMs, likelyHit}'  # 个位数 / true

# ② 杀掉本地可丢弃实例（⚠ 只对 6399，绝不要对生产实例做这个）
redis-cli -p 6399 shutdown nosave

# ③ 再访问：不 500，elapsedMs 从个位数掉到秒级（连接失败被吞掉，走了真实方法）
curl -s -o /dev/null -w '%{http_code}\n' "http://localhost:16605/api/samples/connection/ha/$ID"   # → 200
curl -s "http://localhost:16605/api/samples/connection/ha/$ID" | jq '.elapsedMs'                 # 秒级（见下方说明）

# ④ 把 Redis 起回来，再访问：缓存自己恢复了命中 —— 不需要重启宿主
/opt/homebrew/bin/redis-server --port 6399 --daemonize yes
curl -s "http://localhost:16605/api/samples/connection/ha/$ID" | jq '{elapsedMs, likelyHit}'      # 第 1 次可能要几秒（重建连接），第 2 次个位数 / true
```

> ℹ️ **第 ③ 步的耗时不是一个固定值。** 连接不可达时 SE.Redis 会按
> `connect-timeout` × `connect-retry` 重试，样例配置是 `2s × 3`，
> 所以实测在 **4~8 秒**区间浮动，之后再加上方法体本身的 ~1500ms。
> **判定标准是「从个位数变成秒级」这个量级跃迁**，不是某个具体数字。
> 同理第 ④ 步**第一次**访问可能要几秒（多路复用器要重新握手），**第二次**才回到个位数 ——
> 这两次都不要重启宿主，正是 `RedisMultiplexerHolder` 的意义所在。

**关键断言（三条）：**

1. **HTTP 状态码不是信号。** 第 ③ 步返回的仍是 **200** —— 连接不可达时
   `RedisConnectionException` 是普通 `Exception`，被 `CacheOperationDispatcher` 记日志后**放行**，
   降级执行真实方法。判定连接通没通**只看 `elapsedMs`**。
2. **只有「类型没注册」才会 500。** 注解里 `cacheManager` 写的类型没注册时抛的是
   `InvalidOperationException`，会穿透 dispatcher 的 `when (ex is not InvalidOperationException)` 降级过滤器。
3. **第 ④ 步必须成功。** 这是本次改造的核心收益：`RedisConnectionProvider` 内部包的是
   `RedisMultiplexerHolder`（**成功才发布、失败不留痕**），不是 `Lazy`。
   改造前同样的操作会**永久 miss**到进程重启为止。

> ℹ️ **开机瞬间 Redis 不可达为什么不抛？** 框架默认 `abortConnect=false`
> （StackExchange.Redis 上游默认是 `true`，这是框架**唯一**改掉的连接默认值）：
> 返回一个已注册但尚未连上的多路复用器，后台自动重连 —— 语义对齐 Java
> （Lettuce 不会因为首次连不上就拒绝创建连接工厂）。
> 想保留旧行为，在连接串或 `spring:data:redis:connection-string` 里显式写 `abortConnect=true`
> （**用户写的值永远优先**）。

### 10.6 连接名与两级查找

```bash
curl -s "http://localhost:16605/api/samples/connection/names" | jq
```

| 级别 | 来源 | 本样例的例子 |
|---|---|---|
| 级别 0 | 容器里的用户 `IRedisConnectionSource`（**优先于配置**） | `redis:samples-injected` |
| ① | 按连接名的扁平连接串键 `configuration[连接名]` | `redis:main-site`、`redis:cas-auth`（第八章） |
| ② | `spring:data:redis` 这一节（且给了端点） | `redis:samples-config` |
| ③ | 都没有 → 抛异常 → 被读/写路径吞掉 → **缓存静默不生效** | — |

> ⚠️ **扁平键排在 `spring:data:redis` 前面是有意的。** 它只对**那一个**连接名生效，
> 比全局唯一的 `spring:data:redis` 更具体 —— 与 Java 里「自己声明的
> `@Bean RedisConnectionFactory` 优先于自动配置」同一方向。于是给全局配置节加一节，
> 不会把既有部署里指向别的实例的连接悄悄改道，第八章的行为一字不变。

> ⚠️ **旧键永不失效。** 扁平连接串键是既有形态，`spring:data:redis` 整节缺失
> 也完全合法 —— 这是「既有部署不改配置也能跑」的兼容保证。

**关键断言：** 10.1 落 db 5、10.2 落 db 8（配置里写 7 被跳过）、10.6 列出的三个
第八章连接名行为与第八章一致 —— 新旧两条路**共存且互不干扰**。


---

## 10. 第十一章：OpenFeign + Cacheable（远程调用结果缓存）

本章验证：**远程调用 + 缓存**组合下 `[Cacheable]` 是否生效。

完整调用链：

```
CacheTestController                 GET /api/samples/feign/{id}
  └─→ FeignCacheService             ← 纯转发，不带缓存注解
        └─→ ICacheDemoFeignClient   ← [Cacheable] 标在这个接口的方法上
              └─→ Samples.Api       TestController.GetByIdSlow()  —— 先睡 10 秒再返回
```

> ⚠️ **前置条件：必须先启动 `Samples.Api`（端口 16600）。**
>
> ```bash
> dotnet run --project src/Samples.Api/Samples.Api.csproj    # 占用 16600
> ```
>
> 该端点是本章新增的 `GET /api/test/users/{id}/slow`，固定耗时 10 秒 —— 刻意把远程耗时
> 放大，让「命中 ≈ 0ms」与「未命中 ≈ 10000ms」的差异一眼可辨。

| # | 端点 | 注解 | 期望 |
|---|------|------|------|
| 11.1 | `GET /api/samples/feign/{id}` | `Cacheable(cacheName:"feign:users", key:"#id", ttlSeconds:60)`（标在 `ICacheDemoFeignClient.GetByIdSlow` 上） | 第 1 次 ~10000ms，第 2/3 次 ~0ms |
| 11.2 | `GET /api/samples/feign/ping` | 无（对照组） | 每次都是真实远程调用，瞬时返回 `pong` |
| 11.3 | `GET /api/samples/feign/call-count` | 诊断 | 只反映「请求进到 FeignCacheService 的次数」，**不能**用来判断缓存命中 |

> ✅ **`[Cacheable]` 可以直接标在 OpenFeign 接口的方法上（与 Java/Spring 一致）。**
>
> ```csharp
> // ✅ 生效 —— 接口方法上的注解会被缓存框架识别
> [Cacheable(cacheName: "feign:users", key: "#id", ttlSeconds: 60)]
> [Get("/api/test/users/{id}/slow")]
> Task<UserDto> GetByIdSlow([PathVariable("id")] long id);
> ```
>
> 注意上面**没有** `cacheManager`：默认走框架自带的 `MemoryCacheManager`，所以 `feign:users` 区域
> 落在**进程内**缓存里，服务重启即失效。想让远程结果跨进程/跨重启共享，
> 只需在注解上显式写 `cacheManager: typeof(RedisCacheManager)`
> （参考第八章 8.1 的写法），并确保 `redis:main-site` 已配置。
>
> `AddEnableCaching` 会扫描**已注册进 DI 容器的接口服务**，为「注解标在接口方法上」的接口
> 叠一层缓存代理（`CacheExtensions.cs` 的 ③ 区域）。命中缓存时该代理只设置返回值、
> 不调用 `invocation.Proceed()`，向下的 OpenFeign 拦截器根本不执行 —— **HTTP 一个字节都不会发出**。
>
> 这条路径对任何「已注册进容器的接口服务」都成立，不限于 OpenFeign。
>
> ⚠️ **代价是一条顺序约束**：`AddEnableCaching` 在调用时会扫描此刻容器里已有的服务描述符，
> 因此必须**最后调用**：
>
> ```csharp
> builder.Services.AddBatchServices();          // 1. 业务服务（[IocService]）
> builder.Services.AddFeignStarter(...);        // 2. 远程客户端接口（[FeignClient]）
> builder.Services.AddEnableCaching();          // 3. 必须放最后
> ```
>
> 晚于它注册的接口不会被包住，而且是**静默**失效。

```bash
# 11.2 先确认下游可用（瞬时返回，不走缓存）
curl -s "http://localhost:16605/api/samples/feign/ping" | jq
# → { "remote": "pong", "elapsedMs": 26, ... }

# 11.1 第 1 次：缓存未命中 → 真实远程调用，服务端睡 10 秒
curl -s "http://localhost:16605/api/samples/feign/1" | jq
# → { "elapsedMs": 10050, "cacheKey": "feign:users:1", "cacheHit": false, ... }

# 11.1 第 2 次：缓存命中 → HTTP 请求根本没发出去
curl -s "http://localhost:16605/api/samples/feign/1" | jq
# → { "elapsedMs": 0, "cacheKey": "feign:users:1", "cacheHit": true, ... }
```

**怎么判断缓存生效 —— 只认 `elapsedMs`：**

未命中 ≈ 10000ms、命中 ≈ 0ms，差异足够大，不需要看别的字段。

**关于 `serviceCallCount` / `methodExecutedThisCall`（已不能作为命中判据）：**

缓存层在 `ICacheDemoFeignClient` 的**接口方法**上，短路发生在这两个字段所属的
`FeignCacheService` **之下**。命中缓存时被拦住的只是更下层的 HTTP 调用，
`FeignCacheService` 的方法体照常执行，因此：

- `methodExecutedThisCall` 恒为 `1`（不再能证明「没发 HTTP」）
- `serviceCallCount` 等于「请求进入该服务的累计次数」，每次刷新都会 +1

它们现在只是链路探针（确认请求确实穿到了这一层），判定命中请以 `elapsedMs` 为准。
`test.sh` 里第十一章的三条断言也正是基于耗时的（`miss` / `hit` / `hit`），不受影响。

> 💡 **为什么第十一章的 id 要用时间戳**：`[Cacheable(cacheName: "feign:users", …)]`
> 上**没有**指定 `cacheManager`，默认即 `MemoryCacheManager` ⇒ 此区域是**进程内**缓存，
> 服务一重启就没了（两次响应都带 10 秒耗时，属正常，不是缓存失效）。
> 用时间戳仍是对的：同一个进程内重复跑脚本时，固定 id 会让「首次未命中」假性失败。

---

## 11. 一键自动化脚本

脚本文件：[test.sh](../test.sh) —— **以文件为准**，本节不再内嵌副本（此前内嵌的旧版本
缺少第十一章，容易误导）。

```bash
chmod +x src/Yzl.Extensions.Samples.Cache/test.sh
./src/Yzl.Extensions.Samples.Cache/test.sh
```

覆盖范围：第一 / 二 / 三（枚举）/ 四 / 六 / 八（8.6）/ 九 / 十（10.1~10.4、10.6）/ 十一章。
脚本自行拉起服务（端口 16605），退出时 `trap` 清理。

> ⚠️ **第十一章需要 `Samples.Api`（16600）同时在运行**，否则该章打印
> `⏭ SKIP: Samples.Api (16600) 未启动`，其余章节不受影响。
>
> ```bash
> dotnet run --project src/Samples.Api/Samples.Api.csproj &
> ./src/Yzl.Extensions.Samples.Cache/test.sh
> ```
>
> 章节 SKIP 条件有两条，都是**整章 SKIP**、不会污染全局通过数：
> `Samples.Api` 未运行 → 跳过第十一章（-4 条）；本地 `6399` 未就绪 → 跳过第十章 10.1/10.2（-9 条）。
> 8.6 与 9.4 不探测 6399 —— 它们的后端是 `redis:main-site`（第八章的扁平键），
> 与本地的 6399 不是同一个实例；这两章在 `redis:main-site` 不可达时会大面积失败，
> 见下方「Redis 相关章节的共同前提」。
>
> 预期通过数（脚本末尾自己打印）：全部就绪 **55**；仅缺 `Samples.Api` **51**；
> 仅缺 6399 **46**；两个都缺 **42**。

**脚本的几个关键设计**（读代码前先了解，否则容易误判为 bug）：

| 设计 | 原因 |
|------|------|
| 就绪探测用 `/` 而非 `/basic/1` | 用 `/basic/1` 探测会预热缓存，导致「1.1 首次未命中」失败 |
| 第十一章用 `FEIGN_ID=$(date +%s)` | 同一进程内重复跑脚本时，固定 id 的缓存还在，「首次未命中」会假性失败 |
| 第十章用 `CONN_ID=$(( 2000 + $(date +%s) % 1000 ))` | 10.1/10.2 是 Redis 缓存（TTL 300 秒），固定 id 会撞上一轮残留；区间与 8.6 的 `1000~1999` 错开 |
| 3.5 用固定 `userId=1` | 服务每次由脚本全新拉起，进程内缓存为空，固定 key 不会让「首次未命中」假性失败 |
| 11.2 用响应内容而非耗时判定 | ping 只有几十毫秒，耗时分不出「真实远程」与「缓存命中」 |
| 第十一章整体包在 `if curl ... 16600` 里 | 下游未启动时降级为 SKIP，不拖垮其余用例 |
| 第十章 10.1/10.2 包在 `if redis_6399_ready` 里 | **不探测就会给出误导性绿灯**：Redis 不可达时缓存静默失效，「未命中用例」全部假性通过（见下方说明） |
| 10.4 用响应内容判定（`取不到（这就是陷阱本身）` / `port 6399` + `database 5`） | 这一节验证的是**配置读取路径**，与缓存是否命中无关，只能用内容断言 |
| 10.5 刻意不在脚本里 | 要 kill 掉 Redis 再拉起来，属破坏性操作；手工执行见 10.5 |

> ⚠️ **为什么第十章必须做前置探测？** Redis 不可达 / 连接名没配时**不会 500**
> （读路径的异常被 `CacheableHandler.TryReadCache` 的 `catch (Exception)` 吞掉、
> 写路径被 `CacheOperationHelper.WriteCache` 的同类 catch 吞掉），表现是「缓存永远不命中」。
> 于是 `assert_timing ... miss` 这类断言会**全部通过**，而 `hit` 断言全部失败 ——
> 只看「有没有报错」会得出「第十章基本正常」的错误结论。整章 SKIP 才是诚实的做法。

---

## 12. 结果记录表

| 章节 | 用例 | 第 1 次 (ms) | 第 2 次 (ms) | 结论 |
|------|------|-------------|-------------|------|
| 1.1 | `basic/1` | | | |
| 1.2 | `basic/short-ttl/1`（+11s 后） | | | |
| 1.3 | `basic/by-name` | | | |
| 1.4 | `basic/age-range` | | | |
| 1.5 | `basic/legacy/1` | | | |
| 1.6 | `basic/legacy-sliding/1` | | | |
| 2.1 | `lifecycle/1` | | | |
| 2.2 | `lifecycle/update`（两次都非 0） | | | |
| 2.3 | `lifecycle/delete` → 再查 2.1 | | | |
| 2.4 | `lifecycle/refresh/1`（两次都非 0） | | | |
| 3.1 | `spel/query` | | | |
| 3.2 | `spel/config` | | | |
| 3.3 | `spel/positional/1` | | | |
| 3.4 | `spel/default-name/1` | | | |
| 3.5 | `spel/benefit/1/Normal` | | | |
| 3.5 | `spel/benefit/1/Gold`（同 userId 换枚举值） | | | |
| 4.1 | `condition/cacheable/99` | | | |
| 4.1 | `condition/cacheable/5`（永不缓存） | | | |
| 4.2 | `condition/unless/1` | | | |
| 4.2 | `condition/unless/999`（null 不缓存） | | | |
| 4.3 | `condition/combined/1` | | | |
| 4.6 | `condition/complex/1` | | | |
| 4.7 | `condition/async-unless/1` | | | |
| 4.8 | `condition/typo-condition/1`（永不缓存） | | | |
| 4.9 | `condition/typo-unless/1`（永不缓存） | | | |
| 4.10 | `condition/typo-chain/1`（永不缓存） | | | |
| 5.1–5.4 | `config/*` | | | |
| 6.1 | `async/100`（种子数据里的 id，第二次命中） | | | |
| 6.1 | `async/777`（区间外 → null；v0.2.0 起 null 也写缓存，第二次同样命中） | | | |
| 6.4 | `async/hot/1`（`unless = "#result == null"` 对照组，两次都 ~1500ms） | | | |
| 6.2 | `async/update`（两次都非 0） | | | |
| 6.3 | `async/all` | | | |
| 7.1 | `sliding/fixed/1`（+11s 后过期） | | | |
| 7.2 | `sliding/basic/1`（持续访问保持命中） | | | |
| 8.1 | `redis/1`（需 Redis，命中约 50–150ms） | | | |
| 8.4 | `redis/memory/1` | | | |
| 8.6 | `redis/default-backend/{1000~1999}` 方法 A（继承类级 → db 1，命中约 50–150ms） | | | |
| 8.6 | `redis/default-backend/{同上}` 方法 B（覆盖 → db 2，命中约 50–150ms） | | | |
| 8.6 | `redis/default-backend/{同上}` 方法 C（覆盖 → 内存，命中 ~0ms） | | | |
| 8.6 | `redis/default-backend/7`（区间外 → null 哨兵，A/B/C 三条路径第二次全部命中） | | | |
| 9.1 | `evict-all/1` | | | |
| 9.2 | `evict-all/evict-single/1` → 再查 | | | |
| 9.3 | `evict-all/clear-all` → 再查（内存下仍命中） | | | |
| 9.4 | `evict-all/redis/{id}` → `clear-all-redis` → 再查（Redis 下真删，回到 ~1500ms） | | | |
| 10.1 | `connection/config/{2000~2999}`（走 `spring:data:redis` 配置节 → db 5，命中约 50–150ms） | | | |
| 10.2 | `connection/injected/{2000~2999}`（用户 `IRedisConnectionSource` → db 8，配置里写的 7 被跳过） | | | |
| 10.3 | `connection/injected-connection-count`（访问 10.2 若干次后仍为 1） | | | |
| 10.4 | `connection/options`（`byPath` 有值 / `dictionaryLookupByFullName` 取不到） | | | |
| 10.5 | `connection/ha/{2000~2999}`（① miss → ② hit → ③ 杀 6399 后仍 200 且跌回秒级 → ④ 起回后自动重命中） | | | |
| 10.6 | `connection/names`（新旧连接名共存，两级查找顺序） | | | |
| 11.1 | `feign/{新id}`（需 Samples.Api） | | | |
| 11.2 | `feign/ping`（需 Samples.Api） | | | |

**判定标准：**

- **Memory 提供器**：命中 ✅ = `elapsedMs ≤ 50`；未命中 ✅ = `elapsedMs ≥ 1000`
- **Redis 提供器（8.x）**：命中 ✅ = `elapsedMs ≤ 200`（每次读都有一次网络往返，实测约 100ms）
- 个别方法体为 500ms / 2000ms（如 `lifecycle/update`、`async/all`），按对应章节说明调整阈值
- **10.5 的判定只看 `elapsedMs`，不看 HTTP 状态码** —— 三步的 HTTP 都是 **200**；
  第 ③ 步（Redis 被杀）必须是 200 + 耗时**从个位数跌回秒级**（降级），
  第 ④ 步（Redis 起回）必须恢复成个位数（**自愈**）。若第 ④ 步连续两次仍是秒级，才说明自愈失效。
  第 ③ 步的具体数字不是固定值：连接不可达时还要叠加 `connect-timeout × connect-retry`
  的重试时间（样例是 `2000 × 3`，实测 4~8 秒），**看量级跃迁，不看单点数值**。
- **Redis 相关章节「缺配置」与「Redis 不可达」对外表现相同**：都是 200 + 缓存静默失效，
  不会 500（缺配置抛出的 `InvalidOperationException` 也被同两条 `catch (Exception)` 吞掉）。
  **但时效不同**：缺配置是纯方法体耗时（`elapsedMs ≈ 1500`）；Redis 不可达还要先等
  `connect-timeout × connect-retry` 的重试（实测 4~8 秒）才走到方法体。
  排查方向见 §0「关于 Redis」与第十章 10.5。

**第十一章的实测记录**（`Samples.Api` + Cache 同时运行，id=7）：

| 调用 | elapsedMs | serviceCallCount | methodExecutedThisCall | cacheHit |
|------|-----------|------------------|------------------------|----------|
| 第 1 次 | 10043 | 1 | 1 | false |
| 第 2 次 | 0 | 2 | 1 | true |
| 第 3 次 | 0 | 3 | 1 | true |

`methodExecutedThisCall` 恒为 1 属**预期**：缓存层在 `ICacheDemoFeignClient` 接口上，
短路发生在 `FeignCacheService` 之下，该服务的方法体每次都会执行。
判定命中只看 `elapsedMs`（10043 → 0 → 0）。

同时核对 `Samples.Api` 的访问日志：三次客户端调用只对应**一条** `GET /api/test/users/7/slow`
记录 —— 缓存命中时 HTTP 请求确实没有发出去（这才是「缓存生效」的硬证据，耗时只是佐证）。

---

## 13. 关键文件索引

| 文件 | 作用 |
|------|------|
| [Program.cs](../Program.cs) | `AddBatchServices()` → `AddFeignStarter` → `AddEnableCaching`（**顺序不可换**）；后端由注解上的 `cacheManager` 选择 |
| [Controllers/HomeController.cs](../Controllers/HomeController.cs) | 导航页 `/api/samples` + 统计 `/api/samples/stats`（受 Transient 生命周期限制恒为 0） |
| [Controllers/BasicCacheController.cs](../Controllers/BasicCacheController.cs) | 第一章：基础 `Cacheable` |
| [Controllers/CacheLifecycleController.cs](../Controllers/CacheLifecycleController.cs) | 第二章：`CachePut` / `CacheEvict` |
| [Controllers/SpelKeyController.cs](../Controllers/SpelKeyController.cs) | 第三章：SpEL 键表达式 |
| [Controllers/ConditionalController.cs](../Controllers/ConditionalController.cs) | 第四章：`Condition` / `Unless` / 拼写错误容错 |
| [Controllers/ConfigInheritanceController.cs](../Controllers/ConfigInheritanceController.cs) | 第五章：`[CacheConfig]` 继承 |
| [Controllers/AsyncCacheController.cs](../Controllers/AsyncCacheController.cs) | 第六章：异步缓存 |
| [Controllers/SlidingExpirationController.cs](../Controllers/SlidingExpirationController.cs) | 第七章：滑动过期 |
| [Controllers/RedisCacheController.cs](../Controllers/RedisCacheController.cs) | 第八章：Redis / Memory 提供器 |
| [Controllers/CacheEvictAllController.cs](../Controllers/CacheEvictAllController.cs) | 第九章：`allEntries` 批量清除 |
| [Controllers/CacheConnectionController.cs](../Controllers/CacheConnectionController.cs) | 第十章：Redis 连接（配置节 / 用户注入 / 只建一次 / 冒号陷阱 / 连接层高可用） |
| [Services/CacheConnectionService.cs](../Services/CacheConnectionService.cs) | 第十章：两条连接路径的被代理服务（`conn:config:users` / `conn:injected:users`，各睡 1500ms） |
| [Managers/ConfigBackendRedisManager.cs](../Managers/ConfigBackendRedisManager.cs) | 第十章 10.1：**一行子类** —— 只重写 `ConnectionStringKey`，连接参数全在 `spring:data:redis` |
| [Managers/InjectedConnectionCacheManager.cs](../Managers/InjectedConnectionCacheManager.cs) | 第十章 10.2：**不继承 `RedisCacheManager`** 的手写 `ICacheManager`（`Provider` 声明类型必须是 `ICacheProvider`） |
| [Managers/SamplesInjectedConnectionSource.cs](../Managers/SamplesInjectedConnectionSource.cs) | 第十章 10.2/10.3：`IRedisConnectionSource` 实现（`lock` + 二次检查、建连计数、`IDisposable`） |
| [Configuration/SamplesRedisConfig.cs](../Configuration/SamplesRedisConfig.cs) | 第十章 10.4：`[ConfigurationProperties("spring:data:redis")]` 的客户端配置类（**刻意无 `Password` 字段**，`client-name` 标 `[ConfigurationKeyName]`） |
| [appsettings.json](../appsettings.json) | 第十章：`spring:data:redis` 节（db 5，与 Java `RedisProperties` 同形）+ 扁平键 `redis:samples-injected`（db 7），**不含任何凭据** |
| [Controllers/CacheTestController.cs](../Controllers/CacheTestController.cs) | 第十一章：OpenFeign + `Cacheable` 的三个端点（11.1/11.2/11.3） |
| [Feign/ICacheDemoFeignClient.cs](../Feign/ICacheDemoFeignClient.cs) | 第十一章：OpenFeign 声明式接口，**`[Cacheable]` 标在此接口的方法上**（注解写接口上即可生效的示例） |
| [Services/FeignCacheService.cs](../Services/FeignCacheService.cs) | 第十一章：纯转发服务（不带缓存注解），链路探针计数器 |
| [Services/](../Services/) | 全部被代理的服务类（`[IocService]` + `virtual` 方法 + 缓存注解） |
| `src/Samples.Api/Controllers/TestController.cs` 的 `GetByIdSlow` | 第十一章下游：`GET /api/test/users/{id}/slow`，固定睡 10 秒（端口 16600） |
