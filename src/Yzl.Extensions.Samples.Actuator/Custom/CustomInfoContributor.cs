using Yzl.Extensions.Actuator.Endpoints.Info;

namespace Yzl.Extensions.Samples.Actuator.Custom;

/// <summary>
/// 自定义 Info Contributor，演示如何为 /actuator/info 添加自定义信息片段。
///
/// 注册方式：
/// <code>
/// builder.Services.AddSingleton&lt;IInfoContributor, CustomInfoContributor&gt;();
/// </code>
///
/// 访问 /actuator/info 可在响应中看到 custom 信息片段。
/// </summary>
public class CustomInfoContributor : IInfoContributor
{
    public string Name => "custom";

    public Task<IDictionary<string, object>> ContributeAsync()
    {
        var info = new Dictionary<string, object>
        {
            ["team"] = "Yzl.Extensions Team",
            ["project"] = "Yzl.Extensions.Actuator Samples",
            ["version"] = "1.0.0",
            ["contact"] = "developer@example.com",
            ["repository"] = "https://github.com/yangzonglei/dotnet-extensions",
            ["buildTime"] = DateTime.UtcNow.ToString("O"),
            ["features"] = new[]
            {
                "Actuator 健康检查",
                "Actuator 指标采集",
                "Actuator 环境信息",
                "Actuator 日志管理",
                "Actuator Beans 查看",
                "Actuator 缓存管理"
            }
        };

        return Task.FromResult<IDictionary<string, object>>(info);
    }
}
