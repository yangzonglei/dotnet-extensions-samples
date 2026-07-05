using Microsoft.AspNetCore.Http;
using Yzl.Extensions.Actuator.Abstractions;

namespace Yzl.Extensions.Samples.Actuator.Custom;

/// <summary>
/// 自定义 Actuator Endpoint，演示如何创建自定义端点并自动注册到 Actuator。
///
/// 实现了 IActuatorEndpoint 接口后，AddSpringNetActuator 会自动扫描并将其注册到 DI，
/// ActuatorEndpointMapper 会将其映射为 /actuator/custom 端点。
///
/// 访问 /actuator/custom 查看输出。
/// </summary>
public sealed class CustomActuatorEndpoint : IActuatorEndpoint
{
    public string Id => "custom";

    public IReadOnlyCollection<ActuatorOperationDescriptor> Operations { get; }

    public CustomActuatorEndpoint()
    {
        Operations =
        [
            new ActuatorOperationDescriptor
            {
                Id = Id,
                HttpMethod = HttpMethod.Get,
                PathTemplate = "",
                Handler = HandleAsync
            }
        ];
    }

    private Task HandleAsync(HttpContext ctx)
    {
        var response = new
        {
            id = "custom",
            name = "自定义端点",
            description = "这是一个通过实现 IActuatorEndpoint 接口自动注册的自定义 Actuator 端点",
            timestamp = DateTime.UtcNow.ToString("O"),
            examples = new[]
            {
                new { path = "/actuator/custom", method = "GET", description = "自定义端点入口" }
            },
            links = new
            {
                self = new { href = "/actuator/custom" },
                health = new { href = "/actuator/health" },
                info = new { href = "/actuator/info" }
            }
        };

        return WriteJsonResponse(ctx, response);
    }

    private static Task WriteJsonResponse(HttpContext ctx, object data)
    {
        ctx.Response.ContentType = "application/vnd.spring-boot.actuator.v2+json;charset=utf-8";
        return System.Text.Json.JsonSerializer.SerializeAsync(ctx.Response.Body, data, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
    }
}
