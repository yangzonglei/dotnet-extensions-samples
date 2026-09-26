#!/usr/bin/env bash
# ==========================================
# Yzl.Extensions.SpringBoot.Admin.Client.Net 自动化测试
# ==========================================
#
# 覆盖两类内容：
#   A. SBA 客户端注册 / 心跳 / 反注册 端到端流程
#   B. [ConfigurationProperties] 配置绑定
#        SbaClientOptions                    ← spring:boot:admin:client
#        ManagementServerOptions             ← management:server
#        ManagementWebEndpointsOptions       ← management:endpoints:web
#        ManagementExposureOptions           ← management:endpoints:web:exposure
#        ApplicationIdOptions                ← spring:application
#        ManagementEndpointHttptraceOptions  ← management:endpoint:httptrace
#        ManagementEndpointShutdownOptions   ← management:endpoint:shutdown
#
# 【关于 SBA Server】
#   本脚本**不需要** Spring Boot Admin Server 在 16000 端口运行：
#     - SbaClientOptions.Url 的绑定通过客户端日志中的
#       "POST http://localhost:16000/instances" 断言（连接失败不影响该日志）
#     - 反注册通过客户端日志 "Successfully deregistered from Spring Boot Admin Server" 断言
#   若 16000 端口确有 SBA Server，注册与反注册会真实发生，断言同样成立。
#
# 【前置条件】
#   .NET 8.0 SDK、curl 已安装；端口 16606 / 26606 未被占用
#   首次运行会构建并还原 NuGet 包，耗时较长
#
# 【用法】
#   chmod +x src/Yzl.Extensions.Samples.SpringBoot.Admin.Net/test.sh
#   ./src/Yzl.Extensions.Samples.SpringBoot.Admin.Net/test.sh
# ==========================================
set -uo pipefail

cd "$(dirname "$0")/../.."

PROJECT="src/Yzl.Extensions.Samples.SpringBoot.Admin.Net"
HOST="http://localhost:16606"
MGMT="http://localhost:26606"
LOG="/tmp/sba-client-test.log"
PASS=0
FAIL=0

RED='\033[0;31m'; GREEN='\033[0;32m'; NC='\033[0m'

cleanup() {
    lsof -ti:16606 2>/dev/null | xargs kill -9 2>/dev/null
    lsof -ti:26606 2>/dev/null | xargs kill -9 2>/dev/null
    sleep 1
}
trap cleanup EXIT

assert_eq() {
    if [ "$2" = "$3" ]; then
        echo -e "  ${GREEN}✅ PASS${NC}: $1"
        PASS=$((PASS+1))
    else
        echo -e "  ${RED}❌ FAIL${NC}: $1 (expected: '$3', actual: '$2')"
        FAIL=$((FAIL+1))
    fi
}

assert_contains() {
    if echo "$2" | grep -q "$3"; then
        echo -e "  ${GREEN}✅ PASS${NC}: $1"
        PASS=$((PASS+1))
    else
        echo -e "  ${RED}❌ FAIL${NC}: $1 (expected to contain: '$3')"
        FAIL=$((FAIL+1))
    fi
}

assert_not_contains() {
    if echo "$2" | grep -q "$3"; then
        echo -e "  ${RED}❌ FAIL${NC}: $1 (should NOT contain: '$3')"
        FAIL=$((FAIL+1))
    else
        echo -e "  ${GREEN}✅ PASS${NC}: $1"
        PASS=$((PASS+1))
    fi
}

# 连接失败时 curl 自身会输出 000（退出码非 0），因此不能再追加 echo 000
code() { curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$1" 2>/dev/null; }
post_json_code() {
    curl -s -o /dev/null -w '%{http_code}' --max-time 10 -X POST \
        -H 'Content-Type: application/json' -d "$2" "$1" 2>/dev/null
}

wait_http() {
    local url="$1" timeout="${2:-90}" i=0
    while [ "$i" -lt "$timeout" ]; do
        [ "$(code "$url")" = "200" ] && return 0
        sleep 1
        i=$((i+1))
    done
    return 1
}

echo "=========================================="
echo "  SBA Client 自动化测试"
echo "=========================================="

cleanup
ASPNETCORE_ENVIRONMENT=Development dotnet run --project "$PROJECT" \
    --urls "http://localhost:16606" > "$LOG" 2>&1 &

if ! wait_http "$MGMT/actuator/health" 120; then
    echo -e "  ${RED}❌ 服务启动失败，日志尾部：${NC}"
    tail -20 "$LOG"
    exit 1
fi

# ------------------------------------------------------------
echo ""
echo "===== A. 端口模式与端点隔离 ====="
assert_eq "Mgmt Health"      "$(code $MGMT/actuator/health)" "200"
assert_eq "Mgmt Info"        "$(code $MGMT/actuator/info)" "200"
assert_eq "Mgmt Beans"       "$(code $MGMT/actuator/beans)" "200"
assert_eq "Mgmt Env"         "$(code $MGMT/actuator/env)" "200"
assert_eq "Mgmt Root"        "$(code $MGMT/actuator)" "200"
assert_eq "App Home"         "$(code $HOST/)" "200"
assert_eq "App No Actuator"  "$(code $HOST/actuator/health)" "404"

# ------------------------------------------------------------
echo ""
echo "===== B. ConfigurationProperties 绑定 ====="

echo "  [ManagementServerOptions] ← management:server"
META=$(curl -s --max-time 10 "$MGMT/actuator/metadata")
assert_contains "Port = 26606" "$META" '"port":26606'

echo "  [ManagementWebEndpointsOptions] ← management:endpoints:web"
assert_contains "BasePath = /actuator" "$META" '"basePath":"/actuator"'

echo "  [ApplicationIdOptions] ← spring:application"
assert_contains "Name = yzl-sba-client-samples" "$META" '"name":"yzl-sba-client-samples"'

echo "  [ManagementExposureOptions] ← management:endpoints:web:exposure"
ROOT=$(curl -s --max-time 10 "$MGMT/actuator")
assert_contains "HAL links 含 self" "$ROOT" '"self"'
assert_contains "Exposure include: health" "$ROOT" '"health"'
assert_contains "Exposure include: env" "$ROOT" '"env"'
assert_contains "Exposure include: httptrace" "$ROOT" '"httptrace"'
assert_not_contains "Exposure exclude: shutdown 未暴露" "$ROOT" '"shutdown"'

echo "  [ManagementEndpointHttptraceOptions] ← management:endpoint:httptrace"
assert_eq "HttpTrace enabled=true 端点可访问" "$(code $MGMT/actuator/httptrace)" "200"
assert_contains "HttpTrace 返回 traces" \
    "$(curl -s --max-time 10 "$MGMT/actuator/httptrace")" '"traces"'

echo "  [ManagementEndpointShutdownOptions] ← management:endpoint:shutdown"
# enabled=true 但被 exposure.exclude 排除 → 端点不可达
assert_eq "Shutdown 已排除（404）" "$(code $MGMT/actuator/shutdown)" "404"

# loggers 是 GET + POST 双路由，验证 POST/DELETE 可用
assert_eq "Loggers {name} 读取" "$(code $MGMT/actuator/loggers/Root)" "200"
assert_eq "Loggers POST 设置级别" \
    "$(post_json_code "$MGMT/actuator/loggers/Yzl.Extensions.Samples" '{"configuredLevel":"Information"}')" "204"

# ------------------------------------------------------------
echo ""
echo "===== C. SBA 注册 / 心跳 ====="

echo "  [SbaClientOptions] ← spring:boot:admin:client"
assert_contains "Url 绑定 → POST http://localhost:16000/instances" \
    "$(cat "$LOG")" 'POST http://localhost:16000/instances'
assert_contains "HttpClient 名为 spring-boot-admin" \
    "$(cat "$LOG")" 'HttpClient.spring-boot-admin'

# RefreshInterval = 00:00:15，等待第二次心跳
# 每次请求打印 "Sending HTTP request" 一次，故用该行计数
SBA_SERVER_UP=0
[ "$(code http://localhost:16000/)" != "000" ] && SBA_SERVER_UP=1

if [ "$SBA_SERVER_UP" -eq 1 ]; then
    echo "  等待第二次心跳（RefreshInterval=15s）..."
    sleep 20
    HEARTBEATS=$(grep -c 'Sending HTTP request POST http://localhost:16000/instances' "$LOG")
    if [ "$HEARTBEATS" -ge 2 ]; then
        echo -e "  ${GREEN}✅ PASS${NC}: RefreshInterval 生效（观察到 ${HEARTBEATS} 次注册/心跳）"
        PASS=$((PASS+1))
    else
        echo -e "  ${RED}❌ FAIL${NC}: RefreshInterval 未生效（仅 ${HEARTBEATS} 次注册/心跳）"
        FAIL=$((FAIL+1))
    fi
else
    # SBA Server 不可达时客户端会按指数退避重试，间隔不可观测；
    # 改为断言失败重试路径（退避告警）已被触发
    echo "  SBA Server (16000) 未运行，跳过 RefreshInterval 间隔断言"
    assert_contains "注册失败按指数退避重试" \
        "$(cat "$LOG")" 'Spring Boot Admin heartbeat/registration failed'
fi

# ------------------------------------------------------------
echo ""
echo "===== D. 优雅关闭 / 反注册 ====="

PID=$(lsof -ti:16606 2>/dev/null | head -1)
if [ -z "$PID" ]; then
    echo -e "  ${RED}❌ FAIL${NC}: 找不到业务进程 PID"
    FAIL=$((FAIL+1))
else
    kill -TERM "$PID" 2>/dev/null
    sleep 8
    assert_contains "AutoDeregistration 生效（已反注册）" \
        "$(cat "$LOG")" 'Successfully deregistered from Spring Boot Admin Server'
    assert_contains "独立 Kestrel 优雅停止" \
        "$(cat "$LOG")" 'Actuator Kestrel server stopped gracefully'
fi

cleanup

# ------------------------------------------------------------
echo ""
echo "=========================================="
echo -e "  结果: ${GREEN}${PASS} passed${NC}, ${RED}${FAIL} failed${NC}"
echo "=========================================="
[ "$FAIL" -eq 0 ] && echo "🎉 全部通过!" || echo "😢 有失败用例"

exit "$FAIL"
