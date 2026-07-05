using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace Yzl.Extensions.Samples.TestDashboard;

/// <summary>
/// 注册测试面板端点（/dashboard/api/routes 和 /dashboard）的扩展方法。
/// </summary>
public static class MapTestDashboardExtensions
{
    /// <summary>
    /// 注册测试面板：
    /// <list type="bullet">
    ///   <item><c>GET /dashboard/api/routes</c> — 返回所有可测试路由的 JSON（含手动标记 + 自动发现的 Controller）</item>
    ///   <item><c>GET /dashboard</c> — 返回动态渲染的测试首页 HTML</item>
    /// </list>
    /// </summary>
    /// <param name="app">Web 应用实例</param>
    /// <param name="options">可选配置（分组显示名称等）</param>
    public static void MapTestDashboard(this WebApplication app, TestDashboardOptions? options = null)
    {
        // 排除自身端点
        var excludePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "", "/", "/dashboard", "/dashboard/api/routes" };

        app.MapGet("/dashboard/api/routes", (EndpointDataSource eds) =>
        {
            var routeList = new List<RouteEntry>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 从 Controller 的 [TestDashboardInfo] 特性自动发现的分组信息
            var attributeGroups = new Dictionary<string, (string Title, int Order, string? Badge)>();

            foreach (var endpoint in eds.Endpoints)
            {
                if (endpoint is not RouteEndpoint routeEndpoint) continue;

                var path = routeEndpoint.RoutePattern.RawText ?? "";
                if (excludePaths.Contains(path)) continue;

                // 已经处理过的同路径路由跳过（优先取第一个带元数据的）
                if (!seenPaths.Add(path)) continue;

                var httpMethod = GetHttpMethod(endpoint);
                var testMeta = endpoint.Metadata.GetMetadata<TestRouteMetadata>();

                string group;
                string desc;
                bool isSse;

                if (testMeta != null)
                {
                    // Minimal API 路由：使用附加的元数据
                    group = testMeta.Group;
                    desc = testMeta.Desc;
                    isSse = testMeta.IsSse;

                    // 收集 Minimal API 分组的排序信息
                    if (!attributeGroups.ContainsKey(group))
                    {
                        attributeGroups[group] = (desc, testMeta.Order, null);
                    }
                }
                else if (IsControllerEndpoint(endpoint))
                {
                    // Controller 路由：自动发现
                    group = ResolveControllerGroup(path, endpoint);
                    desc = GetControllerDescription(endpoint);
                    isSse = false;

                    // 从 Controller 类上读取 [TestDashboardInfo] 特性
                    var actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
                    var controllerType = actionDescriptor?.ControllerTypeInfo;
                    var dashboardInfo = controllerType?.GetCustomAttribute<TestDashboardInfoAttribute>();
                    if (dashboardInfo != null && actionDescriptor != null)
                    {
                        var controllerName = actionDescriptor.ControllerName;
                        if (!attributeGroups.ContainsKey(controllerName))
                        {
                            attributeGroups[controllerName] = (dashboardInfo.Title, dashboardInfo.Order, dashboardInfo.Badge);
                        }
                    }
                }
                else
                {
                    // 其他路由（如静态文件、健康检查等）跳过
                    continue;
                }

                routeList.Add(new RouteEntry(group, httpMethod, NormalizePath(path), desc, isSse));
            }

            // 构建分组信息：
            // 1. options.Groups 为手动覆盖（优先级最高，向后兼容）
            // 2. 没有手动配置时，使用 [TestDashboardInfo] 自动发现的信息
            // 3. 都没有时，使用分组 key 本身作为标题
            var groupTitles = options?.Groups ?? new Dictionary<string, (string Title, string Badge)>();
            var groups = routeList
                .Select(r => r.Group)
                .Distinct()
                .Select(g =>
                {
                    var hasManual = groupTitles.TryGetValue(g, out var manual);
                    var hasAttr = attributeGroups.TryGetValue(g, out var attr);

                    return new
                    {
                        id = g,
                        title = hasManual ? manual.Title : (hasAttr ? attr.Title : g),
                        badge = hasManual ? manual.Badge : (hasAttr ? (attr.Badge ?? "") : ""),
                        order = hasAttr ? attr.Order : int.MaxValue
                    };
                })
                .OrderBy(g => g.order)
                .ThenBy(g => g.id, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { g.id, g.title, g.badge })
                .ToList();

            return new { groups, routes = routeList };
        });

        app.MapGet("/dashboard", (HttpContext context) =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";

            // 页面标题：优先取配置，否则自动取入口程序集名称
            var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "API 测试面板";
            var title = options?.Title ?? assemblyName;

            return context.Response.WriteAsync(TestDashboardHtml.GetContent(title));
        });
    }

    private static string GetHttpMethod(Endpoint endpoint)
    {
        var httpMethods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>();
        if (httpMethods?.HttpMethods.Count > 0)
        {
            return httpMethods.HttpMethods.First().ToUpper();
        }
        return "GET";
    }

    private static bool IsControllerEndpoint(Endpoint endpoint)
    {
        // Controller 端点会有 ControllerAttribute 元数据
        return endpoint.Metadata.GetMetadata<ControllerAttribute>() != null;
    }

    private static string ResolveControllerGroup(string path, Endpoint endpoint)
    {
        // 取 Controller 名称作为分组
        var controllerAction = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (controllerAction?.ControllerName != null)
        {
            return controllerAction.ControllerName;
        }

        // 从路径中取第一段作为分组
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? segments[0] : "controller";
    }

    private static string GetControllerDescription(Endpoint endpoint)
    {
        // 优先取 [Description] 属性
        var descAttr = endpoint.Metadata.GetMetadata<DescriptionAttribute>();
        if (descAttr != null && !string.IsNullOrEmpty(descAttr.Description))
        {
            return descAttr.Description;
        }

        // 其次取 Action 名
        var controllerAction = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (controllerAction?.ActionName != null)
        {
            // ActionName is already PascalCase; add spaces for readability
            return SplitPascalCase(controllerAction.ActionName);
        }

        // 最后取 DisplayName 的最后一段
        if (endpoint.DisplayName != null)
        {
            var last = endpoint.DisplayName.Split('.').LastOrDefault()?.Replace(" (", "");
            return last ?? "";
        }

        return "";
    }

    private static string SplitPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return System.Text.RegularExpressions.Regex.Replace(input, "(\\B[A-Z])", " $1");
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "/";
        return path.StartsWith('/') ? path : "/" + path;
    }
}

/// <summary>
/// 内部路由条目 DTO，序列化为 JSON 返回给前端。
/// </summary>
internal record RouteEntry(
    string Group,
    string Method,
    string Path,
    string Desc,
    bool IsSse
);
