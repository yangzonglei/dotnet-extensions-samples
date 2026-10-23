using Yzl.Extensions.Cache.Interfaces;
using Yzl.Extensions.Cache.Managers;
using Yzl.Extensions.Cache.Redis;
using Yzl.Extensions.Cache.Redis.Serialization;

namespace Yzl.Extensions.Samples.Cache.Managers;

/// <summary>
/// 【示例 10.3：<b>完全不继承</b> <c>RedisCacheManager</c>，手写一个 <c>ICacheManager</c>】
///
/// 这是框架 README §5「完全自己控制：裸零件仍然可用」的活样例。它回答的是用户诉求第 2 条
/// 的后半句 —— 「客户端<b>实现一个框架里的接口</b>或着继承一个父类」：
/// 继承父类是 10.1 / 10.2；这里是<b>直接实现接口</b>。
///
/// ⚔️ <b>什么时候真的需要这么写</b>
///
/// 多数场景用一行子类就够了（改连接名 / 库索引 / 序列化器 / 是否允许 null）。
/// 只有当你需要**组合出「四个重写点」表达不了的东西**时才落到这一层，例如：
///
/// <list type="bullet">
///   <item>连接是<b>自己建</b>的（见 <see cref="SamplesInjectedConnectionSource"/>），
///         而你想让容器里那个<b>用户连接</b>直接进来，而不是再走一遍 <c>IRedisConnectionFactory</c>；</item>
///   <item>同一个管理器要按 <b>cacheName</b> 切换序列化器（子类的 <c>Serializer</c> 是整体替换，做不到按名切）；</item>
///   <item>想在 provider 外面再包一层自己的 <c>ICacheProvider</c> 装饰器（打点、降级、键改写…）。</item>
/// </list>
///
/// ⚔️ <b>手写实现同样<b>不需要注册</b></b>
///
/// 核心包自动注册的口径是「<b>实现 <c>ICacheManager</c> 的具体类型</b>」，跟继不继承
/// <c>RedisCacheManager</c> 无关（框架 README §5 末段）。所以本类<b>没有</b> <c>[IocService]</c>、
/// <b>没有</b> <c>AddSingleton</c>、<b>没有</b>要求 <c>Program.cs</c> 的注册顺序 ——
/// <c>AddEnableCaching()</c> 扫描时就把本类当成单例收进去了。
///
/// ⚔️ <b>唯一的硬约束：<c>Provider</c> 的声明类型必须是 <c>ICacheProvider</c></b>
///
/// 不能声明成具体的 <c>RedisCacheProvider</c>。核心包的 <c>CacheInvokerCache</c> 在编译
/// 写缓存委托时会做 <c>Expression.Convert(provider, typeof(ICacheProvider))</c>，
/// 声明成具体类型会在<b>首次调用</b>时炸在表达式树上（不是编译期）。
///
/// ⚔️ <b>库索引留给「连接」决定（<c>databaseIndex: null</c>）</b>
///
/// 传 <c>null</c> 时 provider 走 <c>GetDatabase()</c>，也就是<b>用户连接自己的
/// <c>DefaultDatabase</c></b>（本样例是 <b>8</b>，见 <see cref="SamplesInjectedConnectionSource.DatabaseIndex"/>）。
/// 这样正好成了「用户连接真的生效了」的<b>证据</b>：配置节里写的是 <c>7</c>，
/// 而数据落在 <b>8</b> —— 说明第 ① 步（用户来源）命中后压根没读配置。
/// 反过来若在这里写死 <c>databaseIndex: 8</c>，就分不清是「连接说了算」还是「manager 说了算」了。
/// </summary>
public class InjectedConnectionCacheManager(IRedisConnectionFactory connectionFactory) : ICacheManager
{
    /// <summary>
    /// 本管理器<b>全新的</b>一套零件组合，与第八章那四个后端（<c>RedisCacheManager</c>、
    /// <c>CasAuthRedisManager</c>、<c>Db1RedisManager</c>、<c>Db2RedisManager</c>）互不相干。
    ///
    /// <c>connectionFactory.Get(name)</c> 拿到的是<b>按连接名去重</b>的连接持有者
    /// （<c>RedisConnectionFactory</c> 用 <c>StringComparer.Ordinal</c> 缓存），
    /// 所以这里<b>不会</b>重复建连 —— 「只创建一次」由用户的来源 + 框架的去重共同保证。
    ///
    /// 序列化器刻意用 <c>SystemTextJsonRedisCacheSerializer</c>（非默认），
    /// 用来证明「手写实现能自己挑序列化器」。⚠️ 它产出的字节与默认的 Newtonsoft 实现<b>不同</b>，
    /// 所以必须配一个<b>独占的 cacheName</b>（见 <see cref="CacheName"/>），
    /// 绝不与第八章的 <c>redis:users</c> 之类共用 —— 否则旧数据会静默读不回来。
    /// </summary>
    public ICacheProvider Provider { get; } = new RedisCacheProvider(
        connectionFactory.Get(SamplesInjectedConnectionSource.ConnectionName),
        new SystemTextJsonRedisCacheSerializer(),
        databaseIndex: null);

    /// <summary>本管理器独占的 cacheName。命名上单独开一段，避免与其它章节的键混在一起。</summary>
    public const string CacheName = "conn:injected:users";
}
