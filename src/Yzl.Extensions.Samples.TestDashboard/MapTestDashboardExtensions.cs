using System.ComponentModel;
using System.IO.Pipelines;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
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

                // 只有写方法才需要列出可填写的参数：GET 一律留空，
                // 避免影响既有 GET 端点的渲染与请求构造行为。
                var parameters = new List<ParamEntry>();
                BodyEntry? bodyParam = null;
                if (IsWriteMethod(httpMethod))
                {
                    (parameters, bodyParam) = ExtractParameters(routeEndpoint, httpMethod);
                }

                routeList.Add(new RouteEntry(
                    group, httpMethod, NormalizePath(path), desc, isSse,
                    parameters, bodyParam, BuildUrlPattern(NormalizePath(path))));
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

    /// <summary>
    /// 把路由原文（<see cref="RoutePattern.RawText"/>）转成可直接发请求的 URL 模板。
    ///
    /// 路由原文里的 <c>{id:long}</c> 同时包含参数名、路由约束（<c>:long</c>）、
    /// 可选标记（<c>?</c>）与默认值（<c>=1</c>）。这些是**路由匹配语法**，不是 URL 的一部分：
    /// 直接拿原文去请求（或只替换成 <c>{id}</c> 而把 <c>:long}</c> 留在 URL 里）只会 404，
    /// 因此这里只保留参数名，输出 <c>{id}</c> 形式交给前端替换成真实值。
    /// </summary>
    private static string BuildUrlPattern(string path) =>
        UrlPatternRegex.Replace(path, m => "{" + m.Groups["name"].Value + "}");

    /// <summary>
    /// 逐个匹配路由模板中的参数段落：<c>{name}</c> / <c>{name:constraint}</c> /
    /// <c>{name:constraint(..)}</c> / <c>{name?}</c> / <c>{name=default}</c> /
    /// <c>{*slug}</c> / <c>{**slug}</c>（catch-all 的星号不属于参数名，需一并剥掉）。
    /// </summary>
    private static readonly Regex UrlPatternRegex = new(
        @"\{(?<catchall>\*{1,2})?(?<name>\w+)(?:[^}]*)\}",
        RegexOptions.Compiled);

    // ===================================================================
    // 参数提取：为写方法（POST/PUT/PATCH/DELETE）列出可填写的参数，
    // 供测试面板自动生成输入框，避免"面板不传值 → 服务端静默用默认值 → 200 假成功"。
    // ===================================================================

    private static bool IsWriteMethod(string httpMethod) =>
        httpMethod is "POST" or "PUT" or "PATCH" or "DELETE";

    /// <summary>
    /// 提取端点的可填写参数。
    /// <para>Form 参数按绑定源逐个列出；复杂类型（[FromBody]）返回 JSON 骨架；</para>
    /// <para>两者皆无时前端会退化为"原始 Body 文本框"。</para>
    /// </summary>
    private static (List<ParamEntry> Parameters, BodyEntry? BodyParam) ExtractParameters(
        RouteEndpoint routeEndpoint, string httpMethod)
    {
        var actionDescriptor = routeEndpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (actionDescriptor == null)
        {
            // Minimal API 处理器没有参数描述符（运行时才会推断绑定源），
            // 这里不做猜测：前端会为其渲染"原始 Body 文本框"。
            return (new List<ParamEntry>(), null);
        }

        var parameters = new List<ParamEntry>();
        BodyEntry? bodyParam = null;
        var isApiController = routeEndpoint.Metadata.GetMetadata<ApiControllerAttribute>() != null;

        // 已被路由模板占用的参数名（如 {id}），由前端现有的 URL 参数输入框负责，避免重复渲染
        var routeParamNames = new HashSet<string>(
            routeEndpoint.RoutePattern.Parameters.Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in actionDescriptor.Parameters)
        {
            if (descriptor is not ControllerParameterDescriptor cpd) continue;

            var parameterInfo = cpd.ParameterInfo;
            var parameterType = parameterInfo.ParameterType;

            if (routeParamNames.Contains(descriptor.Name)) continue;
            if (IsUnbindable(parameterType)) continue;

            var source = ResolveSource(parameterInfo, parameterType, isApiController, httpMethod);
            if (source == null) continue;   // [FromServices] 等由框架注入的参数

            if (source == "body")
            {
                // 复杂类型：不列举属性，直接给一份 JSON 骨架让用户改
                bodyParam = new BodyEntry(
                    descriptor.Name,
                    GetFriendlyTypeName(parameterType),
                    BuildJsonSkeleton(parameterType));
                continue;
            }

            parameters.Add(new ParamEntry(
                descriptor.Name,
                source,
                GetFriendlyTypeName(parameterType),
                IsRequired(parameterInfo),
                GetDefaultValue(descriptor.Name, parameterType)));
        }

        return (parameters, bodyParam);
    }

    /// <summary>
    /// 判断参数是否为框架注入、不参与模型绑定的类型（这些不应出现在填写表单里）。
    /// </summary>
    private static bool IsUnbindable(Type type)
    {
        return type == typeof(CancellationToken)
            || type == typeof(HttpContext)
            || type == typeof(HttpRequest)
            || type == typeof(HttpResponse)
            || type == typeof(ClaimsPrincipal)
            || type == typeof(Stream)
            || type == typeof(PipeReader)
            || typeof(IFormFile).IsAssignableFrom(type)
            || typeof(IFormFileCollection).IsAssignableFrom(type)
            || typeof(Stream).IsAssignableFrom(type);
    }

    /// <summary>
    /// 推断参数的绑定源，返回 <c>form</c> / <c>query</c> / <c>header</c> / <c>body</c>；
    /// 返回 <c>null</c> 表示跳过该参数（如 <c>[FromServices]</c>）。
    /// </summary>
    private static string? ResolveSource(
        ParameterInfo parameterInfo, Type parameterType, bool isApiController, string httpMethod)
    {
        if (parameterInfo.GetCustomAttribute<FromServicesAttribute>() != null) return null;
        if (parameterInfo.GetCustomAttribute<FromFormAttribute>() != null) return "form";
        if (parameterInfo.GetCustomAttribute<FromQueryAttribute>() != null) return "query";
        if (parameterInfo.GetCustomAttribute<FromHeaderAttribute>() != null) return "header";
        if (parameterInfo.GetCustomAttribute<FromBodyAttribute>() != null) return "body";

        // 无显式特性时按 ASP.NET Core 的推断规则：
        //   [ApiController] 下，简单类型 → query（GET/DELETE）/ form 之外的写方法仍按 body 走，
        //   这里保持保守：简单类型一律 form（对 [FromForm] 为主的 Cache 示例最友好），
        //   复杂类型在 [ApiController] 下是 body，否则是 form。
        if (IsSimpleType(parameterType)) return "form";
        return isApiController && SupportsBody(httpMethod) ? "body" : "form";
    }

    /// <summary>判断 HTTP 方法是否支持请求体。</summary>
    private static bool SupportsBody(string httpMethod) =>
        httpMethod is "POST" or "PUT" or "PATCH" or "DELETE";

    private static bool IsSimpleType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t.IsPrimitive
            || t.IsEnum
            || t == typeof(string)
            || t == typeof(decimal)
            || t == typeof(DateTime)
            || t == typeof(DateTimeOffset)
            || t == typeof(TimeSpan)
            || t == typeof(Guid);
    }

    /// <summary>参数是否为必填（无默认值且不可为 null）。</summary>
    private static bool IsRequired(ParameterInfo parameterInfo)
    {
        if (parameterInfo.HasDefaultValue) return false;

        var type = parameterInfo.ParameterType;
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) return false;               // int? 等可空值类型
        if (!type.IsValueType) return false;                // 引用类型一律按可选处理

        return true;
    }

    /// <summary>
    /// 给出类型感知的预填值；返回空串时由前端回退到自身的 PARAM_DEFAULTS 表。
    /// </summary>
    private static string GetDefaultValue(string name, Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        // 名称优先：让 id/age 这类字段拿到更直观的示例值
        var byName = name.ToLowerInvariant() switch
        {
            "id" or "userid" or "ids" => "1",
            "age" => "25",
            "minage" => "20",
            "maxage" => "40",
            "email" or "mail" => "test@test.com",
            "name" or "username" => "Alice",
            "keyword" or "query" or "q" => "test",
            "page" or "pageindex" => "1",
            "pagesize" or "size" => "20",
            _ => null
        };
        if (byName != null && (t == typeof(string) || t == typeof(int) || t == typeof(long))) return byName;

        if (t == typeof(string)) return "";
        if (t == typeof(bool)) return "true";
        if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)) return "0";
        if (t == typeof(double) || t == typeof(float) || t == typeof(decimal)) return "0";
        if (t == typeof(Guid)) return Guid.Empty.ToString();
        if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) return "2026-01-01T00:00:00";
        if (t.IsEnum) return Enum.GetNames(t).FirstOrDefault() ?? "";
        return "";
    }

    /// <summary>CLR 类型名到 C# 关键字/惯用名的映射，让面板显示 int/string 而不是 Int32/String。</summary>
    private static readonly Dictionary<Type, string> FriendlyTypeNames = new()
    {
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(short)] = "short",
        [typeof(int)] = "int",
        [typeof(long)] = "long",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(decimal)] = "decimal",
        [typeof(string)] = "string",
        [typeof(object)] = "object",
        [typeof(Guid)] = "Guid",
        [typeof(DateTime)] = "DateTime",
        [typeof(DateTimeOffset)] = "DateTimeOffset",
        [typeof(TimeSpan)] = "TimeSpan",
    };

    /// <summary>类型的可读名（去掉命名空间，泛型保留泛型参数名）。</summary>
    private static string GetFriendlyTypeName(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (FriendlyTypeNames.TryGetValue(t, out var friendly)) return friendly;
        if (t.IsGenericType)
        {
            var name = t.Name[..t.Name.IndexOf('`')];
            var args = string.Join(", ", t.GetGenericArguments().Select(GetFriendlyTypeName));
            return $"{name}<{args}>";
        }
        return t.Name;
    }

    /// <summary>
    /// 为复杂类型生成一份 JSON 骨架（camelCase、缩进），供面板预填到 JSON 文本框。
    /// </summary>
    private static string BuildJsonSkeleton(Type type) =>
        JsonSerializer.Serialize(BuildSkeletonValue(type, 0, new HashSet<Type>()), SkeletonJsonOptions);

    private static readonly JsonSerializerOptions SkeletonJsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// 递归构造骨架的占位对象树；最终由 <see cref="JsonSerializer"/> 序列化成带缩进的 JSON。
    /// </summary>
    private static object? BuildSkeletonValue(Type type, int depth, HashSet<Type> visiting, bool forceNull = false)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        // 可空成员：直接给 null（如 string? city、DateTime? updatedAt）
        if (forceNull) return null;

        // 简单类型：直接给字面量
        if (t == typeof(string)) return "";
        if (t == typeof(bool)) return false;
        if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
            || t == typeof(double) || t == typeof(float) || t == typeof(decimal)) return 0;
        if (t == typeof(Guid)) return Guid.Empty;
        if (t == typeof(DateTime)) return new DateTime(2026, 1, 1);
        if (t == typeof(DateTimeOffset)) return new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        if (t.IsEnum) return Enum.GetNames(t).FirstOrDefault() ?? "";
        if (t == typeof(object)) return new Dictionary<string, object?>();

        // 字典 → {}
        var dictInterface = t.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        if (dictInterface != null) return new Dictionary<string, object?>();
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            return new Dictionary<string, object?>();

        // 集合 → []（string 本身就是 IEnumerable<char>，必须先排除）
        if (t != typeof(string) && (t.IsArray ||
            (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) ||
            t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))))
        {
            return new List<object?>();
        }

        // 防环 / 深度保护
        if (depth >= 3 || !visiting.Add(t)) return new Dictionary<string, object?>();

        try
        {
            var obj = new Dictionary<string, object?>();
            foreach (var (name, memberType, nullability) in EnumerateJsonMembers(t))
            {
                // 可空成员（如 CreateUserRequest.City 的 `string?`、UserDto.UpdatedAt 的 `DateTime?`）→ null，
                // 与 ASP.NET Core 反序列化时"未提供该字段即为 null"的预期一致。
                var preferNull = nullability == NullabilityState.Nullable;
                obj[name] = BuildSkeletonValue(memberType, depth + 1, visiting, preferNull);
            }
            return obj;
        }
        finally
        {
            visiting.Remove(t);
        }
    }

    /// <summary>
    /// 列出某个复杂类型需要出现在 JSON 骨架里的成员（名称 + 类型 + 可空性，名称已转 camelCase）。
    /// record 的主构造函数参数会被包含（其属性往往是 init-only，只扫可写属性会漏）。
    /// </summary>
    private static IEnumerable<(string Name, Type Type, NullabilityState Nullability)> EnumerateJsonMembers(Type type)
    {
        var members = new List<(string Name, Type Type, NullabilityState Nullability)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nullabilityContext = new NullabilityInfoContext();

        // record / class 的主构造函数参数
        foreach (var ctor in type.GetConstructors())
        {
            foreach (var p in ctor.GetParameters())
            {
                if (!seen.Add(p.Name ?? "")) continue;
                var state = NullabilityState.Unknown;
                try { state = nullabilityContext.Create(p).WriteState; } catch { /* 反射拿不到注解时按 Unknown 处理 */ }
                members.Add((p.Name ?? "", p.ParameterType, state));
            }
            break;  // 只取第一个公共构造函数
        }

        // 公共可写属性（跳过已由构造函数覆盖的）
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            if (!prop.CanWrite && !prop.CanRead) continue;
            if (!seen.Add(prop.Name)) continue;
            var state = NullabilityState.Unknown;
            try { state = nullabilityContext.Create(prop).WriteState; } catch { /* 反射拿不到注解时按 Unknown 处理 */ }
            members.Add((prop.Name, prop.PropertyType, state));
        }

        return members.Select(m => (ToCamelCase(m.Name), m.Type, m.Nullability));
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) || char.IsLower(name[0])
            ? name
            : char.ToLowerInvariant(name[0]) + name[1..];
}

/// <summary>
/// 路由条目的可填写参数，序列化为 JSON 返回给前端。
/// </summary>
/// <param name="Name">参数名（Form/Query 的键名）</param>
/// <param name="Source">绑定源：form / query / header</param>
/// <param name="Type">类型的可读名</param>
/// <param name="Required">是否必填</param>
/// <param name="Default">预填值，空串时由前端回退默认表</param>
internal record ParamEntry(
    string Name,
    string Source,
    string Type,
    bool Required,
    string Default
);

/// <summary>
/// 复杂类型（[FromBody]）参数的 JSON 骨架，序列化为 JSON 返回给前端。
/// </summary>
/// <param name="Name">参数名</param>
/// <param name="Type">类型的可读名</param>
/// <param name="Json">预填到 JSON 文本框的骨架字符串</param>
internal record BodyEntry(
    string Name,
    string Type,
    string Json
);

/// <summary>
/// 内部路由条目 DTO，序列化为 JSON 返回给前端。
/// </summary>
/// <param name="Parameters">可填写的参数列表（仅写方法非空）</param>
/// <param name="BodyParam">复杂类型请求体的 JSON 骨架（无则为 null）</param>
/// <param name="UrlPattern">
/// 可直接用于发起请求的 URL 模板：参数名是**简单名**（<c>{id}</c>），
/// 不含路由约束（<c>{id:long}</c>）与默认值（<c>{id=1}</c>）。
/// <see cref="Path"/> 是路由原文、仅供展示，直接拿它发请求会 404。
/// </param>
internal record RouteEntry(
    string Group,
    string Method,
    string Path,
    string Desc,
    bool IsSse,
    List<ParamEntry> Parameters,
    BodyEntry? BodyParam,
    string UrlPattern
);
