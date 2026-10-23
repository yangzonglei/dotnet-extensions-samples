namespace Yzl.Extensions.Samples.Cache.Models;

/// <summary>
/// 【示例 8.5 专用样本】—— 让「两个后端用了不同序列化器」这件事在 redis-cli 里<b>肉眼可见</b>。
///
/// 为什么必须单独造一个 DTO，而不是复用 <see cref="UserDto"/>：
/// 两个内置序列化器对 <see cref="UserDto"/> 里那几类字段（string / int / DateTime / null）
/// 产生的 JSON 文本<b>恰好相同</b> —— Newtonsoft 与 System.Text.Json 的默认 ISO-8601
/// 格式、属性命名、null 处理都是一致的。拿 UserDto 去比会得出「两个后端一样」的错觉。
///
/// 差异出现在<b>浮点</b>上：同一个 <c>1.0</c>，
///   - Newtonsoft 写 <c>1.0</c>（保留小数位）
///   - System.Text.Json 写 <c>1</c>（最简往返表示）
/// 这就是 <c>RedisCacheSerializerTests.BuiltInSerializers_ProduceDifferentText_ForFloatingPoint</c>
/// 钉住的那条事实，这里把它搬到样例里让人能直接看到。
///
/// 验证：<c>redis-cli -n 11 GET multi:sample:1</c> 与
/// <c>redis-cli -n 12 GET multi:cas-sample:1</c> 的 <c>Score</c> 字段写法不同。
/// </summary>
public class MultiBackendSampleDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>整数值的浮点：两个引擎的写法不同（<c>1.0</c> vs <c>1</c>），是本示例的观察点。</summary>
    public double Score { get; set; }
}
