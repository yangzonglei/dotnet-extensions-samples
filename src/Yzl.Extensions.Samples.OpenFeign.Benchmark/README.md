# Yzl.Extensions.Samples.OpenFeign.Benchmark

OpenFeign 性能对比测试工具。

## 用途

对 `Yzl.Extensions.Http.OpenFeign` 的不同版本进行性能压测，对比：
- 连接池行为（MaxConnectionsPerServer）
- 同步方法线程阻塞（HandleSyncResult）
- 超时场景下的重试和兜底
- 突发高并发下的吞吐

## 测试场景

| 场景 | 说明 |
|:----|:------|
| 场景 1 | Ping × 3000，并发 300 — 高频短请求 |
| 场景 2 | 混合 × 3000，并发 300 — Ping + GetUser |
| 场景 3 | Timeout × 200，并发 100 — 超时压力 |
| 场景 4 | Ping × 500，并发 500 — 短暂突发 |

## 使用方法

### 测试本地源码版（来自 ../dotnet-extensions）

```bash
dotnet run -p:UseProjectReference=true
```

### 测试 NuGet 版（默认 0.1.19）

```bash
# 当前版本（由 Directory.Packages.props 中的 Version 决定）
dotnet run

# 修改版本后运行
# 编辑 ../../Directory.Packages.props 中的 Yzl.Extensions.Http.OpenFeign Version
```

### 对比流程

```bash
# 1. 测试本地源码（修改版）
dotnet run -p:UseProjectReference=true | tee /tmp/benchmark-modified.log

# 2. 修改 Directory.Packages.props 中 Version 为 0.1.19
#    测试 NuGet 0.1.19（原始版）
dotnet run | tee /tmp/benchmark-019.log

# 3. 修改 Directory.Packages.props 中 Version 为 0.1.18
#    测试 NuGet 0.1.18（更早版本）
dotnet run | tee /tmp/benchmark-018.log

# 4. 对比
grep -E "版本:|耗时|吞吐|P50=|P95=|P99=|错误统计" /tmp/benchmark-*.log
```

## 测试配置

当前测试使用的 Feign 配置（硬编码在 Program.cs 中）：

```yaml
spring:
  feign:
    default:
      timeout: 3000
      syncTimeoutMs: 15000
      maxConcurrentRequests: 0        # 0 = 不限制
      retry:
        enabled: true
        maxAttempts: 2
        delayMs: 500
        maxDelay: 2000
    httpClient:
      handlerLifetime: "00:30:00"
      pool:
        maxConnectionsPerServer: 300
```

## 输出指标

- 总请求数 / 成功 / 失败
- 延迟 P50 / P95 / P99
- ThreadPool 工作线程峰值
- 错误类型分布（如有）
- 错误消息采样（如有）
