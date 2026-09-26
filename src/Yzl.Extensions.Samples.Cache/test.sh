#!/usr/bin/env bash
# ==========================================
# Yzl.Extensions.Cache 自动化测试
# ==========================================
#
# 通过各章节端点的 elapsedMs 判定缓存是否命中：
#   命中 → elapsedMs ≈ 0（<= 50ms）
#   未命中 → 真实执行业务方法（>= 1000ms）
#
# 【前置条件】
#   .NET 8.0 SDK、curl 已安装；端口 16605 未被占用
#   首次运行会构建并还原 NuGet 包，耗时较长
#   未配置 redis:main-site 时仅内存缓存，第八章端点不可用（本脚本不覆盖）
#
# 【用法】
#   chmod +x src/Yzl.Extensions.Samples.Cache/test.sh
#   ./src/Yzl.Extensions.Samples.Cache/test.sh
# ==========================================
set -uo pipefail

cd "$(dirname "$0")/../.."

PROJECT="src/Yzl.Extensions.Samples.Cache"
HOST="http://localhost:16605/api/samples"
LOG="/tmp/cache-test.log"
PASS=0
FAIL=0

RED='\033[0;31m'; GREEN='\033[0;32m'; NC='\033[0m'

cleanup() {
    lsof -ti:16605 2>/dev/null | xargs kill -9 2>/dev/null
    sleep 1
}
trap cleanup EXIT

# $1=描述 $2=实际耗时(ms) $3=判定：hit | miss
assert_timing() {
    local desc="$1" actual="$2" kind="$3" ok=0
    if [ -z "$actual" ]; then
        echo -e "  ${RED}❌ FAIL${NC}: $desc (无响应/无法解析 elapsedMs)"
        FAIL=$((FAIL+1))
        return
    fi
    if [ "$kind" = "hit" ]; then
        [ "$actual" -le 50 ] && ok=1
    else
        [ "$actual" -ge 1000 ] && ok=1
    fi
    if [ "$ok" -eq 1 ]; then
        echo -e "  ${GREEN}✅ PASS${NC}: $desc (${actual}ms)"
        PASS=$((PASS+1))
    else
        echo -e "  ${RED}❌ FAIL${NC}: $desc (${actual}ms, 期望 $kind)"
        FAIL=$((FAIL+1))
    fi
}

# 从 JSON 响应中提取 elapsedMs
ms()      { curl -s --max-time 15 "$1" | sed -n 's/.*"elapsedMs":\([0-9]*\).*/\1/p'; }
ms_post() { curl -s --max-time 15 -X POST "$1" -d "$2" | sed -n 's/.*"elapsedMs":\([0-9]*\).*/\1/p'; }
# 仅发请求不关心响应（用于 evict 之类的动作）
fire_post() { curl -s --max-time 15 -X POST "$1" -d "$2" > /dev/null; }

echo "=========================================="
echo "  Cache 自动化测试"
echo "=========================================="

cleanup
dotnet run --project "$PROJECT" > "$LOG" 2>&1 &

# 轮询等待就绪（最长 90 秒）
# ⚠️ 不能用 /basic/1 探测：那会预热缓存，导致「首次未命中」用例失败
ready=0
for i in $(seq 1 90); do
    if curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://localhost:16605/" 2>/dev/null | grep -q '^200$'; then
        ready=1
        break
    fi
    sleep 1
done

if [ "$ready" -ne 1 ]; then
    echo -e "  ${RED}❌ 服务启动失败，日志尾部：${NC}"
    tail -20 "$LOG"
    exit 1
fi

echo ""
echo "===== 第一章：基础 Cacheable ====="
assert_timing "1.1 首次未命中" "$(ms $HOST/basic/1)" miss
assert_timing "1.1 二次命中"   "$(ms $HOST/basic/1)" hit

echo ""
echo "===== 第二章：CachePut / CacheEvict ====="
assert_timing "2.1 首次未命中" "$(ms $HOST/lifecycle/1)" miss
assert_timing "2.1 二次命中"   "$(ms $HOST/lifecycle/1)" hit
# 2.4 refresh 是 CachePut，两次都应真实执行
assert_timing "2.4 CachePut 第 1 次仍执行" "$(ms $HOST/lifecycle/refresh/1)" miss
assert_timing "2.4 CachePut 第 2 次仍执行" "$(ms $HOST/lifecycle/refresh/1)" miss
# 2.3 CacheEvict 后 2.1 应重新执行
fire_post "$HOST/lifecycle/delete" "id=1"
assert_timing "2.3 Evict 后重新加载" "$(ms $HOST/lifecycle/1)" miss

echo ""
echo "===== 第四章：Condition / 拼写错误 ====="
# 种子数据只有 id=1/2/5/99；99 满足 #id > 10
assert_timing "4.1 id=99 首次未命中" "$(ms $HOST/condition/cacheable/99)" miss
assert_timing "4.1 id=99 二次命中"   "$(ms $HOST/condition/cacheable/99)" hit
assert_timing "4.1 id=5 永不缓存(1)" "$(ms $HOST/condition/cacheable/5)"  miss
assert_timing "4.1 id=5 永不缓存(2)" "$(ms $HOST/condition/cacheable/5)"  miss
assert_timing "4.8 typo-condition"   "$(ms $HOST/condition/typo-condition/1)" miss
assert_timing "4.9 typo-unless"      "$(ms $HOST/condition/typo-unless/1)"    miss

echo ""
echo "===== 第六章：异步缓存 ====="
assert_timing "6.1 首次未命中" "$(ms $HOST/async/1)" miss
assert_timing "6.1 二次命中"   "$(ms $HOST/async/1)" hit

echo ""
echo "===== 第九章：CacheEvict ====="
assert_timing "9.1 首次未命中" "$(ms $HOST/evict-all/1)" miss
assert_timing "9.1 二次命中"   "$(ms $HOST/evict-all/1)" hit
# 9.2 逐条清除对内存提供器有效
fire_post "$HOST/evict-all/evict-single/1" ""
assert_timing "9.2 逐条清除后重新加载" "$(ms $HOST/evict-all/1)" miss
# 9.3 allEntries 在 MemoryCacheProvider 上是空操作（RemoveByPrefixAsync 未实现）
fire_post "$HOST/evict-all/clear-all" ""
assert_timing "9.3 allEntries 内存下为空操作(仍命中)" "$(ms $HOST/evict-all/1)" hit

cleanup

echo ""
echo "=========================================="
echo -e "  结果: ${GREEN}${PASS} passed${NC}, ${RED}${FAIL} failed${NC}"
echo "=========================================="
[ "$FAIL" -eq 0 ] && echo "🎉 全部通过!" || echo "😢 有失败用例"

exit "$FAIL"
