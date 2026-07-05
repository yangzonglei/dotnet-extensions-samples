namespace Yzl.Extensions.Samples.TestDashboard;

/// <summary>
/// 标注在 Controller 类上，为测试面板提供分组显示信息和排序顺序。
/// 替代在 <see cref="TestDashboardOptions.Groups"/> 中手动配置。
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class TestDashboardInfoAttribute : Attribute
{
    /// <summary>
    /// 分组显示标题（如 "📦 CRUD 基础操作"）。
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 排序顺序（升序）。不指定时默认 9999，排在最后。
    /// </summary>
    public int Order { get; set; } = 9999;

    /// <summary>
    /// 可选的徽章文本（如 "1"、"Beta"）。
    /// </summary>
    public string? Badge { get; set; }

    public TestDashboardInfoAttribute(string title)
    {
        Title = title;
    }
}
