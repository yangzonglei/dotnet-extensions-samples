using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using Yzl.Extensions.Samples.Mcp.WebClient.Models;

namespace Yzl.Extensions.Samples.Mcp.WebClient.Services;

/// <summary>
/// 使用 Claude API 进行自然语言交互，自动调用 MCP 工具完成用户请求。
/// </summary>
public class ClaudeChatService : IChatService
{
    private readonly McpClientService _mcpService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClaudeChatService> _logger;
    private readonly string _apiKey;
    private readonly string _apiUrl = "https://api.anthropic.com/v1/messages";
    private readonly string _model;

    // 会话消息历史（生产环境建议用缓存/数据库）
    private static readonly Dictionary<string, List<ConversationMessage>> Sessions = new();
    private static readonly object SessionLock = new();

    public ClaudeChatService(
        McpClientService mcpService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ClaudeChatService> logger)
    {
        _mcpService = mcpService;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;

        _apiKey = configuration.GetValue<string>("ANTHROPIC_API_KEY")
                  ?? configuration.GetValue<string>("Claude:ApiKey")
                  ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                  ?? string.Empty;

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        _model = configuration.GetValue<string>("Claude:Model") ?? "claude-sonnet-4-20250514";

        var urlOverride = configuration.GetValue<string>("Claude:ApiUrl");
        if (!string.IsNullOrEmpty(urlOverride))
            _apiUrl = urlOverride;
    }

    /// <summary>
    /// 检查 API Key 是否已配置
    /// </summary>
    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

    /// <summary>
    /// 处理用户消息，流式返回 AI 回复（含自动工具调用）
    /// </summary>
    public async IAsyncEnumerable<ChatResponseEvent> ProcessMessageAsync(
        string sessionId,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            yield return Error("未配置 Claude API Key。请在 appsettings.json 中设置 Claude:ApiKey 或环境变量 ANTHROPIC_API_KEY。");
            yield break;
        }

        // 保存用户消息到会话历史
        var history = GetOrCreateSession(sessionId);
        history.Add(new ConversationMessage { Role = "user", Content = userMessage });

        // 获取 MCP 工具列表
        var mcpTools = await TryGetToolsAsync(ct);
        if (mcpTools == null)
        {
            yield return Error("连接 MCP 服务失败，请检查 MCP 服务器是否已启动。");
            yield break;
        }

        // 转换为 Claude 工具格式
        var tools = mcpTools.Select(t => new LlmToolDefinition
        {
            Name = t.Name,
            Description = t.Description ?? "",
            InputSchema = t.JsonSchema,
        }).ToList();

        var systemPrompt = BuildSystemPrompt(tools);

        // 多轮工具调用循环
        for (var round = 0; round < 10; round++)
        {
            // --- 1. 调用 Claude API（非 yield 区域） ---
            var apiResult = await TryCallClaudeApiAsync(history, tools, systemPrompt, ct);
            if (apiResult.ErrorMessage != null)
            {
                if (apiResult.IsCancelled) yield break;
                yield return Error(apiResult.ErrorMessage);
                yield break;
            }
            var response = apiResult.Response!;

            // --- 2. yield 文本内容 ---
            foreach (var evt in response.TextEvents)
                yield return evt;

            // --- 3. 处理工具调用 ---
            if (response.ToolCalls.Count > 0)
            {
                // yield 工具调用事件
                foreach (var tc in response.ToolCalls)
                {
                    yield return new ChatResponseEvent
                    {
                        Type = "tool_call",
                        Content = $"调用工具: {tc.Name}",
                        ToolName = tc.Name,
                        ToolArgs = tc.Arguments,
                    };
                }

                // 保存 assistant 回复
                history.Add(new ConversationMessage
                {
                    Role = "assistant",
                    Content = response.Text,
                    ToolUses = response.ToolCalls,
                });

                // --- 4. 执行工具（非 yield 区域） ---
                var toolResults = await ExecuteToolsAsync(response.ToolCalls, ct);

                // yield 工具结果事件
                foreach (var tr in toolResults)
                {
                    yield return tr.Event;
                }

                // 将工具结果追加到历史
                history.Add(new ConversationMessage
                {
                    Role = "user",
                    Content = "",
                    ToolResults = toolResults.Select(r => r.Content).ToList(),
                });
            }
            else
            {
                // 纯文本回复 - 保存并结束
                history.Add(new ConversationMessage
                {
                    Role = "assistant",
                    Content = response.Text,
                });
                break;
            }
        }

        yield return new ChatResponseEvent { Type = "done" };
    }

    /// <summary>
    /// 调用 Claude API 并解析响应
    /// </summary>
    private async Task<ClaudeResponse> CallClaudeApiAsync(
        List<ConversationMessage> history,
        List<LlmToolDefinition> tools,
        string systemPrompt,
        CancellationToken ct)
    {
        var requestBody = BuildClaudeRequest(history, tools, systemPrompt);
        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false,
        });

        _logger.LogDebug("Claude API 请求: {Json}", json);

        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        var httpResponse = await _httpClient.PostAsync(_apiUrl, httpContent, ct);
        var responseBody = await httpResponse.Content.ReadAsStringAsync(ct);

        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Claude API 错误 ({Status}): {Body}",
                httpResponse.StatusCode, responseBody);
            throw new InvalidOperationException(
                $"Claude API 请求失败 ({httpResponse.StatusCode}): {responseBody}");
        }

        using var responseJson = JsonDocument.Parse(responseBody);
        var root = responseJson.RootElement;

        var stopReason = root.TryGetProperty("stop_reason", out var srEl)
            ? srEl.GetString() : null;

        var text = new StringBuilder();
        var toolCalls = new List<ToolUseContent>();

        if (root.TryGetProperty("content", out var contentArr))
        {
            foreach (var block in contentArr.EnumerateArray())
            {
                var type = block.GetProperty("type").GetString();
                switch (type)
                {
                    case "text":
                        text.Append(block.GetProperty("text").GetString());
                        break;

                    case "tool_use":
                        var toolUse = new ToolUseContent
                        {
                            Id = block.GetProperty("id").GetString() ?? "",
                            Name = block.GetProperty("name").GetString() ?? "",
                        };
                        if (block.TryGetProperty("input", out var inputEl))
                        {
                            toolUse.Arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                inputEl.GetRawText()) ?? [];
                        }
                        toolCalls.Add(toolUse);
                        break;
                }
            }
        }

        return new ClaudeResponse
        {
            Text = text.ToString(),
            ToolCalls = toolCalls,
            TextEvents = BuildTextEvents(text.ToString()),
        };
    }

    /// <summary>
    /// 执行工具调用
    /// </summary>
    private async Task<List<ToolExecutionResult>> ExecuteToolsAsync(
        List<ToolUseContent> toolCalls, CancellationToken ct)
    {
        var results = new List<ToolExecutionResult>();

        foreach (var tc in toolCalls)
        {
            try
            {
                var callArgs = tc.Arguments.ToDictionary(
                    kv => kv.Key, kv => kv.Value);

                var mcpResult = await _mcpService.CallToolAsync(tc.Name, callArgs);
                var resultText = ExtractText(mcpResult);

                results.Add(new ToolExecutionResult
                {
                    Content = new ToolResultContent
                    {
                        ToolUseId = tc.Id,
                        Content = resultText,
                    },
                    Event = new ChatResponseEvent
                    {
                        Type = "tool_result",
                        ToolName = tc.Name,
                        Content = resultText,
                    },
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "工具调用失败: {Tool}", tc.Name);
                results.Add(new ToolExecutionResult
                {
                    Content = new ToolResultContent
                    {
                        ToolUseId = tc.Id,
                        Content = $"错误: {ex.Message}",
                    },
                    Event = new ChatResponseEvent
                    {
                        Type = "tool_result",
                        ToolName = tc.Name,
                        Content = $"❌ {ex.Message}",
                    },
                });
            }
        }

        return results;
    }

    private static List<ChatResponseEvent> BuildTextEvents(string fullText)
    {
        if (string.IsNullOrEmpty(fullText))
            return [];

        // 将完整文本作为单个事件发送，简化处理
        return
        [
            new ChatResponseEvent
            {
                Type = "text",
                Content = fullText,
            }
        ];
    }

    private static ChatResponseEvent Error(string message) =>
        new() { Type = "error", Content = message };

    /// <summary>
    /// 构建系统提示
    /// </summary>
    private static string BuildSystemPrompt(List<LlmToolDefinition> tools)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是一个智能助手，可以通过调用工具来帮助用户完成任务。");
        sb.AppendLine();
        sb.AppendLine("## 可用工具");
        foreach (var tool in tools)
        {
            sb.AppendLine($"- **{tool.Name}**: {tool.Description ?? "无描述"}");
        }
        sb.AppendLine();
        sb.AppendLine("根据用户的问题，选择合适的工具并正确传入参数。");
        sb.AppendLine("如果不需要工具，直接回答用户问题。");
        sb.AppendLine("请用中文回复用户。");
        return sb.ToString();
    }

    /// <summary>
    /// 构建 Claude API 的请求体
    /// </summary>
    private Dictionary<string, object> BuildClaudeRequest(
        List<ConversationMessage> history,
        List<LlmToolDefinition> tools,
        string systemPrompt)
    {
        var messages = new List<Dictionary<string, object>>();

        foreach (var msg in history)
        {
            if (msg.ToolUses.Count == 0 && msg.ToolResults.Count == 0)
            {
                messages.Add(new Dictionary<string, object>
                {
                    ["role"] = msg.Role,
                    ["content"] = msg.Content,
                });
                continue;
            }

            if (msg.ToolUses.Count > 0)
            {
                var content = new List<object>();
                if (!string.IsNullOrEmpty(msg.Content))
                {
                    content.Add(new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["text"] = msg.Content,
                    });
                }
                foreach (var tu in msg.ToolUses)
                {
                    content.Add(new Dictionary<string, object>
                    {
                        ["type"] = "tool_use",
                        ["id"] = tu.Id,
                        ["name"] = tu.Name,
                        ["input"] = tu.Arguments,
                    });
                }
                messages.Add(new Dictionary<string, object>
                {
                    ["role"] = "assistant",
                    ["content"] = content,
                });
                continue;
            }

            if (msg.ToolResults.Count > 0)
            {
                var content = new List<object>();
                foreach (var tr in msg.ToolResults)
                {
                    content.Add(new Dictionary<string, object>
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = tr.ToolUseId,
                        ["content"] = tr.Content,
                    });
                }
                messages.Add(new Dictionary<string, object>
                {
                    ["role"] = "user",
                    ["content"] = content,
                });
            }
        }

        var toolDefs = tools.Select(t => (object)new Dictionary<string, object>
        {
            ["name"] = t.Name,
            ["description"] = t.Description,
            ["input_schema"] = t.InputSchema,
        }).ToList();

        return new Dictionary<string, object>
        {
            ["model"] = _model,
            ["max_tokens"] = 4096,
            ["system"] = systemPrompt,
            ["messages"] = messages,
            ["tools"] = toolDefs,
        };
    }

    /// <summary>
    /// 从 CallToolResult 中提取文本内容
    /// </summary>
    private static string ExtractText(ModelContextProtocol.Protocol.CallToolResult result)
    {
        if (result.Content == null || result.Content.Count == 0)
            return "(无返回内容)";

        var sb = new StringBuilder();
        foreach (var block in result.Content)
        {
            if (block is ModelContextProtocol.Protocol.TextContentBlock text)
                sb.AppendLine(text.Text);
            else
                sb.AppendLine(JsonSerializer.Serialize(block));
        }

        var output = sb.ToString().Trim();
        return string.IsNullOrEmpty(output) ? "(空结果)" : output;
    }

    /// <summary>
    /// 获取或创建会话历史
    /// </summary>
    private static List<ConversationMessage> GetOrCreateSession(string sessionId)
    {
        lock (SessionLock)
        {
            if (!Sessions.TryGetValue(sessionId, out var history))
            {
                history = [];
                Sessions[sessionId] = history;
            }
            return history;
        }
    }

    /// <summary>
    /// 获取会话历史（用于 API 查询）
    /// </summary>
    public List<object>? GetSessionHistory(string sessionId)
    {
        lock (SessionLock)
        {
            if (!Sessions.TryGetValue(sessionId, out var history))
                return null;

            return history.Select(m => (object)new
            {
                role = m.Role,
                content = m.Content,
                toolCalls = m.ToolUses.Select(t => new { t.Id, t.Name, t.Arguments }).ToList(),
            }).ToList();
        }
    }

    /// <summary>
    /// 清除会话历史
    /// </summary>
    public void ClearSession(string sessionId)
    {
        lock (SessionLock)
        {
            Sessions.Remove(sessionId);
        }
    }

    // ============================================================
    // 内部类型
    // ============================================================

    /// <summary>
    /// 获取 MCP 工具列表（安全包装，不抛异常）
    /// </summary>
    private async Task<List<McpClientTool>?> TryGetToolsAsync(CancellationToken ct)
    {
        try
        {
            return await _mcpService.ListToolsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 MCP 工具列表失败");
            return null;
        }
    }

    /// <summary>
    /// 调用 Claude API（安全包装，不抛异常，不 yield）
    /// </summary>
    private async Task<ApiCallResult> TryCallClaudeApiAsync(
        List<ConversationMessage> history,
        List<LlmToolDefinition> tools,
        string systemPrompt,
        CancellationToken ct)
    {
        try
        {
            var response = await CallClaudeApiAsync(history, tools, systemPrompt, ct);
            return ApiCallResult.Success(response);
        }
        catch (OperationCanceledException)
        {
            return ApiCallResult.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claude API 调用异常");
            return ApiCallResult.Error($"Claude API 调用异常: {ex.Message}");
        }
    }

    private class ClaudeResponse
    {
        public string Text { get; set; } = "";
        public List<ToolUseContent> ToolCalls { get; set; } = [];
        public List<ChatResponseEvent> TextEvents { get; set; } = [];
    }

    private class ApiCallResult
    {
        public ClaudeResponse? Response { get; private set; }
        public string? ErrorMessage { get; private set; }
        public bool IsCancelled { get; private set; }

        public static ApiCallResult Success(ClaudeResponse r) =>
            new() { Response = r };
        public static ApiCallResult Cancelled() =>
            new() { IsCancelled = true, ErrorMessage = "请求已取消" };
        public static ApiCallResult Error(string msg) =>
            new() { ErrorMessage = msg };
    }

    private class ToolExecutionResult
    {
        public ToolResultContent Content { get; set; } = new();
        public ChatResponseEvent Event { get; set; } = new();
    }
}
