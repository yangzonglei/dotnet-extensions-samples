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
#
#   ── Redis 相关章节的共同前提 ──
#   第八章与第十章的服务端后端都是 Redis，连接来自两处配置：
#     ① appsettings.json 的 spring:data:redis 节（按连接名嵌套，见第十章）
#     ② appsettings.Development.json 的扁平连接串键 redis:main-site / redis:cas-auth
#   两处都没配、或 Redis 不可达时【不会 500】，而是全部静默不命中（elapsedMs 恒为真实方法耗时
#   ~1500；实例不可达时还会叠加 connect-timeout × connect-retry 的重试时间，量级仍是秒）：
#   读路径的异常被 CacheableHandler.TryReadCache 的 catch(Exception) 吞掉、
#   写路径被 CacheOperationHelper.WriteCache 的同类 catch 吞掉。
#   所以本脚本里 Redis 章节在 Redis 未就绪时会表现为「未命中用例通过、命中用例失败」，
#   而不是报错退出 —— 看到命中用例失败先确认 Redis 是否真的在跑。
#
#   第八章 8.6 复用 redis:main-site，只是用 DatabaseIndex 换到 db 1 / db 2，无需新配置
#   第八章 8.6 的 id 取 1000~1999（服务端只在这个区间造数据），并用「时间戳取模」挑一个
#   本次运行专属的值：固定 id 会撞上上一轮残留在 db 1 / db 2 的 svc:default:{id}
#   （TTL 300 秒），让「首次未命中」假性失败。区间外虽同样会缓存（v0.2.0 起默认缓存 null），
#   但用区间内 id 才能同时验证「真实值」的命中路径。
#   第八章 8.5 另需 redis:cas-auth（第二个 Redis 后端）；只缺它时 8.1~8.4 不受影响，
#   8.5 的 cas-auth 那一路缓存静默失效（elapsedMs 恒为 ~1500），不会报错
#   （注意：连接【配置缺失】与连接【不可达】在时效上不同 —— 缺失是纯 ~1500，
#     不可达还要等 connect-timeout × connect-retry，实测秒级）
#
#   ── 第十章（Redis 连接：配置 / 注入）──
#   10.1 / 10.2 走 appsettings.json 的 spring:data:redis，样例配置指向【本地 6399】
#   （端口在各连接名下用 kebab-case 显式写死：host/port）。因此本脚本给第十章加了
#   「6399 是否可连」的前置探测：连不上就整章 SKIP，不会因为「未命中用例通过」而误报绿灯。
#   10.1/10.2 的 id 取 2000~2999（与 8.6 的 1000~1999 错开；键分别是 conn:config:users:*
#   与 conn:injected:users:*，落在 db 5 / db 8，与其他章节的键空间互不干扰）。
#   10.5（连接层高可用）【刻意不在本脚本里】—— 它要 kill 掉 Redis 再拉起来，
#   属于破坏性操作，请按 TestManual.md 第十章 10.5 手工执行。
#
#   第十一章需要 Samples.Api（16600）在运行；未启动时该章自动跳过
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

# $1=描述 $2=实际耗时(ms) $3=判定：hit | miss $4=命中阈值上限(ms，默认 50)
# 第八章走 Redis 的方法每次命中都有一次网络往返（实测 50~150ms），所以要用第 4 个参数放宽阈值。
assert_timing() {
    local desc="$1" actual="$2" kind="$3" hit_max="${4:-50}" ok=0
    if [ -z "$actual" ]; then
        echo -e "  ${RED}❌ FAIL${NC}: $desc (无响应/无法解析 elapsedMs)"
        FAIL=$((FAIL+1))
        return
    fi
    if [ "$kind" = "hit" ]; then
        [ "$actual" -le "$hit_max" ] && ok=1
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

# 本地可丢弃 Redis（6399）是否可连 —— 第十章 10.1/10.2 的连接就指向它。
# 优先用 redis-cli（Homebrew 装在 /opt/homebrew/bin，通常已在 PATH 里），
# 没有就退化成 TCP 探测，避免把「没装 redis-cli」误判成「Redis 没跑」。
redis_6399_ready() {
    local cli=""
    if command -v redis-cli >/dev/null 2>&1; then
        cli="redis-cli"
    elif [ -x /opt/homebrew/bin/redis-cli ]; then
        cli="/opt/homebrew/bin/redis-cli"
    fi
    if [ -n "$cli" ]; then
        "$cli" -p 6399 ping 2>/dev/null | grep -q PONG && return 0
        return 1
    fi
    (exec 3<>/dev/tcp/127.0.0.1/6399) 2>/dev/null
}

# 通用布尔断言：$1=描述 $2=条件结果（0=通过）
assert_true() {
    local desc="$1" rc="$2"
    if [ "$rc" -eq 0 ]; then
        echo -e "  ${GREEN}✅ PASS${NC}: $desc"
        PASS=$((PASS+1))
    else
        echo -e "  ${RED}❌ FAIL${NC}: $desc"
        FAIL=$((FAIL+1))
    fi
}

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
echo "===== 第三章：SpEL 枚举参数 ====="
# 服务每次由脚本全新拉起，进程内缓存为空，固定 userId 不会让「首次未命中」假性失败
assert_timing "3.5 Normal 首次未命中" "$(ms $HOST/spel/benefit/1/Normal)" miss
assert_timing "3.5 Normal 二次命中"   "$(ms $HOST/spel/benefit/1/Normal)" hit
# 同 userId 换枚举值 → 不同缓存键 → 必须重新执行
assert_timing "3.5 换枚举值 Gold 未命中(不同键)" "$(ms $HOST/spel/benefit/1/Gold)" miss
assert_timing "3.5 Gold 二次命中"                "$(ms $HOST/spel/benefit/1/Gold)" hit
# 回到 Normal：各枚举值互不干扰
assert_timing "3.5 回 Normal 仍命中(未被 Gold 冲掉)" "$(ms $HOST/spel/benefit/1/Normal)" hit

echo ""
echo "===== 第六章：异步缓存 ====="
# 种子数据：100/200/300。
# 6.1 的 [Cacheable] 没有 unless，v0.2.0 起 null 也照常写入 —— 用不存在的 id（如 /async/1）
# 同样能命中（第二次 ≈ 0ms）。6.4 的 [Cacheable(unless = "#result == null")] 才是 opt-out，
# 对同一个不存在的 id 每次都真实执行。两条用例合起来正好钉住这一对语义。
assert_timing "6.1 首次未命中" "$(ms $HOST/async/100)" miss
assert_timing "6.1 二次命中"   "$(ms $HOST/async/100)" hit
# 6.4 对照组：unless = "#result == null" 是逐方法 opt-out —— 不存在的 id 每次真实执行
assert_timing "6.4 unless 排除 null: 第 1 次执行" "$(ms $HOST/async/hot/1)" miss
assert_timing "6.4 unless 排除 null: 第 2 次仍执行" "$(ms $HOST/async/hot/1)" miss
# 6.1 的对照半场：同一个不存在的 id、无 unless → v0.2.0 起 null 照常写入，第二次命中
assert_timing "6.1 null 照常缓存: 第 1 次执行" "$(ms $HOST/async/777)" miss
assert_timing "6.1 null 照常缓存: 第 2 次命中" "$(ms $HOST/async/777)" hit

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
# 9.4 Redis 后端的 allEntries 是「真删」（SCAN prefix* + DEL），与 9.3 的空操作形成对照。
# 键落在 redis:main-site 的 db 11，前缀 redis:evict-all，不会牵动其他章节。
EVICT_REDIS_ID=$(( 2000 + $(date +%s) % 1000 ))
assert_timing "9.4 首次写入 Redis 未命中" "$(ms $HOST/evict-all/redis/$EVICT_REDIS_ID)" miss
assert_timing "9.4 二次命中(redis 往返)"   "$(ms $HOST/evict-all/redis/$EVICT_REDIS_ID)" hit 200
fire_post "$HOST/evict-all/clear-all-redis" ""
assert_timing "9.4 Redis allEntries 后重新加载(真删)" "$(ms $HOST/evict-all/redis/$EVICT_REDIS_ID)" miss

echo ""
echo "===== 第八章 8.6：Service 级默认后端 + 方法级覆盖 ====="
# 三个方法只有「写没写 cacheManager」不同：
#   A 继承类级 [CacheConfig] → 自定义 Db1RedisManager（db 1）
#   B 方法级覆盖            → Db2RedisManager（db 2）
#   C 方法级覆盖            → MemoryCacheManager（进程内）
# ⚠️ 三个方法挤在同一个 JSON 里，没有各自的 URL，只能一次请求后按出现顺序取 elapsedMs。
# ⚠️ id 落在服务端造数据的区间 1000~1999 内，才能验证「真实值」的命中路径。
#    区间外返回 null，v0.2.0 起同样会被缓存（第二次也命中），不影响本用例但语义不同。
# ⚠️ 同时用「时间戳取模」挑一个本次运行专属的 id：A/B 是 Redis 缓存（TTL 300 秒），
#    上一轮跑脚本留下的键还在，固定 id 会让「首次未命中」假性失败。
#    （秒级时间戳模 1000 的回绕周期是 1000 秒 > 300 秒 TTL，不会撞上残留。）
DB6_ID=$(( 1000 + $(date +%s) % 1000 ))
DB6=""
db6() {
    DB6=$(curl -s --max-time 20 "$HOST/redis/default-backend/$1" \
      | grep -o '"elapsedMs":[0-9]*' | awk -F: '{print $2}' | tr '\n' ' ')
}

db6 "$DB6_ID"
# 首次：三条路径都应真实执行（A 与 B 同名键但不同库，互不可见）
assert_timing "8.6 首次 A 未命中(继承 → db 1)" "$(awk '{print $1}' <<< "$DB6")" miss
assert_timing "8.6 首次 B 未命中(覆盖 → db 2)" "$(awk '{print $2}' <<< "$DB6")" miss
assert_timing "8.6 首次 C 未命中(覆盖 → 内存)" "$(awk '{print $3}' <<< "$DB6")" miss

db6 "$DB6_ID"
# 二次：A/B 走 Redis，命中也要一次网络往返（实测 50~150ms），阈值放宽到 200ms；
# C 是进程内缓存，命中几乎为 0，仍用默认的 50ms。
assert_timing "8.6 二次 A 命中(db 1)" "$(awk '{print $1}' <<< "$DB6")" hit 200
assert_timing "8.6 二次 B 命中(db 2)" "$(awk '{print $2}' <<< "$DB6")" hit 200
assert_timing "8.6 二次 C 命中(内存)" "$(awk '{print $3}' <<< "$DB6")" hit
# 8.6 空结果（区间外 id）也照常落缓存：v0.2.0 语义反转的直接证据。
# 用第 2 个参数直接把 id 传给 service（绕过 8.6 的「时间戳取模」），
# A 走 Redis(db1) / B 走 Redis(db2) 第二次都应命中；C 是进程内缓存同理。
NULL_ID=7
db6 "$NULL_ID"
assert_timing "8.6 空结果首次 A 未命中" "$(awk '{print $1}' <<< "$DB6")" miss
assert_timing "8.6 空结果首次 B 未命中" "$(awk '{print $2}' <<< "$DB6")" miss
db6 "$NULL_ID"
assert_timing "8.6 空结果二次 A 命中(null 已写 Redis)" "$(awk '{print $1}' <<< "$DB6")" hit 200
assert_timing "8.6 空结果二次 B 命中(null 已写 Redis)" "$(awk '{print $2}' <<< "$DB6")" hit 200
assert_timing "8.6 空结果二次 C 命中(null 已写内存)"   "$(awk '{print $3}' <<< "$DB6")" hit

echo ""
echo "===== 第十章：Redis 连接（配置 / 用户注入）====="
# 10.1/10.2 的连接参数全部来自 appsettings.json 的 spring:data:redis 节，
# 样例里指向本地可丢弃实例 6399。连不上就整章 SKIP：
# 否则「未命中用例」会因为缓存静默失效而全部假性通过，给出误导性的绿灯。
# （10.5 连接层高可用要 kill/重启 Redis，属破坏性操作，不在本脚本覆盖范围内 —— 见手册 10.5。）
if redis_6399_ready; then
    # id 落在 2000~2999（服务端只在这个区间造数据），并用时间戳取模避开上一轮残留：
    # 键是 conn:config:users:*（db 5）与 conn:injected:users:*（db 8），TTL 300 秒。
    CONN_ID=$(( 2000 + $(date +%s) % 1000 ))

    # 10.1 连接来自配置节（ConfigBackendRedisManager 只重写 ConnectionStringKey）
    assert_timing "10.1 配置节连接首次未命中" "$(ms $HOST/connection/config/$CONN_ID)" miss
    assert_timing "10.1 配置节连接二次命中"   "$(ms $HOST/connection/config/$CONN_ID)" hit 200

    # 10.2 连接由用户注入（IRedisConnectionSource 认领连接名，配置里的同名节被跳过）
    assert_timing "10.2 用户注入连接首次未命中" "$(ms $HOST/connection/injected/$CONN_ID)" miss
    assert_timing "10.2 用户注入连接二次命中"   "$(ms $HOST/connection/injected/$CONN_ID)" hit 200

    # 10.2/10.3 的关键差异证据：配置里同一个连接名写的是 database 7，
    # 实际落到 8（因为「用户来源优先于配置」）；两个字段不一致才是预期的。
    CONN_JSON=$(curl -s --max-time 15 "$HOST/connection/injected/$CONN_ID")
    assert_true "10.2 用户连接落 db 8（配置里写 7 被跳过）" \
        "$(grep -q '"actualDatabase":8' <<< "$CONN_JSON" && grep -q '"configuredDatabaseForSameName":7' <<< "$CONN_JSON" && echo 0 || echo 1)"

    # 10.3 「同一个连接名只建一次」：访问过 10.2 之后计数仍是 1
    CONNECT_COUNT=$(curl -s --max-time 15 "$HOST/connection/injected-connection-count" \
        | grep -o '"connectCount":[0-9]*' | awk -F: '{print $2}')
    assert_true "10.3 用户连接只创建一次 (connectCount=${CONNECT_COUNT:-?})" \
        "$([ "${CONNECT_COUNT:-0}" -eq 1 ] && echo 0 || echo 1)"

    # 10.4 「连接名里的冒号 = 配置层级」这个坑：按字典全名取不到，按配置节路径才读得到
    OPT_JSON=$(curl -s --max-time 15 "$HOST/connection/options")
    assert_true "10.4 字典按全名取连接失败（冒号被当层级）" \
        "$(grep -q '取不到（这就是陷阱本身）' <<< "$OPT_JSON" && echo 0 || echo 1)"
    assert_true "10.4 按配置节路径能读到真实值（port 6399 / db 5）" \
        "$(grep -q '"port":6399' <<< "$OPT_JSON" && grep -q '"database":5' <<< "$OPT_JSON" && echo 0 || echo 1)"

    # 10.6 新旧连接名共存：第八章的扁平键 + 第十章的配置节连接名
    NAMES_JSON=$(curl -s --max-time 15 "$HOST/connection/names")
    assert_true "10.6 连接名一览含新旧两类（redis:main-site 与 redis:samples-config）" \
        "$(grep -q 'redis:main-site' <<< "$NAMES_JSON" && grep -q 'redis:samples-config' <<< "$NAMES_JSON" && echo 0 || echo 1)"
else
    echo -e "  ⏭ SKIP: 本地 Redis (6399) 未就绪 —— 第十章 10.1/10.2 的连接指向它"
    echo "         启动：/opt/homebrew/bin/redis-server --port 6399 --daemonize yes"
fi

echo ""
echo "===== 第十一章：OpenFeign + Cacheable ====="
# 需要下游 Samples.Api（16600）。未启动时跳过，不影响其余用例。
if curl -s -o /dev/null --max-time 3 "http://localhost:16600/api/test/ping"; then
    # 用时间戳做 id：feign:users 区域是进程内内存缓存（注解未指定 cacheManager），
    # 同一进程内重复跑脚本时固定 id 会让「首次未命中」假性失败
    FEIGN_ID=$(date +%s)

    # 11.2 对照组：无缓存的远程调用。耗时只有几十毫秒，
    # 无法用时延判定「是否真的走了远程」，因此改成校验响应内容。
    if curl -s --max-time 5 "$HOST/feign/ping" | grep -q '"remote":"pong"'; then
        echo -e "  ${GREEN}✅ PASS${NC}: 11.2 远程 ping 返回 pong（下游可达）"
        PASS=$((PASS+1))
    else
        echo -e "  ${RED}❌ FAIL${NC}: 11.2 远程 ping 未返回 pong"
        FAIL=$((FAIL+1))
    fi

    # 11.1 首次：未命中 → 真实远程（服务端睡 10 秒）
    assert_timing "11.1 首次未命中(远程 ~10s)" "$(ms $HOST/feign/$FEIGN_ID)" miss
    # 11.1 二次：命中 → 未发出 HTTP，立即返回
    assert_timing "11.1 二次命中(未发远程)"   "$(ms $HOST/feign/$FEIGN_ID)" hit
    assert_timing "11.1 三次命中(未发远程)"   "$(ms $HOST/feign/$FEIGN_ID)" hit
else
    echo -e "  ⏭ SKIP: Samples.Api (16600) 未启动 —— 第十一章需要它提供 10 秒接口"
fi

cleanup

echo ""
echo "=========================================="
echo -e "  结果: ${GREEN}${PASS} passed${NC}, ${RED}${FAIL} failed${NC}"
echo "=========================================="
[ "$FAIL" -eq 0 ] && echo "🎉 全部通过!" || echo "😢 有失败用例"

exit "$FAIL"
