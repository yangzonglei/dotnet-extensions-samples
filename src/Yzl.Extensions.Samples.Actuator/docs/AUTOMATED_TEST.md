# Actuator 自动化测试手册

本手册验证 `Yzl.Extensions.Actuator` 的**两种端口模式**，二者由配置项 `management:server:port` 决定：

| 模式 | 触发条件 | 行为 |
|------|---------|------|
| **共享端口模式** | 未配置 `management:server:port` | 需在 `Program.cs` 手动调用 `UseSpringNetActuatorMapEndpoints()`，`/actuator/*` 挂在业务端口 |
| **独立端口模式** | 配置了 `management:server:port` | `AddSpringNetActuator` 注册 `ActuatorHostedService` → `ActuatorKestrelServer` 自动拉起独立 Kestrel；业务端口上**不再暴露** `/actuator`；此时 `UseSpringNetActuatorMapEndpoints()` 自动变为 no-op |

> 本示例的两种模式端口分配：业务端口 **16601**，独立管理端口 **26601**。

---

## 准备工作

```bash
cd /Users/yangzonglei/Documents/code/github.com/dotnet/dotnet-extensions-samples
```

确保没有残留进程：

```bash
lsof -ti:16601 | xargs kill -9 2>/dev/null
lsof -ti:26601 | xargs kill -9 2>/dev/null
```

---

## 测试 1：共享端口模式

端口：应用端口 `16601`，**无管理端口**。

### 启动

```bash
dotnet run --project src/Yzl.Extensions.Samples.Actuator \
  --management:server:port="" 2>/dev/null &
```

等待约 5 秒后执行测试。

### 测试用例

```bash
# 1. 静态路径 - 健康检查
curl -s -w "\nHTTP %{http_code}\n" http://localhost:16601/actuator/health

# 2. 参数化路径 - 具体指标 {name}
curl -s -w "\nHTTP %{http_code}\n" \
  http://localhost:16601/actuator/metrics/process.cpu.usage | head -c 100

# 3. 参数化路径 - Logger 配置 {loggerName}
curl -s -w "\nHTTP %{http_code}\n" \
  http://localhost:16601/actuator/loggers/Root | head -c 200

# 4. 静态路径 - 信息端点
curl -s -w "\nHTTP %{http_code}\n" http://localhost:16601/actuator/info | head -c 100

# 5. 根端点 - HAL 链接
curl -s -w "\nHTTP %{http_code}\n" http://localhost:16601/actuator | head -c 200

# 6. 首页（验证非 Actuator 路径正常）
curl -s -w "\nHTTP %{http_code}\n" http://localhost:16601/
```

### 预期结果

| # | 用例 | 期望 |
|---|------|------|
| 1 | `/actuator/health` | `200`，`status: UP`，details 含 `ping` / `diskSpace` / `customHealth` / `db` / `redis` |
| 2 | `/actuator/metrics/process.cpu.usage` | `200` |
| 3 | `/actuator/loggers/Root` | `200` |
| 4 | `/actuator/info` | `200` |
| 5 | `/actuator` | `200` |
| 6 | `/` | `200` |

### 停止

```bash
kill %1 2>/dev/null; wait 2>/dev/null
```

---

## 测试 2：独立端口模式

端口：应用 `16601` + 管理 `26601`。

### 启动

```bash
dotnet run --project src/Yzl.Extensions.Samples.Actuator \
  --management:server:port=26601 2>/dev/null &
```

> 也可以在 VS / VS Code 中直接选择启动配置 **`Yzl.Extensions.Samples.Actuator (独立管理端口)`**（见 `Properties/launchSettings.json`）。

### 测试用例

```bash
# 1. 管理端口 - 健康检查
curl -s -w "\nHTTP %{http_code}\n" http://localhost:26601/actuator/health

# 2. 管理端口 - 参数化 {name}
curl -s -w "\nHTTP %{http_code}\n" \
  http://localhost:26601/actuator/metrics/process.cpu.usage | head -c 100

# 3. 管理端口 - 参数化 {loggerName}
curl -s -w "\nHTTP %{http_code}\n" \
  http://localhost:26601/actuator/loggers/Root | head -c 200

# 4. 管理端口 - 根端点
curl -s -w "\nHTTP %{http_code}\n" http://localhost:26601/actuator | head -c 200

# 5. 应用端口 - 首页
curl -s -w "\nHTTP %{http_code}\n" http://localhost:16601/

# 6. 应用端口 - 不应有 Actuator 端点（独立端口隔离）
curl -s -w "\nHTTP %{http_code}\n" http://localhost:16601/actuator/health
```

### 预期结果

| # | 用例 | 期望 |
|---|------|------|
| 1 | `:26601/actuator/health` | `200` |
| 2 | `:26601/actuator/metrics/process.cpu.usage` | `200` |
| 3 | `:26601/actuator/loggers/Root` | `200` |
| 4 | `:26601/actuator` | `200` |
| 5 | `:16601/` | `200` |
| 6 | `:16601/actuator/health` | **`404`**（端口隔离生效） |

### 停止

```bash
kill %1 2>/dev/null; wait 2>/dev/null
```

---

## 一键自动化脚本

将以下内容保存为 `src/Yzl.Extensions.Samples.Actuator/test.sh`：

```bash
#!/bin/bash
set -e

cd "$(dirname "$0")/../.."
PROJECT="src/Yzl.Extensions.Samples.Actuator"
HOST="http://localhost:16601"
MGMT="http://localhost:26601"
PASS=0
FAIL=0

cleanup() {
  kill %1 2>/dev/null; wait 2>/dev/null
  lsof -ti:16601 | xargs kill -9 2>/dev/null
  lsof -ti:26601 | xargs kill -9 2>/dev/null
}

assert() {
  local desc="$1" actual="$2" expected="$3"
  if echo "$actual" | grep -q "$expected"; then
    echo "  ✅ $desc"
    PASS=$((PASS+1))
  else
    echo "  ❌ $desc (expected: $expected, got: $actual)"
    FAIL=$((FAIL+1))
  fi
}

# === 测试 1：共享端口模式 ===
cleanup
echo "===== 测试 1：共享端口模式 ====="
dotnet run --project "$PROJECT" --management:server:port="" 2>/dev/null &
sleep 5

assert "Health"          "$(curl -s -o /dev/null -w '%{http_code}' $HOST/actuator/health)" "200"
assert "Metrics {name}"  "$(curl -s -o /dev/null -w '%{http_code}' $HOST/actuator/metrics/process.cpu.usage)" "200"
assert "Loggers {name}"  "$(curl -s -o /dev/null -w '%{http_code}' $HOST/actuator/loggers/Root)" "200"
assert "Info"            "$(curl -s -o /dev/null -w '%{http_code}' $HOST/actuator/info)" "200"
assert "Root"            "$(curl -s -o /dev/null -w '%{http_code}' $HOST/actuator)" "200"
assert "Home"            "$(curl -s -o /dev/null -w '%{http_code}' $HOST/)" "200"

cleanup

# === 测试 2：独立端口模式 ===
echo "===== 测试 2：独立端口模式 ====="
dotnet run --project "$PROJECT" --management:server:port=26601 2>/dev/null &
sleep 5

assert "Mgmt Health"     "$(curl -s -o /dev/null -w '%{http_code}' $MGMT/actuator/health)" "200"
assert "Mgmt Metrics"    "$(curl -s -o /dev/null -w '%{http_code}' $MGMT/actuator/metrics/process.cpu.usage)" "200"
assert "Mgmt Loggers"    "$(curl -s -o /dev/null -w '%{http_code}' $MGMT/actuator/loggers/Root)" "200"
assert "Mgmt Root"       "$(curl -s -o /dev/null -w '%{http_code}' $MGMT/actuator)" "200"
assert "App Home"        "$(curl -s -o /dev/null -w '%{http_code}' $HOST/)" "200"
assert "App No Actuator" "$(curl -s -o /dev/null -w '%{http_code}' $HOST/actuator/health)" "404"

cleanup

echo "===== 结果: $PASS passed, $FAIL failed ====="
[ "$FAIL" -eq 0 ] && echo "🎉 全部通过!" || echo "😢 有失败用例"
```

### 运行

```bash
chmod +x src/Yzl.Extensions.Samples.Actuator/test.sh
./src/Yzl.Extensions.Samples.Actuator/test.sh
```

---

## VS Code REST Client (.http)

在 `src/Yzl.Extensions.Samples.Actuator/Yzl.Extensions.Samples.Actuator.http` 中追加以下内容：

```http
### ==========================================
### Actuator 测试 - 共享端口模式
### 启动: dotnet run --project src/Yzl.Extensions.Samples.Actuator --management:server:port=""
### ==========================================

### 1. 健康检查
GET http://localhost:16601/actuator/health

### 2. 参数化 {name} - CPU 使用率
GET http://localhost:16601/actuator/metrics/process.cpu.usage

### 3. 参数化 {loggerName} - 日志级别
GET http://localhost:16601/actuator/loggers/Root

### 4. 信息端点
GET http://localhost:16601/actuator/info

### 5. 根端点
GET http://localhost:16601/actuator

### 6. 首页
GET http://localhost:16601/

### ==========================================
### Actuator 测试 - 独立端口模式
### 启动: dotnet run --project src/Yzl.Extensions.Samples.Actuator --management:server:port=26601
### ==========================================

### 1. 管理端口健康检查
GET http://localhost:26601/actuator/health

### 2. 管理端口参数化 {name}
GET http://localhost:26601/actuator/metrics/process.cpu.usage

### 3. 管理端口参数化 {loggerName}
GET http://localhost:26601/actuator/loggers/Root

### 4. 管理端口根端点
GET http://localhost:26601/actuator

### 5. 应用端口首页
GET http://localhost:16601/

### 6. 应用端口/actuator/health（应 404）
GET http://localhost:16601/actuator/health
```

---

## 代理 Controller 的端口自适应

`Controllers/ActuatorEndpointsController.cs` 是仪表盘测试 `/actuator/*` 的桥梁（Actuator 的 Minimal API 路由未附加 `WithTest()` 元数据，Dashboard 无法自动发现）。它的基地址由 `ResolveActuatorBaseUrl` 解析，优先级：

1. `ActuatorTest:BaseUrl`（显式配置，便于指向远程实例）
2. `management:server:port`（独立端口模式下自动指向 `http://localhost:{port}`）
3. 回落 `http://localhost:16601`

因此**独立端口模式下无需改动任何代码**，`/api/actuator-test/health` 等代理端点会自动跟随到 `26601`：

```bash
# 独立端口模式下，业务端口上的代理端点仍可用，内部转发到 26601
curl -s http://localhost:16601/api/actuator-test/health
```

---

## 关键文件索引

| 文件 | 作用 |
|------|------|
| [Program.cs](../Program.cs) | 示例入口，显式调用 `UseSpringNetActuatorMapEndpoints` |
| [Properties/launchSettings.json](../Properties/launchSettings.json) | 两个启动配置（共享端口 / 独立管理端口 26601） |
| [Controllers/ActuatorEndpointsController.cs](../Controllers/ActuatorEndpointsController.cs) | 代理 Controller + `ResolveActuatorBaseUrl` 端口自适应 |
| [Controllers/CacheController.cs](../Controllers/CacheController.cs) | 业务侧 `IMemoryCache` 写入 → `/actuator/caches` 联动 |
| [Custom/CustomHealthContributor.cs](../Custom/CustomHealthContributor.cs) | `HealthComponents.Up(...)` 多项明细 |
| [Custom/DatabaseHealthContributor.cs](../Custom/DatabaseHealthContributor.cs) | `HealthComponents.Up(...)` + `Stopwatch` 耗时统计 |
| [Custom/RedisHealthContributor.cs](../Custom/RedisHealthContributor.cs) | 成功 `Up()` / 异常 `Down(ex)` 标准写法 |
| [Extensions/LifetimeExtensions.cs](../Extensions/LifetimeExtensions.cs) | 应用生命周期事件 + NLog 运行时规则注入 |
