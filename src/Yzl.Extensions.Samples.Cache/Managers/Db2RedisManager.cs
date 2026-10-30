namespace Yzl.Extensions.Samples.Cache.Managers;

/// <summary>
/// 【示例 8.6：方法级覆盖用的第二个库】
///
/// 与 <see cref="Db1RedisManager"/> 只差一个数字 —— 这就是「方法 B 指定使用 db 2」
/// 的全部改动量：<b>注解上换个 <c>typeof</c></b>，框架源码一行不动。
///
/// 两个管理器的 <c>ConnectionStringKey</c> 都是默认的 <c>redis:main-site</c>，
/// 于是共享同一个 <see cref="StackExchange.Redis.ConnectionMultiplexer"/>，
/// 只是读写的库索引不同（1 / 2）。
///
/// 对照 Java：<c>@Qualifier("db1RedisTemplate")</c> 与 <c>@Qualifier("db2RedisTemplate")</c>，
/// 两个 <c>@Bean</c> 的差别就是 <c>setDatabase(...)</c>。
/// </summary>
public class Db2RedisManager(IRedisConnectionFactory connectionFactory)
    : RedisCacheManager(connectionFactory)
{
    /// <summary>2 号库。同样覆盖掉连接串里的 <c>defaultDatabase</c>（11）。</summary>
    protected override int? DatabaseIndex => 2;
}
