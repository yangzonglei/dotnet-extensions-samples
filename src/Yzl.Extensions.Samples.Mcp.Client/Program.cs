using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

Console.WriteLine("""
                  ╔═══════════════════════════════════════════════════════╗
                  ║      Yzl.Extensions.AI.Mcp 客户端测试               ║
                  ║                                                      ║
                  ║     控制台交互式 MCP 工具调用演示                   ║
                  ║     JWT 认证 · 工具列表 · 参数交互输入              ║
                  ║                                                      ║
                  ║     前置依赖: Mcp.Service (端口 16607)               ║
                  ╚═══════════════════════════════════════════════════════╝
                  """);

// ===== 命令行参数解析 =====
// 用法: dotnet run [mcpUrl] [tokenEndpoint] [clientId] [clientSecret]
// dotnet run --project src/Yzl.Extensions.Samples.Mcp.Client/
var mcpUrl = GetArg(0) ?? "http://localhost:16607/mcp";
var tokenEndpoint = GetArg(1) ?? "http://localhost:16607/api/auth/token";
var clientId = GetArg(2) ?? "mcp-client";
var clientSecret = GetArg(3) ?? "mcp-secret";

Console.OutputEncoding = System.Text.Encoding.UTF8;

// 创建日志工厂
using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    }).SetMinimumLevel(LogLevel.Warning));

// ===== 1. 获取 JWT Token =====
Console.WriteLine($"正在获取 JWT Token: {tokenEndpoint}");
using var authHttpClient = new HttpClient();
var tokenRequest = new TokenRequest { ClientId = clientId, ClientSecret = clientSecret };
var tokenResponse = await authHttpClient.PostAsJsonAsync(tokenEndpoint, tokenRequest);
tokenResponse.EnsureSuccessStatusCode();

var tokenResult = await tokenResponse.Content.ReadFromJsonAsync<TokenResponse>();
var accessToken = tokenResult?.AccessToken ?? string.Empty;
Console.WriteLine($"Token 获取成功，有效期: {tokenResult?.ExpiresIn ?? 0}s\n");

// ===== 2. 创建 HTTP 传输（携带 JWT Token） =====
var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri(mcpUrl),
    TransportMode = HttpTransportMode.StreamableHttp,
    AdditionalHeaders = new Dictionary<string, string>
    {
        { "Authorization", $"Bearer {accessToken}" }
    }
}, loggerFactory);

// ===== 3. 创建 MCP 客户端 =====
Console.WriteLine($"正在连接到 MCP 服务: {mcpUrl}");
await using var client = await McpClient.CreateAsync(
    transport,
    new McpClientOptions
    {
        ClientInfo = new Implementation { Name = "Yzl MCP Console Client", Version = "1.0.0" },
    },
    loggerFactory);

Console.WriteLine($"连接成功！服务器: {client.ServerInfo?.Name ?? "Unknown"} v{client.ServerInfo?.Version ?? "?"}");
Console.WriteLine();

// ===== 4. 列出所有可用工具 =====
Console.WriteLine("正在获取可用工具列表...");
var tools = await client.ListToolsAsync();

if (tools.Count == 0)
{
    Console.WriteLine("⚠️  服务器没有注册任何工具。");
    return;
}

Console.WriteLine($"找到 {tools.Count} 个工具：\n");
foreach (var tool in tools)
{
    Console.WriteLine($"  ┌─ {tool.Name}");
    if (!string.IsNullOrEmpty(tool.Description))
        Console.WriteLine($"  │  {tool.Description}");
    Console.WriteLine($"  │  输入 Schema: {tool.JsonSchema}");
    Console.WriteLine();
}

// ===== 5. 交互式调用演示 =====
Console.WriteLine("═══════════════════════════════════════");
Console.WriteLine("交互式工具调用演示");
Console.WriteLine("═══════════════════════════════════════\n");

while (true)
{
    Console.Write("输入工具名 (或 'exit' 退出, 'list' 重新列出): ");
    var input = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(input) || input == "exit") break;

    if (input == "list")
    {
        foreach (var t in tools)
            Console.WriteLine($"  {t.Name} - {t.Description}");
        continue;
    }

    var tool = tools.FirstOrDefault(t =>
        t.Name.Equals(input, StringComparison.OrdinalIgnoreCase));
    if (tool == null)
    {
        Console.WriteLine($"❌ 未找到工具: {input}\n");
        continue;
    }

    // 解析参数（交互式输入）
    var argsDict = new Dictionary<string, object?>();
    var schema = tool.JsonSchema;

    if (schema.ValueKind == JsonValueKind.Object)
    {
        if (schema.TryGetProperty("properties", out var properties) &&
            properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in properties.EnumerateObject())
            {
                Console.Write($"  参数 [{prop.Name}]: ");
                var valueInput = Console.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(valueInput))
                {
                    argsDict[prop.Name] = InferValue(valueInput, prop.Value);
                }
            }
        }
    }

    Console.WriteLine($"\n  调用工具 '{tool.Name}'...");
    try
    {
        var result = await tool.CallAsync(
            argsDict.Count > 0 ? argsDict : null);
        PrintResult(result);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ❌ 调用失败: {ex.Message}");
    }
    Console.WriteLine();
}

Console.WriteLine("👋 再见！");

// ==============================================================
// 辅助方法
// ==============================================================

string? GetArg(int index) =>
    index < args.Length && !string.IsNullOrEmpty(args[index]) ? args[index] : null;

static object? InferValue(string input, JsonElement schemaProp)
{
    if (int.TryParse(input, out var intVal)) return intVal;
    if (long.TryParse(input, out var longVal)) return longVal;
    if (double.TryParse(input, out var dVal)) return dVal;
    if (bool.TryParse(input, out var bVal)) return bVal;
    if (input is "null" or "") return null;

    if (schemaProp.ValueKind == JsonValueKind.Object &&
        schemaProp.TryGetProperty("type", out var typeEl))
    {
        var typeName = typeEl.GetString();
        if (typeName == "integer" && long.TryParse(input, out var l2)) return l2;
        if (typeName == "number" && double.TryParse(input, out var d2)) return d2;
        if (typeName == "boolean")
        {
            if (bool.TryParse(input, out var b2)) return b2;
            return input is "1" or "yes" or "是";
        }
    }

    return input;
}

static void PrintResult(CallToolResult result)
{
    if (result.IsError == true)
    {
        Console.WriteLine("  ❌ 工具返回错误:");
        foreach (var block in result.Content ?? [])
        {
            if (block is TextContentBlock text)
                Console.WriteLine($"    {text.Text}");
        }
        return;
    }

    Console.WriteLine("  ✅ 结果:");
    foreach (var block in result.Content ?? [])
    {
        if (block is TextContentBlock text)
            Console.WriteLine($"    {text.Text}");
        else
            Console.WriteLine($"    [{block.Type}] {JsonSerializer.Serialize(block)}");
    }
}

// ==============================================================
// DTO
// ==============================================================

internal sealed record TokenRequest
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; init; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; init; } = string.Empty;
}

internal sealed record TokenResponse
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("tokenType")]
    public string TokenType { get; init; } = string.Empty;

    [JsonPropertyName("expiresIn")]
    public int ExpiresIn { get; init; }
}
