using StackExchange.Redis;
using Yzl.Extensions.Cache.Redis;

namespace Yzl.Extensions.Samples.Cache.Managers;

/// <summary>
/// 【示例 10.2：用户自定义连接的缝 —— Java 里「自己声明一个 <c>@Bean RedisConnectionFactory</c>」】
///
/// 对应 README §2.2 的<b>路二</b>。本类认领 <see cref="ConnectionName"/> 这一个连接名，
/// 别的一律返回 <c>null</c>（<b>不认领</b>）交给链上的下一个来源。
///
/// ⚔️ <b>为什么说「用户连接优先」是<b>可验证</b>的，而不是文档里的空话</b>
///
/// <c>appsettings.json</c> 里<b>也</b>给这个连接名配了扁平连接串键
/// （<c>"redis:samples-injected": "localhost:6399,defaultDatabase=7,…"</c>）。但本来源认领了
/// 这个名字 ⇒ <c>RedisConnectionProvider.Create()</c> 的解析顺序是
/// <b>① 用户来源 → ② 配置 → ③ 抛错</b>，第 ① 步命中就<b>根本不会去读配置</b>。
///
/// 于是本样例刻意让两边<b>不一样</b>，好让你一眼看出谁赢了：
///
/// <code>
///   配置里的扁平连接串（不生效）→ db 7, clientName 由框架推导（应用名）
///   本类自己建的连接（真正生效）→ db 8, clientName = "samples-cache-injected-by-user"
/// </code>
///
/// 核对方式（连接是真的生效了才会出现）：
/// <code>
///   redis-cli -p 6399 -n 8 KEYS 'conn:injected*'   → 有值 ✅（用户连接生效）
///   redis-cli -p 6399 -n 7 KEYS 'conn:injected*'   → 空   ✅（配置被跳过）
///   redis-cli -p 6399 CLIENT LIST                  → 能看到 samples-cache-injected-by-user
/// </code>
///
/// ⚔️ <b>「只创建一次」的责任在<b>你</b>这边</b>
///
/// 框架侧的 <c>RedisMultiplexerHolder</c> 只对<b>框架自己建</b>的连接负责。用户来源返回什么、
/// 建几次，框架不干预 —— README §2.2 的契约表把这条写成「只创建一次：同一个连接名只应返回
/// 同一个实例，最终由你的实现保证」。
///
/// 本类用和框架内部<b>同一套形状</b>来保证，而不是 <c>Lazy&lt;T&gt;</c>：
///
/// <code>
///   lock + 二次检查 → 并发首访也只建一条
///   成功才发布      → 工厂抛异常时什么都不发布，下次访问重新建
/// </code>
///
/// 为什么不写 <c>Lazy&lt;IConnectionMultiplexer&gt;(ExecutionAndPublication)</c>：
/// 它会<b>永久缓存工厂抛出的异常</b> —— 第一次失败之后 factory 再也不被调用，
/// 「开机抖动一次 = 这个后端到进程重启为止一直失效」。框架内部刻意换掉了 <c>Lazy</c>
/// 就是这个原因（README §6）；你自己建连接时同样别踩。见 <see cref="ConnectCount"/> 的说明。
///
/// ⚔️ <b>释放归属</b>：交进来的连接<b>归你</b>。框架的 <c>Dispose</c> 链<b>不会</b>释放它。
/// 本类实现 <see cref="IDisposable"/> 并注册成 DI 单例，于是由容器负责释放 —— 这是推荐做法。
/// </summary>
public sealed class SamplesInjectedConnectionSource : IRedisConnectionSource, IDisposable
{
    /// <summary>本来源认领的连接名。与 <see cref="RedisConnectionProvider"/> 无关，是<b>用户自己起的名字</b>。</summary>
    public const string ConnectionName = "redis:samples-injected";

    /// <summary>本来源自己建的连接所用的库索引。刻意与配置节里写的 <c>7</c> 不同（见类文档）。</summary>
    public const int DatabaseIndex = 8;

    private static int _connectCount;

    /// <summary>
    /// 建连次数 —— 本样例用来证明「同一个连接名只建一次」。
    ///
    /// 期望值：进程内从 0 涨到 <b>1</b> 之后就不再变化，无论并发多少请求。
    /// 端点 <c>GET /api/samples/connection/injected-connection-count</c> 把它打印出来。
    /// </summary>
    public static int ConnectCount => Volatile.Read(ref _connectCount);

    private readonly object _gate = new();
    private IConnectionMultiplexer? _connection;

    /// <summary>
    /// 认领 <see cref="ConnectionName"/>，其余返回 <c>null</c>。
    ///
    /// ⚠️ <b>契约</b>：不认领必须返回 <c>null</c>，<b>不得</b>用抛异常表示「这个连接我没管」——
    /// 那会把「不关我的事」变成一次业务失败（README §2.2）。
    /// </summary>
    public IConnectionMultiplexer? GetConnection(string connectionName) =>
        string.Equals(connectionName, ConnectionName, StringComparison.Ordinal)
            ? GetOrCreate()
            : null;

    /// <summary>
    /// 惰性建连 + 成功才发布 + 失败不留痕（与框架内部的 <c>RedisMultiplexerHolder</c> 同一形状）。
    ///
    /// 生产项目请<b>不要</b>把端点写死在代码里 —— 这里写死是为了让「连接参数完全由用户决定」
    /// 这件事在样本里一目了然。<b>凭据（密码 / TLS 私钥）请务必走配置或密钥服务，
    /// 且任何情况下都不得写进日志、异常消息或接口响应。</b>
    /// </summary>
    private IConnectionMultiplexer GetOrCreate()
    {
        var existing = Volatile.Read(ref _connection);
        if (existing is not null)
        {
            return existing;
        }

        lock (_gate)
        {
            if (_connection is not null)
            {
                return _connection;
            }

            // 用户完全掌控连接参数：端点、超时、abortConnect、心跳、客户端名……
            // 框架在这一步之前就退出了（用户来源优先），所以这里写什么就是什么。
            var options = ConfigurationOptions.Parse(
                "localhost:6399,abortConnect=false,connectTimeout=2000,syncTimeout=2000,keepAlive=30");
            options.DefaultDatabase = DatabaseIndex;
            options.ClientName = "samples-cache-injected-by-user";

            // ⚠️ 建连在 lock 内、发布之前完成：抛异常时 _connection 仍为 null，
            //    下一次访问会重新建 —— 这就是「失败不留痕」。
            var created = ConnectionMultiplexer.Connect(options);

            Interlocked.Increment(ref _connectCount);

            Volatile.Write(ref _connection, created);
            return created;
        }
    }

    /// <summary>由 DI 容器调用（本类注册为单例）。</summary>
    public void Dispose()
    {
        var existing = Interlocked.Exchange(ref _connection, null);
        existing?.Dispose();
    }
}
