using System.Reflection;

namespace Yzl.Extensions.Samples.TestDashboard;

/// <summary>
/// 测试面板的可选配置项。
/// </summary>
public class TestDashboardOptions
{
    /// <summary>
    /// 分组显示信息。key = 分组名，value = (标题, 徽章文本)。
    /// 不提供时，标题和徽章使用分组名。
    /// </summary>
    public Dictionary<string, (string Title, string Badge)>? Groups { get; set; }

    /// <summary>
    /// 页面标题。不指定时自动取入口程序集名称。
    /// </summary>
    public string? Title { get; set; }
}
