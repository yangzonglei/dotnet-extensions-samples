#!/usr/bin/env bash
# ==========================================
# Yzl.Extensions.Actuator 自动化测试
# ==========================================
#
# 覆盖两类内容：
#   A. 两种端口模式（共享端口 / 独立管理端口）
#   B. [ConfigurationProperties] 配置绑定
#        ManagementServerOptions            ← management:server
#        ManagementWebEndpointsOptions      ← management:endpoints:web
#        ManagementExposureOptions          ← management:endpoints:web:exposure
#        ManagementEndpointHttptraceOptions ← management:endpoint:httptrace
#        ManagementEndpointShutdownOptions  ← management:endpoint:shutdown
#
# 【前置条件】
#   .NET 8.0 SDK、curl 已安装；端口 16601 / 26601 未被占用
#   首次运行会构建并还原 NuGet 包，耗时较长
#
# 【用法】
#   chmod +x src/Yzl.Extensions.Samples.Actuator/test.sh
#   ./src/Yzl.Extensions.Samples.Actuator/test.sh
# ==========================================
set -uo pipefail

cd "$(dirname "$0")/../.."

PROJECT="src/Yzl.Extensions.Samples.Actuator"
HOST="http://localhost:16601"
MGMT="http://localhost:26601"
LOG="/tmp/actuator-test.log"
PASS=0
FAIL=0

RED='\033[0;31m'; GREEN='\033[0;32m'; NC='\033[0m'

cleanup() {
    lsof -ti:16601 2>/dev/null | xargs kill -9 2>/dev/null
    lsof -ti:26601 2>/dev/null | xargs kill -9 2>/dev/null
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
post_code() { curl -s -o /dev/null -w '%{http_code}' --max-time 10 -X POST "$1" 2>/dev/null; }

# 轮询等待端口就绪，最长 $2 秒
wait_http() {
    local url="$1" timeout="${2:-60}" i=0
    while [ "$i" -lt "$timeout" ]; do
        [ "$(code "$url")" = "200" ] && return 0
        sleep 1
        i=$((i+1))
    done
    return 1
}

echo "=========================================="
echo "  Actuator 自动化测试"
echo "=========================================="

# ============================================================
# 测试 1：共享端口模式（无管理端口）
# ============================================================
echo ""
echo "===== 测试 1：共享端口模式 ====="
cleanup
ASPNETCORE_ENVIRONMENT=Development dotnet run --project "$PROJECT" \
    --management:server:port="" > "$LOG" 2>&1 &
if ! wait_http "$HOST/actuator/health" 90; then
    echo -e "  ${RED}❌ 服务启动失败，日志尾部：${NC}"
    tail -20 "$LOG"
    FAIL=$((FAIL+1))
else
    assert_eq "Health"          "$(code $HOST/actuator/health)" "200"
    assert_eq "Metrics {name}"  "$(code $HOST/actuator/metrics/process.cpu.usage)" "200"
    assert_eq "Loggers {name}"  "$(code $HOST/actuator/loggers/Root)" "200"
    assert_eq "Info"            "$(code $HOST/actuator/info)" "200"
    assert_eq "Root"            "$(code $HOST/actuator)" "200"
    assert_eq "Home"            "$(code $HOST/)" "200"

    echo "  --- 配置绑定 ---"
    META=$(curl -s --max-time 10 "$HOST/actuator/metadata")
    assert_contains "Metadata basePath=/actuator" "$META" '"basePath":"/actuator"'
    # 未配置 management:server:port 时 Port 为 null，metadata 中不应出现 port
    assert_not_contains "Metadata 无 port（未配置管理端口）" "$META" '"port"'

    ROOT=$(curl -s --max-time 10 "$HOST/actuator")
    assert_contains "HAL links 含 self" "$ROOT" '"self"'
    assert_contains "Exposure include: health" "$ROOT" '"health"'
    assert_contains "Exposure include: beans" "$ROOT" '"beans"'
    # Development 配置把 shutdown 加入 include
    assert_contains "Exposure include: shutdown" "$ROOT" '"shutdown"'

    assert_contains "HttpTrace 返回 traces" \
        "$(curl -s --max-time 10 "$HOST/actuator/httptrace")" '"traces"'

    # 共享端口模式下不存在独立 Kestrel
    assert_not_contains "无独立管理 Kestrel" "$(cat "$LOG")" 'stopped gracefully'

    # shutdown 放在最后：POST 200 后服务应停止
    assert_eq "Shutdown 返回 200" "$(post_code "$HOST/actuator/shutdown")" "200"
    sleep 3
    assert_eq "Shutdown 后服务已停止" "$(code $HOST/actuator/health)" "000"
fi
cleanup

# ============================================================
# 测试 2：独立管理端口模式
# ============================================================
echo ""
echo "===== 测试 2：独立管理端口模式 ====="
cleanup
ASPNETCORE_ENVIRONMENT=Development dotnet run --project "$PROJECT" \
    --management:server:port=26601 > "$LOG" 2>&1 &
if ! wait_http "$MGMT/actuator/health" 90; then
    echo -e "  ${RED}❌ 服务启动失败，日志尾部：${NC}"
    tail -20 "$LOG"
    FAIL=$((FAIL+1))
else
    assert_eq "Mgmt Health"      "$(code $MGMT/actuator/health)" "200"
    assert_eq "Mgmt Metrics"     "$(code $MGMT/actuator/metrics/process.cpu.usage)" "200"
    assert_eq "Mgmt Loggers"     "$(code $MGMT/actuator/loggers/Root)" "200"
    assert_eq "Mgmt Root"        "$(code $MGMT/actuator)" "200"
    assert_eq "App Home"         "$(code $HOST/)" "200"
    assert_eq "App No Actuator"  "$(code $HOST/actuator/health)" "404"

    echo "  --- 配置绑定 ---"
    META=$(curl -s --max-time 10 "$MGMT/actuator/metadata")
    assert_contains "Metadata port=26601" "$META" '"port":26601'
    assert_contains "Metadata basePath=/actuator" "$META" '"basePath":"/actuator"'

    ROOT=$(curl -s --max-time 10 "$MGMT/actuator")
    assert_contains "HAL links 含 self" "$ROOT" '"self"'
    assert_contains "Exposure include: health" "$ROOT" '"health"'
    assert_contains "Exposure include: shutdown" "$ROOT" '"shutdown"'

    assert_contains "HttpTrace 返回 traces" \
        "$(curl -s --max-time 10 "$MGMT/actuator/httptrace")" '"traces"'

    assert_eq "Shutdown 返回 200" "$(post_code "$MGMT/actuator/shutdown")" "200"
    sleep 3
    assert_eq "Shutdown 后服务已停止" "$(code $MGMT/actuator/health)" "000"

    # 独立端口模式下 ActuatorKestrelServer 在停止时打印优雅停止日志（须在 shutdown 之后断言）
    assert_contains "独立 Kestrel 优雅停止日志" "$(cat "$LOG")" 'stopped gracefully'
fi
cleanup

# ============================================================
echo ""
echo "=========================================="
echo -e "  结果: ${GREEN}${PASS} passed${NC}, ${RED}${FAIL} failed${NC}"
echo "=========================================="
[ "$FAIL" -eq 0 ] && echo "🎉 全部通过!" || echo "😢 有失败用例"

exit "$FAIL"
