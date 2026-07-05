using Yzl.Extensions.AI.Mcp.Attributes;
using Samples.Models;
using Yzl.Extensions.Samples.Mcp.Service.Acs;

namespace Yzl.Extensions.Samples.Mcp.Service.Tools;

[McpTool(Name = "CustomTool", Description = "自定义工具")]
public class CustomTool(ILogger<CustomTool> logger, ITestApiFeignClient client)
{
    [McpTool(Name = "age", Description = "获取年龄")]
    public string GetAge(
        [McpParam(Description = "您怎么称呼")] string name)
    {
        logger.LogInformation("GetName-->{Result}", name);
        return $"{name}，永远 18哦";
    }

    [McpTool(Name = "GetByIdAsync", Description = "异步获取年纪")]
    public Task<UserDto> GetByIdAsync(int id)
    {
        var data = client.GetByIdAsync(id);
        return data;
    }

    [McpTool(Name = "GetById", Description = "获取年纪")]
    public UserDto GetById(int id)
    {
        var data = client.GetById(id);
        return data;
    }
}
