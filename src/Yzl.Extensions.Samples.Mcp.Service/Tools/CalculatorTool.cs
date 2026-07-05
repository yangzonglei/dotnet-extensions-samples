using Yzl.Extensions.AI.Mcp.Attributes;

namespace Yzl.Extensions.Samples.Mcp.Service.Tools;

/// <summary>
/// 数学计算工具，演示 [McpTool] 类级别标注 + DI 注入。
/// </summary>
[McpTool(Name = "calculator", Description = "数学计算工具")]
public class CalculatorTool(ILogger<CalculatorTool> logger)
{
    [McpTool(Name = "add", Description = "两数相加")]
    public int Add(
        [McpParam(Description = "第一个数")] int a,
        [McpParam(Description = "第二个数")] int b)
    {
        logger.LogInformation("Add({A}, {B}) = {Result}", a, b, a + b);
        return a + b;
    }

    [McpTool(Name = "subtract", Description = "两数相减")]
    public int Subtract(
        [McpParam(Description = "被减数")] int a,
        [McpParam(Description = "减数")] int b)
    {
        logger.LogInformation("Subtract({A}, {B}) = {Result}", a, b, a - b);
        return a - b;
    }

    [McpTool(Name = "echo", Description = "返回用户输入的信息，可重复多次")]
    public string Echo(
        [McpParam(Description = "要返回的文本")] string message,
        [McpParam(Description = "重复次数（默认1次）")] int count = 1)
    {
        return string.Concat(Enumerable.Repeat(message, count));
    }
}
