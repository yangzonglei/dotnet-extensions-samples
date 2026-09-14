# Yzl.Extensions.Samples.Sentinel

Sentinel 限流 / 熔断演示样例，对接 Java Sentinel 生态的 .NET 8 实现。

## 用途

演示 [Yzl.Extensions.Sentinel](https://github.com/yangzonglei/dotnet-extensions) 的核心能力：

| 能力 | 说明 |
|------|------|
| 限流（Flow） | 滑动窗口 QPS 限流（2×500ms 窗口，镜像 Java `LeapArray`） |
| 熔断（Degrade） | 慢请求比例熔断器（RT 阈值 / 慢比例 / 半开探测，镜像 Java `ResponseTimeCircuitBreaker`） |
| 资源全量纳管 | 资源 key = URL（显式路由模板 / 请求路径，对标 Java `CommonFilter`），启动预注册全部 Controller 路由 |
| 系统请求过滤 | `spring.cloud.sentinel.filter.excludeUrlPatterns` 排除首页 / favicon 等系统 URL（不建节点、不入调用链路） |
| Dashboard 对接 | 每 10s 心跳上报 `transport.dashboard` + 命令中心（默认端口 8719）反向拉取/推送规则 |
| 429 响应按异常子类跳转 | 拦截异常由库内 `UseSentinelBlockExceptionHandler` 中间件接管（镜像 Java BlockException 语义）：浏览器渲染异常子类自带 Response 指定的 Razor 视图（URL 不变），API 返回 429 JSON；视图名 / HTML 模板 / JSON 模板 / 文案 / 状态码可用 `UseSentinelBlockExceptionHandler(options => …)` 配置参数按场景（Flow 限流 / Degrade 降级·鉴权）定制（配置优先，异常自带 Response 兜底），零 appsettings |
| Nacos 数据源 | `spring.cloud.sentinel.datasource.{name}.nacos` 从 Nacos 拉取并热更新规则（未配置时回退 `spring.cloud.sentinel.rules` 本地规则） |

## 端口

| 服务 | 端口                                     |
|------|----------------------------------------|
| 本应用 | `16609`                                |
| 命令中心（Command Center） | `8719`（占用时自动顺延）                        |
| Sentinel Dashboard（心跳上报目标） | `127.0.0.1:8080`（未启动时仅记 warning，不影响运行） |

## 运行方式

```bash
cd /Users/yangzonglei/Documents/code/github.com/dotnet/dotnet-extensions-samples

# Sentinel 包尚未发布 NuGet：默认源码引用（指向本地 ../dotnet-extensions/src）
dotnet run --project src/Yzl.Extensions.Samples.Sentinel
```

> 源码引用需 `dotnet-extensions` 与 `dotnet-extensions-samples` 为同级目录。包发布后可删除
> `Yzl.Extensions.Samples.Sentinel.csproj` 中的 `<UseProjectReference>true</UseProjectReference>`，
> 恢复为 NuGet 包引用默认模式（也可用 `-p:UseProjectReference=false` 临时切换）。

## 演示端点

| 路由 | 资源 key | 演示内容 |
|------|---------|---------|
| `/` | （被 filter 排除） | 首页（特性导航，系统页） |
| `/Home/RateLimitDemo` | `/Home/RateLimitDemo` | QPS 限流（2 QPS）。快速刷新触发 429，渲染 `Views/Shared/RateLimit.cshtml` |
| `/Home/Slow` | `/Home/Slow` | 慢请求熔断（RT>300ms 为慢、慢比例 ≥50% 且 ≥5 请求即熔断 10s） |
| `/api/ratelimit` | `/api/ratelimit` | API 限流（1 QPS）。快速调用返回硬编码 429 JSON |
| `/api/degrade/fail` | `degrade:api:error-count` | 熔断降级 API（无 Fallback）：`?fail=true` 抛异常 → 全局异常处理 503 JSON；连续抛异常触发熔断后 → 429 JSON |

浏览器快速刷新限流页面即可看到 429 渲染页（URL 不变）。

## 配置结构（appsettings.json）

镜像 Java Spring Cloud Alibaba 的 `spring.cloud.sentinel` 前缀（资源 key 与 Java `CommonFilter` 一致用 URL）：

```yaml
spring:
  cloud:
    sentinel:
      enabled: true            # 全局开关；false 时全部放行
      eager: true              # 启动即加载规则，保证首个请求已有规则
      transport:
        dashboard: 127.0.0.1:8080
        port: 8719             # 命令中心端口
        client-ip: 127.0.0.1
      filter:
        urlPatterns: ["/**"]               # 纳入 Sentinel 的 URL（默认全量）
        excludeUrlPatterns:                # 系统请求过滤：命中即放行、不建节点、不入调用链路
          - /favicon.ico
          - /
          - /Home/Index
          - /Home/Error
      rules:                   # 本地规则（配置了 datasource 时被忽略并告警）；resource 用 URL
        flow:    [{ resource: "/Home/RateLimitDemo", grade: 1, count: 2 }, ...]
        degrade: [{ resource: "/Home/Slow", grade: 0, count: 300, ... }]
```

> 应用名（Dashboard 注册的 `app` 字段）取自 `spring:application:name`，未配置则回退入口程序集名。

> 429 拦截响应不再配置（`spring.cloud.sentinel.response` 已移除）：拦截异常（`SentinelFlowException` 限流 /
> `SentinelDegradeException` 熔断 / `SentinelAuthorityException` 鉴权）由库内中间件
> `UseSentinelBlockExceptionHandler` 接管——按**异常子类自带的 `Response` 规格**跳转：浏览器请求渲染
> 该规格 `ViewName` 指定的宿主 Razor 视图（默认 `RateLimit` / `Degrade`，URL 不变），API 请求输出
> 该规格 `JsonTemplate` 指定的 429 JSON（默认结构硬编码）。镜像 Java
> `AbstractSentinelInterceptor`：动作声明了 `BlockHandler` 时先调 blockHandler，未配置才抛出给库中间件
> （未注册该中间件时继续上抛给宿主自己的全局异常处理，两层可选）。
>
> ⚠️ **两条拦截异常接管路径互斥**：注册了库中间件 → 拦截异常被它捕获、不再上抛（宿主全局异常处理
> 收到的只剩普通业务异常）；不注册库中间件 → 拦截异常照常抛到宿主，宿主必须自行按子类型接管。
> 二者不可同时生效，选其一。本样例的 [ApiExceptionHandler.cs](ApiExceptionHandler.cs) 即按前者设计：
> catch filter 用 `ex is not SentinelBlockException` 显式排除拦截异常，只降级普通业务异常。
>
> 想改「页面 HTML / 视图模板 / API JSON 结构」，**首选 `UseSentinelBlockExceptionHandler(options => …)`
> 配置参数**（按场景定制，配置优先、异常自带 Response 兜底）；更细粒度场景（按异常子类区分）才子类化
> 异常覆盖 `Response`（见下文）。

### 定制 BlockException 跳转目标

#### 方式一：中间件配置参数（推荐，改一行即可）

`UseSentinelBlockExceptionHandler` 接受一个可选 lambda，按场景（`Flow` 限流 / `Degrade` 降级·鉴权）
设置 `ViewName`（视图模板）、`HtmlTemplate`（页面 HTML 模板）、`JsonTemplate`（API JSON 结构）、
`Message`（文案）、`StatusCode`。**配置参数优先**：明确设置的字段覆盖异常自带 `Response`；未设置
（null）的字段沿用异常值（含默认）；完全不配置时行为与旧版一致。

```csharp
app.UseSentinelBlockExceptionHandler(options =>
{
    // 限流：页面仍渲染宿主 RateLimit 视图（@Model.Message 显示定制文案）；API 返回自定义 JSON
    options.Flow = new SentinelBlockResponseOptions
    {
        Message = "访问过于频繁，请稍后再试",
        JsonTemplate = """{"code":{code},"msg":"{message}","resource":"{resource}","time":"{time}"}""",
    };
    // 降级/鉴权：API 返回自定义 JSON；页面配置自定义 HTML 模板（视图缺失/渲染失败时输出）
    options.Degrade = new SentinelBlockResponseOptions
    {
        Message = "服务暂不可用（已触发熔断），请稍后再试",
        HtmlTemplate = "<!DOCTYPE html><html>…自定义熔断页面…</html>",
        JsonTemplate = """{"code":{code},"msg":"{message}","time":"{time}"}""",
    };
});
```

- `JsonTemplate` 支持占位符 `{code}` `{message}` `{resource}` `{detail}` `{time}`；空则回退内置默认 JSON。
- `ViewName` 为空或视图渲染失败时回退 `HtmlTemplate`（再回退异常自带 `HtmlFallback` / 系统内置默认 HTML）；
  设 `ViewName = ""` 可显式禁用视图渲染（只想要纯 HTML 时）。
- 页面视图经 `@Model.Message` / `@Model.Resource` / `@Model.Time` 展示动态信息（HTML 模板为纯静态，不替换占位符）。

#### 方式二：子类化拦截异常覆盖 Response（按异常子类细分）

想按「更细粒度」定制（如仅某个资源抛的自定义异常），无需改库、无需 `appsettings`——**子类化拦截异常并
覆盖 `Response`**（`SentinelBlockExceptionResponse` 的 `StatusCode` / `ViewName` / `JsonTemplate` /
`Message` / `HtmlFallback` 字段均可设）：

```csharp
// 自定义限流异常：页面跳 CustomLimit 视图，API 返回自定义 JSON
public sealed class CustomFlowException : SentinelFlowException
{
    public CustomFlowException(string resource, string message) : base(resource, message) { }

    public override SentinelBlockExceptionResponse Response { get; } = new()
    {
        ViewName = "CustomLimit",
        JsonTemplate = """{"code":{code},"msg":"{message}","resource":"{resource}","time":"{time}"}""",
    };
}
```

- `JsonTemplate` 支持占位符 `{code}` `{message}` `{resource}` `{detail}` `{time}`；空则回退内置默认 JSON。
- `ViewName` 为空或视图渲染失败时回退 `HtmlFallback`，再回退系统内置默认 HTML。
- 让库 `SentinelEngine` 抛出你的子类：继承后覆盖 `Response` 即可，`Reason`（限流/熔断/鉴权）仍随父类，
  判定逻辑不变。

### 资源 key 推导规则（对标 Java CommonFilter + UrlCleaner）

优先级：显式 `[SentinelResource]` 属性 > 显式路由模板（归一化为 `/api/users/{id}`）> 请求路径。

| 路由类型 | 资源 key 示例 | 说明 |
|---------|--------------|------|
| 属性路由 | `/api/ratelimit`、`/api/users/{id}` | 用路由模板归一化（UrlCleaner 效果，路径参数天然归并） |
| 约定路由 | `/Home/RateLimitDemo` | 用请求路径（对标 Java `request.getRequestURI()`） |
| 未匹配端点 | （不建节点） | 404 / 静态资源直接放行，不污染调用链路 |

启动时会把**所有** Controller 路由预注册为统计节点，因此 Dashboard「调用链路」在零流量时也能看到
完整路由清单；被 `excludeUrlPatterns` 排除的系统端点不注册。

### Nacos 数据源（可选）

配置 `spring.cloud.sentinel.datasource.{name}.nacos` 即从 Nacos 拉取/监听规则，未配置时回退读取 `spring.cloud.sentinel.rules`：

```yaml
spring:
  cloud:
    sentinel:
      datasource:
        flow-datasource:
          nacos:
            server-addr: 127.0.0.1:8848
            data-id: flow-rules
            group-id: DEFAULT_GROUP
            rule-type: flow        # flow / degrade
            data-type: json
```

> 需同时引用 `Yzl.Extensions.Sentinel.DataSource.Nacos` 包（本样例已引用）并在启动时调用
> `AddSentinelNacosDataSource()`。Nacos 宕机时规则不加载、请求直通，后台指数退避重试（2s→60s）。

## 对接 Sentinel Dashboard

1. 启动 Java Sentinel Dashboard（如 `java -Dserver.port=8080 -jar sentinel-dashboard.jar`）。
2. 启动本样例，每 10s 自动上报心跳，Dashboard「机器列表」可见本机。
3. Dashboard「调用链路」展示全部预注册的 Controller 路由（URL 资源名，系统页已被过滤）。
4. 在 Dashboard 或通过命令中心 HTTP 反向管理规则：
   - 查询规则：`curl "http://127.0.0.1:8719/getRules?type=flow"`
   - 推送规则：`curl -X POST "http://127.0.0.1:8719/setRules" -d 'type=flow&data=[...]'`
   - 秒级指标：`curl "http://127.0.0.1:8719/metric?startTime=<ms>&endTime=<ms>"`

> **安全提示**：命令中心**无认证**（镜像 Java `SimpleHttpCommandCenter`，其同样无认证），并绑定 `0.0.0.0`
> 供远端 Dashboard 反连拉取 `/metric`、`/tree`。任何能访问 8719 端口的主机都可调用 `/setRules`
> 推送恶意或清空规则。生产必须用防火墙把 8719 限制在可信网络、仅允许 Dashboard 所在主机访问，
> 切勿暴露到公网或不可信网段。
