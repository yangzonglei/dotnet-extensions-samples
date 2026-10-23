namespace Yzl.Extensions.Samples.Cache.Managers;

/// <summary>
/// 【示例 10.1：连接来自<b>配置</b> —— <c>spring:data:redis</c>】
///
/// 和第八章的 <c>CasAuthRedisManager</c> / <c>Db1RedisManager</c> 一样，仍然是<b>一行子类</b>
/// （框架 README §3 的「四个重写点」之一：换连接名）。
///
/// ⚔️ <b>本类新的地方不在 C# 侧，而在配置侧</b>
///
/// 第八章那三个后端的连接串来自 <c>appsettings.Development.json</c> 里的<b>扁平键</b>
/// （<c>"redis:cas-auth": "host:port,password=…"</c>）。本类这条连接<b>不写扁平键</b>，
/// 而是走 Java 同名的配置节：
///
/// <code>
///   spring:
///     data:
///       redis:
///         host: localhost
///         port: 6399
///         database: 5
///         connection-string: abortConnect=false,connectRetry=3   # 本包扩展的逃生口
///         connect-timeout: 2s
///         timeout: 2s
///         client-name: samples-cache-config-backend
/// </code>
///
/// 节点形状与 Java 的 <c>RedisProperties</c> <b>逐字段一致</b>：这个节只描述<b>一个</b> Redis，
/// 没有中间的 <c>default</c> / <c>connections</c> 层。要连第二个 Redis，Java 是自己声明
/// <c>@Bean RedisConnectionFactory</c>，这里是 <c>AddRedisConnection(...)</c>（见 10.2）。
///
/// 结构化配置相对扁平连接串多出来的东西：
///
/// <list type="bullet">
///   <item>能写 <b>超时 / 库索引 / TLS</b>，且用 Java 的 <c>Duration</c> 写法
///         （<c>2s</c> / <c>500ms</c> / <c>PT5S</c>，无单位按毫秒）；</item>
///   <item><c>abortConnect=false</c> 这类高可用默认值由框架铺底，不用每处连接串重复写；</item>
///   <item>本包没建模的 StackExchange.Redis 原生参数可以写进 <c>connection-string</c>。</item>
/// </list>
///
/// <b>两级查找顺序</b>（框架 README §2.1）：<b>按连接名的扁平连接串键</b>（更具体、优先）→
/// <c>spring:data:redis</c>（且给了端点）→ 抛「缺配置」。
/// 本类命中的是第 2 层，因为 <c>redis:samples-config</c> <b>没有</b>扁平键；
/// 而第八章的 <c>redis:main-site</c> / <c>redis:cas-auth</c> 在 <c>appsettings.Development.json</c>
/// 里有扁平键，于是它们仍是第 1 层 —— 两边互不干扰。这正是「扁平键优先」的用意：
/// 给全局配置节加一节，不会把既有部署里指向别的实例的连接悄悄改道。
///
/// <b>怎么核对这条连接真的生效了</b>（起在本地的可丢弃实例上）：
/// <code>
///   redis-cli -p 6399 -n 5 KEYS 'conn:config*'   → 有值 ✅（命中配置里的 database=5）
///   redis-cli -p 6399 CLIENT LIST                → 有 samples-cache-config-backend ✅
/// </code>
/// <c>CLIENT LIST</c> 里那个名字就是配置里 <c>client-name</c> 的值 —— 这也是框架默认
/// 取应用名的地方（不写就显示应用名），排障时一眼能认出是哪个服务连上来的。
/// </summary>
public class ConfigBackendRedisManager(IRedisConnectionFactory connectionFactory)
    : RedisCacheManager(connectionFactory)
{
    /// <summary>连接名。本样例里它<b>没有</b>扁平键，所以最终命中 <c>spring:data:redis</c> 那一节。</summary>
    public const string Name = "redis:samples-config";

    /// <summary>配置节里显式写的库索引，仅用于文档/端点展示与核对（真正生效的仍是配置）。</summary>
    public const int ConfiguredDatabase = 5;

    /// <summary>配置里的 <c>client-name</c>，用于文档/端点展示。</summary>
    public const string ConfiguredClientName = "samples-cache-config-backend";

    /// <summary>
    /// 重写点 ①：连接名。这么做之后，本后端的连接参数<b>完全由配置决定</b>，
    /// 换 Redis 实例 / 改库索引 / 调超时都不用碰代码。
    /// </summary>
    protected override string ConnectionStringKey => Name;
}
