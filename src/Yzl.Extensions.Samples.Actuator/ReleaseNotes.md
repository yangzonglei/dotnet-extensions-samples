# Yzl.Extensions.Actuator 更新日志

## v0.1.9 [2026/06/29]

### 🚀 新功能
1. **改用 `[ConfigurationProperties]` 特性配置绑定**：配置类全部添加 `[ConfigurationProperties]` 特性声明配置前缀，注册方式从手动 `services.Configure<T>()` 改为 `AddConfigurationPropertiesScan(configuration)` 自动扫描注册
   - `ManagementServerOptions` → `[ConfigurationProperties("management:server")]`
   - `ManagementEndpointShutdownOptions` → `[ConfigurationProperties("management:endpoint:shutdown")]`
   - `ManagementEndpointHttpTraceOptions` → `[ConfigurationProperties("management:endpoint:httptrace")]`
   - `ManagementWebEndpointsOptions` → `[ConfigurationProperties("management:endpoints:web")]`

### 🔧 修复
1. **更新命名空间引用**：`Yzl.Extensions.Common` → `Yzl.Extensions.Core`，适配 Core 包重命名
2. **新增 `PackageId` 属性**：csproj 中显式声明 `Yzl.Extensions.Actuator` 包 ID
3. **`HttpTraceRepository` 容量从配置读取**：修改 `HttpTraceRepository` 注册方式，使其容量从 `management:endpoint:httptrace:capacity` 配置读取，而非固定使用默认值 100

### 🏗 架构重构：Actuator 端点基类化

#### 新增基础设施
1. **新增 `ActuatorEndpointBase`**：为所有 Actuator 端点提供统一基类，自动处理 Id/Operations 声明、IsEnabled 门控检查和 IActuatorResult 响应序列化。子类只需通过 `DefineOperation(handler)` 注册操作。
2. **新增 `IActuatorResult` + `ActuatorResults`**：统一端点返回值抽象，提供 `Ok()` / `NoContent()` / `NotFound()` / `BadRequest()` / `Error()` 工厂方法。handler 不再直接操作 `HttpContext`，由基类自动序列化。
3. **新增 `EndpointCachingStrategy<T>`**：统一端点缓存策略，支持永久缓存（Lazy<T>）和 TTL 过期（双检锁 + volatile CachedEntry），消除各端点手写缓存逻辑的重复。

#### 端点迁移
1. **所有端点继承 `ActuatorEndpointBase`**：HealthEndpoint、InfoEndpoint、MetricsEndpoint、EnvEndpoint、LoggersEndpoint、CachesEndpoint、BeansEndpoint、MappingsEndpoint、ConditionsEndpoint、HttpTraceEndpoint、ShutdownEndpoint、MetadataEndpoint 全部迁移，消除 12 个端点的重复类骨架代码。
2. **Handler 签名从 `Task` 改为 `Task<IActuatorResult>`**：10 个端点使用推荐方式，handler 返回结果对象而非直接操作 `HttpContext`。ShutdownEndpoint 保留 `Task` 重载以保持 flush→StopApplication 时序。
3. **IsEnabled 门控集中化**：`HttpTraceEndpoint` 和 `ShutdownEndpoint` 中的内联 `_options.Enabled` 检查移至基类 `IsEnabled` 虚属性，路由映射层也统一使用 `ManagementWebEndpointsOptions.IsEndpointEnabled()`。

## v0.1.8 [2026/06/28]

### 新功能
1. 新增 loggers API `groups` 字段支持，从 `logging:group` 配置节读取 logger 分组，兼容 SBA 3.x 格式
2. 新增 loggers API `DELETE /actuator/loggers/{loggerName}` 处理，兼容 SBA 3.x 重置日志级别方式
3. 新增 `git.properties` 自动生成（MSBuild Target），构建时提取 git 分支、commit 等信息写入输出目录
4. build info 输出格式对齐 Spring Boot：增加 `artifact`、`name`、`group` 字段
5. git info 输出格式对齐 Spring Boot：点号 key 转为嵌套结构（如 `git.commit.id` → `commit.id`）
6. **新增 beans 端点**：`GET /actuator/beans`，列出 DI 容器中所有注册的服务，对齐 Spring Boot Actuator 标准格式（aliases/scope/type/resource/dependencies）
7. **新增 httptrace 端点**：`GET /actuator/httptrace`，HTTP 请求追踪，显示最近 N 条请求的方法、URI、状态码、耗时和头信息（敏感头自动跳过）
8. **新增 shutdown 端点**：`POST /actuator/shutdown`，优雅关闭应用，默认禁用
9. **新增 HttpTraceMiddleware**：不缓存响应体，性能开销 < 0.05ms/请求，敏感请求头自动过滤
10. **新增 HttpTraceRepository**：线程安全的循环缓冲区，默认容量 100 条，可通过 `management:endpoint:httptrace:capacity` 配置
11. **新增 ActuatorOptions 属性**：`ShutdownEnabled`（默认 false）、`HttptraceEnabled`（默认 false）、`HttpTraceCapacity`（默认 100）、`CorsOrigins`（默认 ["*"]）
12. **新增 `IsEndpointEnabled()`**：在根端点列表和路由映射中过滤已禁用的端点，支持按需启用
13. **新增 `UseSpringNetActuatorHttpTrace()`** 扩展方法：注册 HttpTrace 中间件到请求管道
14. **注册 IServiceCollection 到 DI**：供 BeansEndpoint 读取所有服务注册信息

### 新增配置变更
```json
{
  "management": {
    "endpoint": {
      "shutdown": { "enabled": true },
      "httptrace": { "enabled": true, "capacity": 200 }
    },
    "endpoints": {
      "web": {
        "exposure": {
          "corsOrigins": ["http://sba-server:9090", "https://monitor.example.com"]
        }
      }
    }
  }
}
```

### CORS 配置说明
管理端口的 CORS 策略通过 `ActuatorOptions.CorsOrigins`（配置路径 `management:endpoints:web:exposure:corsOrigins`）控制：
- 默认 `["*"]`：允许任意来源，方法限制为 `GET/OPTIONS`，阻止浏览器跨域写入
- 可配置为具体来源列表（如 `["http://sba-server:9090"]`）在生产环境中收窄
- CORS 仅影响浏览器端跨域请求，server-to-server 的监控调用不受 CORS 限制

### 性能优化
1. **响应序列化**：`HttpContextResponseExtensions` 改为 `SerializeAsync` 直接写入响应流，避免中间字符串分配（影响所有 Endpoint）
2. **构建信息缓存**：`BuildInfoContributor` 反射结果 + 文件 IO 移至静态构造函数，进程生命周期内只执行一次
3. **Metadata 端点缓存**：`MetadataEndpoint` 反射结果移至构造函数缓存，消除每次请求的反射开销
4. **Git 信息缓存**：`GitInfoContributor` git.properties 文件解析结果移至静态构造函数，消除每次请求的磁盘 IO
5. **Env 端点线程安全**：`_cachedProviderPayloads` 改为 `ConcurrentDictionary`，消除锁外读取的数据竞争
6. **缓存批量清除**：`MemoryCacheRegistry.Clear()` 改用 `Compact(1.0)`，替代反射读取所有条目再逐个删除
7. **进程资源释放**：5 个 Metrics 类（`ProcessCpuUsage`、`SystemCpuUsage`、`ThreadCount`、`ThreadPeak`、`SystemMemoryUsage`）释放 `Process` 对象，消除 OS 句柄泄漏
8. **磁盘健康检查缓存**：`DiskSpaceHealthContributor` 添加 5 秒缓存，避免每次健康检查触发磁盘 IO
9. **配置脱敏优化**：`DefaultPropertySanitizer` 6 次 `Contains` 循环改为编译正则 `Regex.IsMatch`
10. **JVM 内存指标**：3 个 JVM 内存 Metrics（committed/max/used）消除 `ToLowerInvariant()` 字符串分配
11. **GC 暂停指标**：`GcPauseMetric.TagsMatch` 中 LINQ `Contains` 改为 `for` 循环，消除枚举器分配
12. **响应序列化优化**：所有 Endpoint 响应直接流式写入，零中间 JSON 字符串
13. **NLog logger 发现优化**：`NLogLoggerManagement` 从反射扫描 `AppDomain.GetAssemblies()` 改为**枚举 IServiceCollection**，只发现实际注册在 DI 中的服务类型，避免全量反射加载程序集类型的性能开销，首次 `/actuator/loggers` 请求延迟从 10-200ms 降至 ~1ms
14. **CPU 指标无锁化**：`ProcessCpuUsageMetric`、`SystemCpuUsageMetric` 移除 `lock`，改为 `volatile` + 不可变记录缓存模式（同 `DiskSpaceHealthContributor`），高并发下无锁读取，吞吐量不再受锁争用限制
15. **/proc/stat 缓存**：`SystemCpuUsageMetric` 新增 `ProcStatCache` 2 秒缓存，Linux 生产环境下避免每次指标采集都读取 `/proc/stat` 文件 IO，高频轮询下文件 IO 减少 99.9%

### 问题修复
1. 修复 SBA 3.3.5 重置日志级别失败（POST `{}` 空 JSON 体无 `configuredLevel` 属性导致 500）
2. 修复 `GitInfoContributor` 解析冲突（`commit.id` 字符串值与 `commit.id.xxx` 子路径冲突时崩溃）
3. 删除 `MetadataEndpoint` 中未使用的死代码 `HandleAsync1()`
4. 修复 `NLogLoggerManagement._cachedAssemblyTypes` 中 `HasIocServiceAttribute` 过滤逻辑被注释后导致扫描所有类型的问题，改为只添加 DI 中注册的服务类型
5. 移除 `NLogLoggerManagement` 中废弃的 `HasIocServiceAttribute`、`ContainsIocServiceInMembers`、`AddIocServicesByReflection` 方法

### 构建
1. 新增 `GenerateGitProperties` MSBuild Target，构建后自动生成 `git.properties` 

## v0.1.7 [2026/05/14]
1. 优化 metrics 端点，支持按 `tag` 查询指标并返回 description、baseUnit、availableTags 等完整 Spring Boot Actuator 响应信息
2. 扩展指标模型，新增多 measurement 输出、标签过滤和可用标签声明能力，提升与 Spring Boot Admin 指标展示兼容性
3. 优化 `jvm.gc.pause` 指标，新增 COUNT、TOTAL_TIME、MAX 统计值，并补充 cause、action、gc 标签信息
4. 优化 `jvm.memory.used`、`jvm.memory.committed`、`jvm.memory.max` 指标，支持 heap、nonheap 区域标签查询
5. 新增 .NET GC 与进程内存指标，包括 `dotnet.gc.heap.size`、`dotnet.gc.committed`、`dotnet.gc.total.available`、`dotnet.gc.memory.load`、`dotnet.process.memory.working_set`、`dotnet.process.memory.private`、`dotnet.process.memory.virtual`
6. 优化线程指标描述与采集逻辑，`jvm.threads.daemon` 改为返回当前 .NET ThreadPool 工作线程使用量
7. 更新 Yzl.Extensions.Actuator 包版本至 0.1.7

## v0.1.6
占位

## v0.1.5 [2026/05/14]
1. 新增 IActuatorServiceConfigurator 扩展点，支持外部 Actuator 插件自动参与服务注册
2. 新增 Yzl.Extensions.Actuator.Nacos 项目，提供 Nacos 健康检查贡献者并支持根据 nacos 配置自动注册配置中心服务
3. 新增 ping、diskSpace 内置健康检查，health 端点返回各检查项 details 并汇总整体状态
4. 优化 env 端点，增加 runtime 配置源并支持按配置项名称查询 PID 等运行时属性
5. 优化 metrics 指标注册表，新增 jvm.threads.peak、jvm.threads.daemon 指标并补充指标描述、单位和标签信息
6. 重构 CPU、GC、内存、线程等指标采集实现，移除 GcInfoProvider 反射依赖并降低采集开销

## v0.1.4 [2026/05/10]
1. 优化 loggers 端点，返回 LoggerDescriptor 结构并支持按名称查询、更新 configuredLevel
2. 重构 NLog 日志管理，增加日志发现排除前缀扩展点，缓存 logger 名称并提升日志级别计算准确性
3. 优化 env 端点，增加全量配置和单个配置源 30 秒缓存，统一 Spring Boot Actuator JSON 输出
4. 优化 metrics 端点和指标注册表，缓存指标名称列表并统一响应序列化逻辑
5. 优化 CPU、GC、缓存等指标采集实现，减少反射、集合分配和重复对象创建
6. 优化 health 检查执行流程，改为并行执行所有 HealthContributor
7. 优化 conditions 端点，缓存条件报告并避免请求时解析服务实例
8. 修复 ActuatorEndpointMapper 在独立管理端口场景下使用临时 scope 解析服务的问题
9. 修复健康状态字符串输出，显式对齐 UP、DOWN、OUTOFSERVICE、UNKNOWN
10. 新增 Yzl.Extensions.Actuator 项目架构与执行流程说明文档
11. 优化 publish.nuget.org.sh脚本，增加-p:Version参数，避免依赖的包版本号被修改

## v0.1.3 [2026/04/14]
1. 重构NLog日志管理以提升类型发现性能（由扫描*.dll改为获取 Controller+类上面的[IocService]特性，避免出现很多无用类型）

## v0.1.1 [2026/04/10]
1. 重构 NLogLoggerManagement,提升读取性能

## v0.1.0 [2026/04/09]
1. 首次发布