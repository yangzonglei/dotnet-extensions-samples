using Yzl.Extensions.AI.Mcp.Attributes;

namespace Yzl.Extensions.Samples.Mcp.Service.Tools;

/// <summary>
/// 时间工具，演示 async / ValueTask / 无参数方法 / DateTime 类型。
/// </summary>
[McpTool]
public class TimeTool(ILogger<TimeTool> logger)
{
    [McpTool(Name = "get_current_time", Description = "获取当前服务器时间")]
    public async ValueTask<string> GetCurrentTime()
    {
        logger.LogInformation("Getting current time");
        await Task.Delay(10); // 模拟异步操作
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    [McpTool(Name = "days_until", Description = "计算到指定日期的天数")]
    public int DaysUntil(
        [McpParam(Description = "目标日期（格式：yyyy-MM-dd）")] DateTime target)
    {
        return (target.Date - DateTime.Now.Date).Days;
    }

    [McpTool(Name = "is_weekend", Description = "判断指定日期是否为周末")]
    public bool IsWeekend(
        [McpParam(Description = "要判断的日期（格式：yyyy-MM-dd）")] DateTime date)
    {
        return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }
}
