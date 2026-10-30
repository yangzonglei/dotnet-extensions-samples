using Microsoft.Extensions.Configuration;
using Yzl.Extensions.Core.Attributes;

namespace Yzl.Extensions.Samples.Cache.Configuration;

/// <summary>
/// 【示例 10.4：客户端自己用 <c>[ConfigurationProperties("spring:data:redis")]</c> 读 Redis 配置】
///
/// 这就是「客户端想要一份自定义的 Redis 配置视图」的标准写法：写一个普通 POCO、
/// 标上 <see cref="ConfigurationPropertiesAttribute"/>、构造函数里注入
/// <c>IOptions&lt;SamplesRedisConfig&gt;</c> —— 注册由框架的全局扫描完成
/// （<c>AddBatchServices()</c> / <c>AddFeignStarter(...)</c> 内部都会调
/// <c>AddConfigurationPropertiesScan</c>），<b>本类不需要任何注册代码</b>。
///
/// <b>为什么样例要自己再写一份，而不是直接用框架的 <c>RedisCacheOptions</c></b>：
/// 这正是用户诉求第 1 条 —— 「系统里提供一个 redis 链接配置，客户端如果需要自定义
/// redisManager 就通过 <c>[ConfigurationProperties("")]</c> 获取自定义 redis 配置」。
/// 框架的 <c>RedisCacheOptions</c> 是<b>框架自己读连接参数</b>用的；客户端要的是
/// <b>自己读</b>。两者绑同一个节、互不干扰（<c>Configure&lt;T&gt;</c> 是按类型注册的）。
///
/// <b>节点形状与 Java 完全一致</b>：<c>spring.data.redis</c> 直接就是那几个字段，
/// <b>没有</b>中间的 <c>default</c> / <c>connections</c> 层。
/// 框架自己的 <c>RedisCacheOptions</c> 也是同样的平铺结构（逐字段对齐
/// <c>org.springframework.boot.autoconfigure.data.redis.RedisProperties</c>）。
///
/// <b>多个 Redis 怎么办</b>：Java 里是自己声明多个 <c>@Bean RedisConnectionFactory</c>，
/// 本框架里是 <c>AddRedisConnection(...)</c> / 实现 <c>IRedisConnectionSource</c>
/// （见 10.2）。<b>不要</b>指望在这一个配置节里塞多组连接 —— 那样就从 Java 的语义
/// 漂移出去了。
///
/// <b>本类刻意没有 <c>Password</c> 属性</b>：样例要证明「读出来的配置可以安全地
/// 输出/打日志」。真实项目需要密码时请显式加属性，并保证它<b>不进日志、不进接口响应</b>
/// —— 本仓所有相关文档都遵守这条纪律。
/// </summary>
[ConfigurationProperties(SectionName)]
public class SamplesRedisConfig
{
    /// <summary>配置节名。与 Java 的 <c>spring.data.redis</c> 逐字对齐（本框架用冒号分层）。</summary>
    public const string SectionName = "spring:data:redis";

    /// <summary>主机名 / IP。给了它，连接串里的端点会被整体替换。</summary>
    public string? Host { get; set; }

    /// <summary>端口。只给 <c>port</c> 不给 <c>host</c> 时，改的是连接串里那个端点的端口。</summary>
    public int? Port { get; set; }

    /// <summary>库索引（映射 StackExchange.Redis 的 <c>defaultDatabase</c>）。</summary>
    public int? Database { get; set; }

    /// <summary>
    /// Redis <c>CLIENT LIST</c> 里显示的客户端名。
    ///
    /// ⚠️ 必须显式给键名：绑定器只做大小写不敏感匹配，<b>不做</b>连字符→驼峰的转换，
    /// 裸写 <c>ClientName</c> 属性去接 <c>client-name</c> 是<b>静默绑不上</b>的
    /// （不抛异常、值为 null —— 与框架 <c>RedisCacheOptions</c> 同一处坑）。
    /// </summary>
    [ConfigurationKeyName("client-name")]
    public string? ClientName { get; set; }

    /// <summary>
    /// 命令超时（Java <c>spring.data.redis.timeout</c>）。配置里写成 <c>5s</c> / <c>500ms</c>
    /// 这类 Java <c>Duration</c> 写法；本类只原样读字符串，解析规则与框架的
    /// <c>RedisDurationParser</c> 相同（无单位按毫秒）。样例不做二次解析，所以保持 string。
    /// </summary>
    public string? Timeout { get; set; }

    /// <summary>建连超时（Java <c>spring.data.redis.connect-timeout</c>），写法同 <see cref="Timeout"/>。</summary>
    [ConfigurationKeyName("connect-timeout")]
    public string? ConnectTimeout { get; set; }
}
