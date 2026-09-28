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

---

## 0. 启动

```bash
cd /Users/yangzonglei/Documents/code/github.com/dotnet/dotnet-extensions-samples

# 清理残留进程
lsof -ti:16605 | xargs kill -9 2>/dev/null

# 启动（默认：仅内存缓存）
dotnet run --project src/Yzl.Extensions.Samples.Cache
```

启动日志会出现以下二选一：

```text
✓ 内存缓存已启用（如需 Redis，请在 appsettings.Development.json 中配置 redis:main-site）
```

或

```text
✓ Redis 缓存已启用
```

### 关于 Redis

`Program.cs` 读取配置项 `redis:main-site` 决定是否启用 Redis：

```csharp
var redisConn = builder.Configuration["redis:main-site"];
if (!string.IsNullOrEmpty(redisConn))
{
    builder.Services.AddEnableCaching(assemblies: null, enableRedis: true, redisConnectionString: redisConn);
}
else
{
    builder.Services.AddEnableCaching(assemblies: null, enableRedis: false);
}
```

连接字符串在 `appsettings.Development.json` 的 `redis:main-site` 中配置（该文件不入库，需自行按实际环境填写）。**未配置 Redis 时，第八章的 `redis/*` 端点会因缺少 `RedisCacheProvider` 而失败**，其余章节不受影响。

### 服务注册方式

本示例用 `[IocService]` 特性 + `AddBatchServices()` 批量扫描注册服务，而非逐个手写 `AddTransient<T>()`：

```csharp
builder.Services.AddBatchServices();               // 扫描 [IocService] 并注册
builder.Services.AddEnableCaching(assemblies: null, enableRedis: false);
```

> ⚠️ 缓存注解依赖 Castle DynamicProxy，因此**被注解的方法必须是 `virtual`**（本示例全部满足）。

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

---

## 3. 第三章：SpEL 键表达式

| # | 端点 | SpEL 键表达式 | 说明 |
|---|------|--------------|------|
| 3.1 | `GET /api/samples/spel/query?userId=1&keyword=abc` | `#qo.UserId:#qo.Keyword` | 对象属性参与拼键 |
| 3.2 | `GET /api/samples/spel/config` | `#cfg.site_name:#cfg.version` | `Dictionary` 索引式访问 |
| 3.3 | `GET /api/samples/spel/positional/1` | `#p0` | 位置参数 `#p0` / `#p1` |
| 3.4 | `GET /api/samples/spel/default-name/1` | `#id`（无 `cacheName`） | 未指定 `cacheName` 时走默认区域 |
| 3.5 | `GET /api/samples/spel/sliding-redis/1` | `#id` + `slidingTtl` + `CacheType.Redis` | SpEL + 滑动 + Redis 组合 |

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

# 3.5 SpEL + 滑动 + Redis（需启用 Redis）
curl -s "http://localhost:16605/api/samples/spel/sliding-redis/1" | jq
```

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

类级 `[CacheConfig]` 提供默认 `cacheName` / `ttlSeconds` / `cacheType`，方法级注解可逐项覆盖：

```csharp
[CacheConfig(defaultCacheName: "config-demo", defaultTtlSeconds: 120, defaultCacheType: CacheType.Memory)]
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
| 6.1 | `GET /api/samples/async/{id}` | `Cacheable(cacheName:"async:users", key:"#id", ttlSeconds:60)` | 第 1 次 ~1500ms，第 2 次 ~0ms |
| 6.2 | `POST /api/samples/async/update` | `CachePut(key:"#user.Id", ttlSeconds:60)` | 每次执行（~500ms），并刷新 6.1 的缓存 |
| 6.3 | `GET /api/samples/async/all` | `Cacheable(key:"'all'", ttlSeconds:30)` | 常量键（~2000ms → ~0ms） |
| 6.4 | `GET /api/samples/async/call-count` | — | 异步方法执行计数（Transient 下恒为 0，见 §1） |

```bash
# 6.1 异步 Cacheable
curl -s "http://localhost:16605/api/samples/async/1" | jq '{elapsedMs, callCount}'
curl -s "http://localhost:16605/api/samples/async/1" | jq '{elapsedMs, callCount}'   # ~0ms

# 6.2 异步 CachePut：始终执行（~500ms），并把结果写回 async:users:1
curl -s -X POST "http://localhost:16605/api/samples/async/update" -d "id=1&name=AsyncBob&age=28" | jq
curl -s -X POST "http://localhost:16605/api/samples/async/update" -d "id=1&name=AsyncBob&age=28" | jq
curl -s "http://localhost:16605/api/samples/async/1" | jq '{data, elapsedMs}'        # ~0ms，且数据已被 6.2 更新

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

> 需先在 `appsettings.Development.json` 配置 `redis:main-site` 连接字符串并重启。未配置时本章端点不可用。

| # | 端点 | 配置 | 说明 |
|---|------|------|------|
| 8.1 | `GET /api/samples/redis/{id}` | `CacheType.Redis`, TTL 300s | 数据落在 Redis，多实例共享 |
| 8.2 | `GET /api/samples/redis/sliding/{id}` | Redis + `slidingTtl: 300` + 绝对上限 86400s | Redis 滑动过期 |
| 8.3 | `POST /api/samples/redis/update` | `CachePut` + `CacheType.Redis` | 更新 DB 同时刷新 Redis |
| 8.4 | `GET /api/samples/redis/memory/{id}` | `CacheType.Memory` | 与 8.1 对比：进程内缓存，多实例不共享 |

```bash
# 8.1 Redis 读写（命中时仍有一次 Redis 往返，~100ms 属正常，非 0ms）
curl -s "http://localhost:16605/api/samples/redis/1" | jq '{cacheType, cacheKey, elapsedMs}'
curl -s "http://localhost:16605/api/samples/redis/1" | jq '{elapsedMs}'   # 命中，约 50–150ms

# 8.4 对照：Memory 提供器（命中为 0ms 量级）
curl -s "http://localhost:16605/api/samples/redis/memory/1" | jq '{cacheType, elapsedMs}'
curl -s "http://localhost:16605/api/samples/redis/memory/1" | jq '{elapsedMs}'

# 8.3 更新
curl -s -X POST "http://localhost:16605/api/samples/redis/update" -d "id=1&name=RedisBob&age=33" | jq
```

用 `redis-cli` 直接验证键是否落库：

```bash
redis-cli -h localhost -p 6379 --scan --pattern "redis:users:*"
```

> 键格式为 `{cacheName}:{key}`，且 `cacheName` 与 `key` 之间**只保留一个 `:`** —— `cacheName` 自身含 `:`（`redis:users`）不会退化成 `redis:users1`。

**关键断言：** 8.1 与 8.4 使用**不同的缓存区域**（`redis:users` vs `memory:users`），因此各自的首次调用都会真实执行（~1500ms），第二次才命中。

---

## 9. 第九章：CacheEvict AllEntries

| # | 端点 | 注解 | 期望 |
|---|------|------|------|
| 9.1 | `GET /api/samples/evict-all/{id}` | `Cacheable(cacheName:"evict-all", key:"#id", ttlSeconds:300)` | 第 1 次 ~1500ms，第 2 次 ~0ms |
| 9.2 | `POST /api/samples/evict-all/evict-single/{id}` | `CacheEvict(key:"#id")` 逐条清除 | 只清 `{id}` 一条，其他 id 仍命中 |
| 9.3 | `POST /api/samples/evict-all/clear-all` | `CacheEvict(allEntries: true)` 整区清除 | ⚠️ **内存缓存下为空操作**，见下方说明 |

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
> 因此本示例（未启用 Redis 时）调用 9.3 后，`evict-all/{id}` 仍会命中缓存。
> 真正生效的是 `RedisCacheProvider.RemoveByPrefixAsync`（`SCAN prefix*` + `DEL`），
> 即服务里的 9.4 `EvictAllFromRedis`（本示例未暴露 HTTP 端点）。
> 验证 `allEntries` 请启用 Redis 后使用 `redis:evict-all` 区域。

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
```

---

## 10. 一键自动化脚本

保存为 `src/Yzl.Extensions.Samples.Cache/test.sh`：

```bash
#!/bin/bash
set -e

cd "$(dirname "$0")/../.."
PROJECT="src/Yzl.Extensions.Samples.Cache"
HOST="http://localhost:16605/api/samples"
PASS=0
FAIL=0

cleanup() {
  kill %1 2>/dev/null; wait 2>/dev/null
  lsof -ti:16605 | xargs kill -9 2>/dev/null
}

# $1=描述 $2=实际值 $3=判定：hit | miss
assert_timing() {
  local desc="$1" actual="$2" kind="$3"
  local ok
  if [ "$kind" = "hit" ]; then
    [ "$actual" -le 50 ] && ok=1 || ok=0
  else
    [ "$actual" -ge 1000 ] && ok=1 || ok=0
  fi
  if [ "$ok" -eq 1 ]; then
    echo "  ✅ $desc (${actual}ms)"
    PASS=$((PASS+1))
  else
    echo "  ❌ $desc (${actual}ms, 期望 $kind)"
    FAIL=$((FAIL+1))
  fi
}

ms() { curl -s "$1" | sed -n 's/.*"elapsedMs":\([0-9]*\).*/\1/p'; }
ms_post() { curl -s -X POST "$1" -d "$2" | sed -n 's/.*"elapsedMs":\([0-9]*\).*/\1/p'; }

cleanup
dotnet run --project "$PROJECT" 2>/dev/null &
sleep 6

echo "===== 第一章：基础 Cacheable ====="
assert_timing "1.1 首次未命中" "$(ms $HOST/basic/1)"        miss
assert_timing "1.1 二次命中"   "$(ms $HOST/basic/1)"        hit

echo "===== 第二章：CachePut / CacheEvict ====="
assert_timing "2.1 首次未命中" "$(ms $HOST/lifecycle/1)"    miss
assert_timing "2.1 二次命中"   "$(ms $HOST/lifecycle/1)"    hit
# 2.4 refresh 是 CachePut，两次都应真实执行（~1500ms）
assert_timing "2.4 CachePut 第 1 次仍执行" "$(ms $HOST/lifecycle/refresh/1)" miss
assert_timing "2.4 CachePut 第 2 次仍执行" "$(ms $HOST/lifecycle/refresh/1)" miss
# 2.3 CacheEvict 后 2.1 应重新执行
ms_post "$HOST/lifecycle/delete" "id=1" > /dev/null
assert_timing "2.3 Evict 后重新加载" "$(ms $HOST/lifecycle/1)" miss

echo "===== 第四章：Condition / 拼写错误 ====="
# 种子数据只有 id=1/2/5/99；99 满足 #id > 10
assert_timing "4.1 id=99 首次未命中" "$(ms $HOST/condition/cacheable/99)" miss
assert_timing "4.1 id=99 二次命中"   "$(ms $HOST/condition/cacheable/99)" hit
assert_timing "4.1 id=5 永不缓存(1)" "$(ms $HOST/condition/cacheable/5)"  miss
assert_timing "4.1 id=5 永不缓存(2)" "$(ms $HOST/condition/cacheable/5)"  miss
assert_timing "4.8 typo-condition"  "$(ms $HOST/condition/typo-condition/1)" miss
assert_timing "4.9 typo-unless"     "$(ms $HOST/condition/typo-unless/1)"    miss

echo "===== 第六章：异步缓存 ====="
assert_timing "6.1 首次未命中" "$(ms $HOST/async/1)"        miss
assert_timing "6.1 二次命中"   "$(ms $HOST/async/1)"        hit

echo "===== 第九章：CacheEvict ====="
assert_timing "9.1 首次未命中" "$(ms $HOST/evict-all/1)"    miss
assert_timing "9.1 二次命中"   "$(ms $HOST/evict-all/1)"    hit
# 9.2 逐条清除对内存提供器有效
ms_post "$HOST/evict-all/evict-single/1" "" > /dev/null
assert_timing "9.2 逐条清除后重新加载" "$(ms $HOST/evict-all/1)" miss
# 9.3 allEntries 在 MemoryCacheProvider 上是空操作（RemoveByPrefixAsync 未实现）
ms_post "$HOST/evict-all/clear-all" "" > /dev/null
assert_timing "9.3 allEntries 内存下为空操作(仍命中)" "$(ms $HOST/evict-all/1)" hit

cleanup
echo "===== 结果: $PASS passed, $FAIL failed ====="
[ "$FAIL" -eq 0 ] && echo "🎉 全部通过!" || echo "😢 有失败用例"
```

运行：

```bash
chmod +x src/Yzl.Extensions.Samples.Cache/test.sh
./src/Yzl.Extensions.Samples.Cache/test.sh
```

---

## 11. 结果记录表

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
| 6.1 | `async/1` | | | |
| 6.2 | `async/update`（两次都非 0） | | | |
| 6.3 | `async/all` | | | |
| 7.1 | `sliding/fixed/1`（+11s 后过期） | | | |
| 7.2 | `sliding/basic/1`（持续访问保持命中） | | | |
| 8.1 | `redis/1`（需 Redis，命中约 50–150ms） | | | |
| 8.4 | `redis/memory/1` | | | |
| 9.1 | `evict-all/1` | | | |
| 9.2 | `evict-all/evict-single/1` → 再查 | | | |
| 9.3 | `evict-all/clear-all` → 再查（内存下仍命中） | | | |

**判定标准：**

- **Memory 提供器**：命中 ✅ = `elapsedMs ≤ 50`；未命中 ✅ = `elapsedMs ≥ 1000`
- **Redis 提供器（8.x）**：命中 ✅ = `elapsedMs ≤ 200`（每次读都有一次网络往返，实测约 100ms）
- 个别方法体为 500ms / 2000ms（如 `lifecycle/update`、`async/all`），按对应章节说明调整阈值

---

## 12. 关键文件索引

| 文件 | 作用 |
|------|------|
| [Program.cs](../Program.cs) | `AddBatchServices()` + `AddEnableCaching`，Redis 开关 |
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
| [Services/](../Services/) | 全部被代理的服务类（`[IocService]` + `virtual` 方法 + 缓存注解） |
