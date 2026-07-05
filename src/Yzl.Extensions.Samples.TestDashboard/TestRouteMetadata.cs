using Microsoft.AspNetCore.Builder;

namespace Yzl.Extensions.Samples.TestDashboard;

/// <summary>
/// 附加到 Minimal API 路由上的元数据，用于测试面板自动发现。
/// 通过 <see cref="TestRouteMetadataExtensions.WithTest"/> 扩展方法附加。
/// </summary>
public record TestRouteMetadata(
    string Group,
    string Desc,
    bool IsSse = false,
    int Order = 9999
);

/// <summary>
/// 为 Minimal API 路由附加测试元数据的扩展方法。
/// </summary>
public static class TestRouteMetadataExtensions
{
    /// <summary>
    /// 为路由附加测试面板所需的元数据（分组、描述、SSE 标记、排序）。
    /// </summary>
    /// <param name="builder">路由构建器</param>
    /// <param name="group">分组 key，用于在测试页面上归类</param>
    /// <param name="desc">人类可读的描述</param>
    /// <param name="isSse">是否为 SSE 流端点</param>
    /// <param name="order">排序顺序（升序），同分组下的路由共用此顺序</param>
    public static RouteHandlerBuilder WithTest(
        this RouteHandlerBuilder builder,
        string group,
        string desc,
        bool isSse = false,
        int order = 9999)
    {
        return builder.WithMetadata(new TestRouteMetadata(group, desc, isSse, order));
    }
}
