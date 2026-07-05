using System.Text.Json;
using Yzl.Extensions.Samples.Mcp.WebClient.Services;

Console.WriteLine("""
                  ╔═══════════════════════════════════════════════════════╗
                  ║     Yzl.Extensions.AI.Mcp Web 客户端测试            ║
                  ║                                                      ║
                  ║     访问: http://localhost:16608                      ║
                  ║                                                      ║
                  ║     MCP 工具调用 + 智能聊天 UI                      ║
                  ║     SSE 流式回复 · 自动工具调用 · LLM 集成          ║
                  ║                                                      ║
                  ║     前置依赖: Mcp.Service (端口 16607)               ║
                  ╚═══════════════════════════════════════════════════════╝
                  """);

var builder = WebApplication.CreateBuilder(args);

// 注册 MCP 客户端服务（单例）
builder.Services.AddSingleton<McpClientService>();

// 根据配置注册 LLM 聊天服务（Claude / OpenAI 兼容）
var llmProvider = builder.Configuration.GetValue<string>("LlmProvider") ?? "Claude";
if (llmProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IChatService, OpenAiChatService>();
    Console.WriteLine($"[LlmProvider] 使用 OpenAI 兼容格式 (已启用)");
}
else
{
    builder.Services.AddSingleton<IChatService, ClaudeChatService>();
    Console.WriteLine($"[LlmProvider] 使用 Claude API (已启用)");
}

// 注册 HttpClientFactory
builder.Services.AddHttpClient();

// CORS（允许本地开发）
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();
app.UseStaticFiles();

// ============================================================
// API: 获取 MCP 服务器上可用的工具列表
// ============================================================
app.MapGet("/api/tools", async (McpClientService mcpService) =>
{
    try
    {
        var tools = await mcpService.ListToolsAsync();
        return Results.Ok(tools.Select(t => new
        {
            t.Name,
            t.Description,
            Schema = t.JsonSchema,
        }));
    }
    catch (Exception ex)
    {
        return Results.Problem($"无法连接 MCP 服务器: {ex.Message}");
    }
});

// ============================================================
// API: 手动调用 MCP 工具
// ============================================================
app.MapPost("/api/tools/call", async (
    McpClientService mcpService,
    ToolCallRequest request) =>
{
    try
    {
        var args = request.Arguments?.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
        var result = await mcpService.CallToolAsync(request.ToolName, args);
        var text = string.Join("\n", (result.Content ?? [])
            .OfType<ModelContextProtocol.Protocol.TextContentBlock>()
            .Select(b => b.Text));
        return Results.Ok(new { content = text, isError = result.IsError });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { content = $"错误: {ex.Message}", isError = true });
    }
});

// ============================================================
// API: 智能聊天（SSE 流式回复 + 自动工具调用）
// ============================================================
app.MapPost("/api/chat", async (
    HttpContext context,
    IChatService chatService) =>
{
    var request = await context.Request.ReadFromJsonAsync<ChatRequest>(
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    request ??= new ChatRequest();

    if (string.IsNullOrEmpty(request.SessionId))
        request.SessionId = Guid.NewGuid().ToString("N");

    if (string.IsNullOrWhiteSpace(request.Message))
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { error = "消息不能为空" });
        return;
    }

    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";

    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    try
    {
        await foreach (var evt in chatService.ProcessMessageAsync(
            request.SessionId, request.Message, context.RequestAborted))
        {
            var json = JsonSerializer.Serialize(evt, jsonOptions);
            await context.Response.WriteAsync($"data: {json}\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
    }
    catch (OperationCanceledException)
    {
        // 客户端断开连接
    }
    catch (Exception ex)
    {
        var err = new { type = "error", content = $"服务器错误: {ex.Message}" };
        var errJson = JsonSerializer.Serialize(err);
        await context.Response.WriteAsync($"data: {errJson}\n\n");
    }
});

// ============================================================
// API: 获取聊天会话历史
// ============================================================
app.MapGet("/api/sessions/{sessionId}", (string sessionId, IChatService chatService) =>
{
    var history = chatService.GetSessionHistory(sessionId);
    if (history == null)
        return Results.NotFound(new { error = "会话不存在" });
    return Results.Ok(history);
});

// ============================================================
// API: 清除会话历史
// ============================================================
app.MapDelete("/api/sessions/{sessionId}", (string sessionId, IChatService chatService) =>
{
    chatService.ClearSession(sessionId);
    return Results.Ok(new { message = "会话已清除" });
});

// 默认页面指向 index.html
app.MapFallbackToFile("index.html");

app.Run();

// ============================================================
// 请求模型
// ============================================================
public record ToolCallRequest(string ToolName, Dictionary<string, object>? Arguments);

public record ChatRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
