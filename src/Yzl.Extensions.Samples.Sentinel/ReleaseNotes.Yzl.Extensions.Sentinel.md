# Yzl.Extensions.Sentinel 更新日志

## v0.1.0 [2026/09/10]

首次发布。镜像 Java Alibaba Sentinel 1.8.8 的限流 / 熔断 / 黑白名单能力，配置模型与 Java Spring Cloud Alibaba 的 `spring.cloud.sentinel` 对齐，可直连 Sentinel Dashboard 下发规则、上报指标。

### 🚀 新功能

1. **限流熔断引擎**：`SentinelEngine.TryEntryAsync(resource, origin)` 一次调用完成 Authority 黑白名单 → Flow 限流 → Degrade 熔断三段检查；未命中返回 `SentinelEntry`，命中按原因抛 `SentinelBlockException` 子类。放行后由 `entry.Exit(error)` 回填 RT、成功 / 异常、并发线程数并驱动熔断状态机。
2. **无锁滑动窗口 `SentinelLeapArray`**：镜像 Java `LeapArray`，默认 2×500ms 双桶统计。
3. **四种流控控制器（对齐 Java 同名类）**：`SentinelDefaultController`（超阈值直拒）、`SentinelWarmUpController`（冷启动预热曲线）、`SentinelRateLimiterController`（匀速排队 / 漏桶）、`SentinelWarmUpRateLimiterController`（预热 + 排队）。规则字段 `grade` 0=并发线程数 / 1=QPS，`controlBehavior` 0=直拒 / 1=预热 / 2=匀速排队 / 3=预热+排队。
4. **三种熔断器 + 状态机**：`SentinelSlowRequestRatioCircuitBreaker`（慢调用比例）、`SentinelExceptionCircuitBreaker`（异常比例 grade=1 / 异常数 grade=2），状态机 `SentinelAbstractCircuitBreaker` 驱动 CLOSED → OPEN → HALF_OPEN；OPEN 期间直接拒绝，半开只放行 1 个探测请求，探测成功回 CLOSED、失败重新 OPEN。
5. **三个规则管理器**：`SentinelFlowRuleManager` / `SentinelDegradeRuleManager` / `SentinelAuthorityRuleManager`，负责规则持有、校验与控制器 / 熔断器工厂装配，支持运行时热更新。
6. **请求中间件 `SentinelMiddleware`（`UseSentinel()`，须在 `UseRouting` 之后）**：
    - URL 纳管 / 豁免：`filter.urlPatterns` / `filter.excludeUrlPatterns`（Ant 风格），未纳管请求直接放行且不建节点
    - 资源 key 推导：显式路由模板 >（约定）路由路径，未匹配端点返回空放行
    - origin（调用方）解析：`IRequestOriginParser`，内置从 `filter.origin-header` 指定的请求头读取
7. **`[SentinelResource]` 特性**：显式资源名覆盖，且支持 `blockHandler` / `fallback` / `defaultFallback` / `fallbackClass` / `blockHandlerClass` / `exceptionsToTrace` / `exceptionsToIgnore`（镜像 Java `@SentinelResource`）；命中且配置了 handler 时经 `SentinelResourceFallbackInvoker` 反射调用并写出降级响应，未配置则原样抛异常。
8. **全局开关 `SentinelSwitch`**：启动值取自 `spring.cloud.sentinel.enabled`，运行时由命令中心 `/setSwitch` 覆盖；关闭时中间件直接放行，不建节点、不限流（紧急一键放开）。
9. **命令中心 `SentinelCommandCenter`**：内嵌 Kestrel，独立端口（默认 8719，被占用自动 +1），绑定 `0.0.0.0`（Dashboard 需反连），提供 9 个端点：`/version`、`/getRules`、`/setRules`、`/metric`、`/jsonTree`、`/clusterNode`、`/cnode`、`/getSwitch`、`/setSwitch`。
10. **Dashboard 心跳与机器注册 `SentinelHeartbeatSender`**：`POST {dashboard}/registry/machine`，默认 10s 一次、3s 超时；上报 `hostname`、`ip`、`app`、`app_type=0`、`port`、`v`、`version`（毫秒时间戳）；`Interlocked` 防在途请求重叠。
11. **Pull 型指标导出 SPI**：`ISentinelMetricsExporter` 由宿主实现（启动自动发现，须 public 无参构造，唯一实现才激活，零实现跳过、多实现告警不激活）；`ISentinelMetricsProvider` 提供 `CollectCurrent` / `CollectRange` / `CollectTotals`。**库本身不依赖任何监控库**（prometheus-net 等由宿主自行引入）。
12. **数据源 SPI**：`ISentinelDataSourceProvider`（按类型标识匹配配置节）、`ISentinelRuleSource`、`IWriteableRuleSource`、`ISentinelRuleConverter`（规则字符串 ↔ 规则对象双向转换，内置 json / xml，可 `converter-class` 反射自定义）；`AddSentinel()` 沿引用链自动发现数据源包，引包即用。

### 🛡 拦截异常体系与响应中间件

1. **BlockException 异常家族**（镜像 Java `BlockException`）：抽象基类 `SentinelBlockException`（持 `Resource` + `Reason` + `Response`）与子类 `SentinelFlowException`（限流）/ `SentinelDegradeException`（熔断）/ `SentinelAuthorityException`（黑白名单）。
2. **异常自带响应规格 `SentinelBlockExceptionResponse`**：`StatusCode` / `ViewName`（Razor 视图）/ `JsonTemplate` / `Message` / `HtmlFallback`。子类构造器内覆盖 `Response` 即可定制「跳转哪个视图 / 输出什么 JSON」，无需改库、无需改配置。
3. **新增 `UseSentinelBlockExceptionHandler(options)` 中间件**：挂在 `UseSentinel()` 外层，捕获 `SentinelBlockException` 渲染响应 —— 页面请求（Accept text/html）渲染宿主 Razor 视图，渲染失败回退 `HtmlTemplate` → 内置默认 HTML；API 请求（`[ApiController]` / JSON）恒走 JSON，模板为空则回退内置默认 JSON。
4. **配置参数按场景字段级覆盖**（`SentinelBlockExceptionHandlerOptions`）：`Flow` = 限流，`Degrade` = 降级 + 鉴权；`effectiveXxx = 场景配置?.Xxx ?? 异常自带 Response.Xxx`，只在明确设置的字段上生效；无参调用 `UseSentinelBlockExceptionHandler()` 等价空选项，行为与旧版一致。
5. **职责拆分对齐 Java `AbstractSentinelInterceptor`**：`SentinelMiddleware` 只负责检查并抛异常，响应渲染全部交给外层中间件；未注册该中间件时异常照常上抛给宿主全局异常处理。**两条接管路径互斥**，不可同时生效。业务异常不受影响，仍由宿主全局异常处理接管。
6. **移除 `spring.cloud.sentinel.response` 配置节**（连同 `SentinelResponseConfig`）：429 响应规格改为「异常自带 + 中间件方法参数」定制。

### 📈 指标

1. **新增累计计数导出**：`ISentinelMetricsProvider.CollectTotals()` + `SentinelTotalNode`（pass / block / success / exception / rt），数据源为 `SentinelStatNode` 的 `Interlocked` 累计字段，进程生命周期单调递增、采集无锁、热路径零开销，语义等价 Micrometer Counter，供 Prometheus `*_total` 系列。默认实现返回空集合，自定义 Provider 不实现即「无累计指标」，其余能力不受影响。
2. **累计计数与秒级指标刻意分离**：秒级 `SentinelMetricNode` 同时是 Dashboard `/metric` 的线格式契约，混入累计值会让 `CollectRange` 回溯路径出现恒为 0 的伪累计值；且秒级 Gauge 在 scrape 间隔 >1s 或抖动时会丢秒，累计计数对任意窗口 `rate()` / `increase()` 都正确。
3. **新增 `spring.cloud.sentinel.metrics.include-only-ruled-resources`**（默认 false）：只对外 pull 指标导出「当前有规则」的资源（flow / degrade / authority 任一命中），便于让 Prometheus series 收敛到受治理接口；Dashboard `/metric` 走分钟环，不受该开关影响，仍展示全部有流量资源。过滤条件与 `CollectCurrent` 保持一致，避免 `*_total` 与 `*_qps` 系列覆盖的资源集合分叉。
4. **`SentinelMetricNode` 快照字段**：`Resource` / `PassQps` / `BlockQps` / `SuccessQps` / `ExceptionQps` / `Rt` / `Concurrency` / `Timestamp`，与建议的 metric 名称一一对应（文档给出对照表）。

### ⚙️ 配置模型

配置节迁移至 **`spring:cloud:sentinel`**（对齐 Java Spring Cloud Alibaba），同一份 Nacos / 配置中心内容可在 Java 服务与 .NET 服务间直接复用；**不保留旧 `sentinel` 前缀回退**。

1. **改用 `[ConfigurationProperties(SectionName)]` 声明式绑定**：由手工 `configuration.GetSection("sentinel")` 绑定改为 `AddConfigurationProperties<SentinelOptions>()`，编译期绑定、无反射、AOT 友好。
2. **kebab-case 键用 `[ConfigurationKeyName]` 绑定**：.NET 8 binder 不会自动把 kebab 映射到 PascalCase。
3. **应用名解析收敛为 `ApplicationNameResolver.GetApplicationName(configuration)`**：`spring:application:name` > 入口程序集名 > 执行程序集名 > `application`（镜像 Java `getApplicationId`）；移除 `app-name` 覆盖键与 `"unknown"` 兜底。
4. **`Yzl.Extensions.Sentinel.csproj` 新增 `Yzl.Extensions.Core` 条件引用**（源码 / NuGet 双模式），并声明 `FrameworkReference Microsoft.AspNetCore.App`（Kestrel 命令中心 / 中间件 / Razor 视图渲染）。
5. 规则来源三个渠道，优先级：**外部数据源 > 本地 `spring.cloud.sentinel.rules` > `/setRules` 运行时推送**；配置了外部数据源时本地规则被忽略并告警。

```json
{
  "spring": {
    "application": { "name": "Zujuan.PC" },
    "cloud": {
      "sentinel": {
        "enabled": true,
        "eager": true,
        "transport": {
          "dashboard": "10.111.120.166:8080",
          "port": 8719,
          "heartbeat-interval-ms": 10000,
          "client-ip": "172.16.25.139"
        },
        "filter": {
          "urlPatterns": ["/**"],
          "excludeUrlPatterns": ["/favicon.ico", "/", "/Home/Index"],
          "origin-header": "X-App-Id"
        },
        "metrics": { "include-only-ruled-resources": false },
        "datasource": {
          "flow-ds": {
            "nacos": {
              "server-addr": "10.1.25.43:10086",
              "namespace": "fa9dc1b8-23b7-4ed7-8b48-62d18adc700f",
              "group-id": "DEV",
              "data-id": "zujuan-flow-config-test",
              "rule-type": "flow",
              "data-type": "json"
            }
          }
        }
      }
    }
  }
}
```

### 🔧 修复

1. **熔断窗口结构与阈值聚合对齐 Java 1.8.8**（修复前熔断几乎不触发，10 并发慢请求全 200）：
    - 统计窗口改为 `LeapArray(1, statIntervalMs)` —— **单桶覆盖整个统计区间**。原先按 `statIntervalMs / minRequestAmount` 拆成 200 个 5ms 小桶，并发请求分散到不同桶，单桶永远凑不齐 `minRequestAmount=5`，熔断永不触发。
    - 阈值判断聚合整个统计区间（单桶下「当前窗口」即整个区间，等价且零分配）。
    - 异常数模式（grade=2）改走滑动窗口 + `minRequestAmount` 门槛 + 基类 half-open 分发。原先累计计数器不走窗口、缺门槛、且 override 掉 `OnRequestComplete` 导致半开探测短路，熔断后永不复原。
    - 边界：`slowRatioThreshold` 默认 1.0 时，全慢请求（ratio == 1.0）需显式触发（`ratio == threshold == 1.0` 分支），慢比例用严格 `>`；否则该极端场景永不熔断。
2. **`SentinelLeapArray` 无锁滑动窗口两处缺陷**：位掩码 `timeId & _mask` 在 sampleCount 较大时失效 → 改 `timeId % _sampleCount`；publish-then-reset 竞态 → 改 fresh-window CAS 换窗。
3. **对接 Sentinel Dashboard 的四个契约坑（已实测）**：
    - 心跳 `app_type` 必须发 `0`：发 `1` 会被 Dashboard 当网关应用，展示「请求链路」而非「簇点链路」。
    - `/metric` 时间戳必须是 Unix **毫秒**：发秒会让 Dashboard 曲线落在 1970 年。
    - 命令中心必须绑定 `0.0.0.0`：绑回环导致 Dashboard 反连失败、metric 列表为空。
    - `client-ip` 自动解析镜像 Java `HostNameUtil.getIp()`：遍历网卡、过滤未启用 / 回环 / 虚拟接口（utun / vmnet / docker 等），优先 site-local IPv4。
4. **数据源装配加固**：数据源创建 / 启动失败 pass-through，不阻断应用启动；坏配置解析异常保持 last-good 规则，绝不把非法内容当「空」清空规则静默放行；`eager` 时启动后立即应用首个值，保证首个请求前规则已就位。

### ⚠️ 注意事项

1. **命令中心无认证**（镜像 Java `SimpleHttpCommandCenter`）：必须用防火墙限制在可信网络，防止任意内网主机调用 `/setRules` 推送恶意或清空规则。
2. **`.NET 8 对接 Dashboard 不需要 Netty**：Java 客户端默认的 `sentinel-transport-simple-http` 就是裸 Socket 手写 HTTP，协议即普通 HTTP。Netty 只出现在可选的 `netty-http` 传输模块和集群限流模式（本包未实现集群限流）。
3. 常见误区：`grade: 0` 是**并发线程数**模式，串行请求天然满足；要验证 QPS 限流必须用 `grade: 1`。

### 📚 文档

1. 新增包内文档 `Yzl.Extensions.Sentinel.md`（定位、架构图、目录结构、快速开始、配置模型、核心抽象、限流 / 熔断时序图、命令中心、数据源 SPI、对外接口全景）。
2. 新增端到端验证资产：`docs/Sentinel-E2E-Test-Guide.md`、`docs/Sentinel-Degrade-E2E-Report.md`、`docs/Sentinel-3Features-E2E-Verification.md`、`docs/sentinel-e2e-test/`（appsettings 与验证脚本）。

### 依赖

1. 依赖 `Yzl.Extensions.Core`（应用名解析），源码 / NuGet 双模式条件引用。
2. `FrameworkReference Microsoft.AspNetCore.App`（Kestrel 命令中心、请求中间件、Razor 视图渲染）。
