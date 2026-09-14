# Yzl.Extensions.Sentinel.DataSource.Nacos 更新日志

## v0.1.0 [2026/09/10]

首次发布。`Yzl.Extensions.Sentinel` 的 Nacos 规则数据源扩展：从 Nacos 拉取并长轮询监听流控 / 熔断 / 鉴权规则，并把 Dashboard 上改动的规则写回 Nacos。引包即被核心库自动发现，零代码接入。

### 🚀 新功能

1. **数据源 SPI 实现 `SentinelNacosDataSourceProvider`**（`ProviderType = "nacos"`）：核心库 `AddSentinel()` 沿引用链自动发现，引包即用；另提供显式注册 `AddSentinelNacosDataSource()`（`TryAddEnumerable`，幂等，用于早期 / 显式注册场景）。
2. **`SentinelNacosRuleSource` 一个数据源同时承担读与写**：实现 `ISentinelRuleSource`（拉取 + 监听）与 `IWriteableRuleSource`（写回）。
3. **后台异步初始化，不阻塞应用启动**：`Start()` 只起后台任务 —— 阶段 1 注册长轮询监听；阶段 2 预检可达 + 拉取首个配置。Nacos 不可达时应用照常起来（pass-through），恢复后规则自动就位。
4. **可达性预检**：用独立 `HttpClient`（5s 超时）直连 `GET /nacos/v1/console/health/readiness`。区分「Nacos 宕机」与「配置被清空」—— 避免把宕机误判成规则清空而放行全部流量。
5. **指数退避重试 2s → 4s → … → 60s**：监听注册与首次拉取失败都自动重试直到成功或进程关停。
6. **长轮询订阅 `SentinelNacosListener`**：Nacos 配置内容变化 → 回调 `ReceiveConfigInfo` → 更新 `RawProperty` → 核心库 `SentinelDataSourceManager` 热更新对应规则管理器，无需重启。
7. **`ConfigUseRpc = false`**：强制走 HTTP 协议（非 gRPC），兼容 Nacos 1.x / 2.x。
8. **写回 `WriteAsync(rules)`**：Dashboard 改规则 → 命令中心 `/setRules` → 内存热更新 → 写回 Nacos。用**本数据源配置的转换器**序列化（`Converter.Serialize`），保证读 / 写方向格式一致（json / xml / 自定义 `converter-class`），不再固定写 JSON。

### 🔧 修复 / 加固

1. **写回保留配置描述 `desc`（`preserve-desc`，默认 true）**：nacos-sdk-csharp 的 `PublishConfig` 没有 desc 参数，走 SDK 发布会被 Nacos 服务端全量覆盖**清空配置描述**。改为原始 HTTP 发布：
    - `GET /nacos/v1/cs/configs?show=all&dataId=&group=&tenant=` 读回当前 `desc`（`show=all` 是可靠的 desc 读取方式）
    - `POST /nacos/v1/cs/configs` 显式传 `content` + `type` + `desc` + `accessToken`
    - 原始发布失败自动回退 SDK 4 参 `PublishConfig`（保证内容能发布，desc 不保留并记 warning）
    - 不需要保留 desc 时可配 `"preserve-desc": false`，纯走 SDK 4 参发布
2. **写回显式传 config `type`**：nacos-sdk-csharp 1.3.6 的 **3 参** `PublishConfig(dataId, group, content)` 内部硬编码 `type="text"`，会把 JSON / XML 配置的 type 元数据重置为 text（Nacos 控制台随之按 text 渲染该配置）。现在 type 由转换器声明（`ISentinelRuleConverter.DataType`，内建 json / xml，默认 json），统一走 4 参发布，**3 参重载不再使用**。
3. **开启 auth 的 Nacos 写回全链路支持**：配置 `username` / `password` 后先登录取 accessToken（`POST /nacos/v1/auth/users/login`，v1 兼容端点 `/nacos/v1/auth/login` 兜底）；**读 desc 与发布都带 token**（不带 token 会 401 读不到 desc，desc 照样被清）；遇 401 / 403 清缓存重登一次、用新 token **重读 desc** 后重发（服务端 token 有 TTL）；发布与读取均按地址列表故障转移。
4. **规则转换器 SPI 落地**：`converter-class` 反射自定义转换器（优先）> `data-type` 分发 json / xml 内建；非法 `data-type` 抛 `NotSupportedException`，不再静默清空规则（`DataType` 从「是不是 json」的开关变为显式格式声明）；自定义转换器由 AppDomain 已加载程序集扫描解析。
5. **坏配置保持 last-good**：解析异常时保持当前规则、绝不把非法内容当「空」清空规则（由核心库 `SentinelDataSourceManager` 统一兜底），写回与读方向使用同一转换器，不破坏自定义格式。

### ⚙️ 配置

```json
{
  "spring": {
    "cloud": {
      "sentinel": {
        "datasource": {
          "degrade-ds": {
            "nacos": {
              "server-addr": "10.1.25.43:10086",
              "server-addresses": ["http://10.1.25.43:10086/"],
              "namespace": "fa9dc1b8-23b7-4ed7-8b48-62d18adc700f",
              "group-id": "DEV",
              "data-id": "zujuan-degrade-config-test",
              "rule-type": "degrade",
              "data-type": "json",
              "preserve-desc": true,
              "username": "",
              "password": ""
            }
          }
        }
      }
    }
  }
}
```

| 键 | 默认值 | 说明 |
|---|---|---|
| `server-addr` | `127.0.0.1:8848` | Nacos 地址（可逗号分隔多个）；配置 `server-addresses` 时被忽略 |
| `server-addresses` | 无 | 地址列表（元素可为 `host:port` 或完整 `http://host:port`），非空时优先 |
| `namespace` | 空 | Nacos 命名空间 ID |
| `username` / `password` | 空 | 账号（生产开启 auth 时用于登录取 accessToken） |
| `group-id` | `DEFAULT_GROUP` | 配置分组 |
| `data-id` | `""`（必填） | 规则配置 data-id |
| `data-type` | `json` | 规则数据格式（`json` / `xml` / `converter-class` 自定义） |
| `converter-class` | 无 | 自定义 `ISentinelRuleConverter` 类型全名（优先于 `data-type`） |
| `rule-type` | 必填 | `flow` / `degrade` / `authority`（决定灌入哪个规则管理器） |
| `preserve-desc` | `true` | 写回时是否保留配置描述 `desc` |
| `timeout-ms` | `5000` | 首次获取配置超时 |

### ⚠️ 注意事项

1. **写回是全量覆盖**：写回的是该规则类型的**完整规则列表**。生产共享 data-id（如 `zujuan-flow-config` / `zujuan-degrade-config`）会被整体覆盖 —— 联调验证请使用**测试专用 data-id**。
2. **写回是尽力而为**：命令中心侧为 fire-and-forget（`Task.Run`），失败仅告警、内存规则仍生效；若推送后该实例立即退出，写回可能丢失（内存规则不回滚，重启后回退到 Nacos 旧内容）。
3. **数据源故障不阻断启动**：监听注册 / 拉取失败指数退避重试，应用先起来，Nacos 恢复后规则自动就位。
4. **转换器必须线程安全**：同一 `ISentinelRuleConverter` 实例在订阅线程（`Convert`）与写回 `Task.Run`（`Serialize`）间共享。
5. **`rule-type` 与 `data-id` 必填**，缺失时该数据源创建失败并告警（pass-through，不影响其余数据源）。

### 对外请求（全部指向 Nacos）

| 接口 | 触发点 | 作用 |
|---|---|---|
| `GET /nacos/v1/console/health/readiness` | 启动预检（独立 HttpClient，5s 超时） | 区分「Nacos 宕机」与「配置清空」 |
| `GET /nacos/v1/cs/configs`（SDK `GetConfig`） | 首次加载 | 拉取指定 dataId / group / tenant 配置 |
| `POST /nacos/v1/cs/configs/listener`（SDK 长轮询） | 常驻订阅 | 配置变更即时热更新 |
| `POST /nacos/v1/auth/users/login` 或 `/nacos/v1/auth/login` | 配置账号时，写回前 | 登录取 accessToken |
| `GET /nacos/v1/cs/configs?show=all`（带 token） | 写回前 | 读回当前配置 desc |
| `POST /nacos/v1/cs/configs`（原始 HTTP，form：dataId / group / tenant / content / type / desc / accessToken） | 写回主体 | 发布配置，保留 desc + 显式 type |
| `POST /nacos/v1/cs/configs`（SDK 4 参 `PublishConfig`） | 原始发布失败回退 / `preserve-desc=false` | 保证内容能发布（desc 被服务端清空） |

### 📚 文档

1. 新增包内文档 `Yzl.Extensions.Sentinel.DataSource.Nacos.md`（定位、架构图、快速开始、配置模型、核心类、拉取 / 监听流程、写回流程、对外接口、注意事项）。

### 依赖

1. 依赖 `Yzl.Extensions.Sentinel`（核心库，源码 / NuGet 双模式条件引用）。
2. 依赖 `nacos-sdk-csharp`。
