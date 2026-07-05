using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using Yzl.Extensions.Samples.Mcp.WebClient.Models;

namespace Yzl.Extensions.Samples.Mcp.WebClient.Services;

/// <summary>
/// 使用 OpenAI 兼容格式的 LLM 进行自然语言交互，自动调用 MCP 工具完成用户请求。
/// 支持 DeepSeek、通义千问、GLM-4 等国内模型。
/// </summary>
public class OpenAiChatService : IChatService
{
    private readonly McpClientService _mcpService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiChatService> _logger;
    private readonly string _apiKey;
    private readonly string _apiUrl;
    private readonly string _model;

    // 会话消息历史（生产环境建议用缓存/数据库）
    private static readonly Dictionary<string, List<OpenAiMessage>> Sessions = new();
    private static readonly object SessionLock = new();

    public OpenAiChatService(
        McpClientService mcpService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OpenAiChatService> logger)
    {
        _mcpService = mcpService;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;

        _apiKey = configuration.GetValue<string>("OpenAi:ApiKey")
                  ?? configuration.GetValue<string>("LLM:ApiKey")
                  ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                  ?? string.Empty;

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        _model = configuration.GetValue<string>("OpenAi:Model")
                 ?? configuration.GetValue<string>("LLM:Model")
                 ?? "deepseek-chat";

        _apiUrl = configuration.GetValue<string>("OpenAi:ApiUrl")
                  ?? configuration.GetValue<string>("LLM:ApiUrl")
                  ?? "https://api.deepseek.com/v1/chat/completions";
    }

    /// <inheritdoc />
    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseEvent> ProcessMessageAsync(
        string sessionId,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            yield return Error("未配置 LLM API Key。请在 appsettings.json 中设置 OpenAi:ApiKey 或环境变量 OPENAI_API_KEY。");
            yield break;
        }

        // 保存用户消息到会话历史
        var history = GetOrCreateSession(sessionId);
        history.Add(new OpenAiMessage { Role = "user", Content = userMessage });

        // 获取 MCP 工具列表
        var mcpTools = await TryGetToolsAsync(ct);
        if (mcpTools == null)
        {
            yield return Error("连接 MCP 服务失败，请检查 MCP 服务器是否已启动。");
            yield break;
        }

        // 转换为 OpenAI 工具格式
        var tools = mcpTools.Select(t => new OpenAiToolDefinition
        {
            Type = "function",
            Function = new OpenAiFunctionDefinition
            {
                Name = t.Name,
                Description = t.Description ?? "",
                Parameters = t.JsonSchema,
            },
        }).ToList();

        var systemPrompt = BuildSystemPrompt(tools);

        // 多轮工具调用循环
        for (var round = 0; round < 10; round++)
        {
            // --- 1. 调用 LLM API ---
            var apiResult = await TryCallOpenAiApiAsync(history, tools, systemPrompt, ct);
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

                history.Add(new OpenAiMessage
                {
                    Role = "assistant",
                    Content = response.Text,
                    ToolCalls = response.ToolCalls,
                });

                // --- 4. 执行工具 ---
                var toolResults = await ExecuteToolsAsync(response.ToolCalls, ct);

                foreach (var tr in toolResults)
                {
                    yield return tr.Event;
                }

                history.Add(new OpenAiMessage
                {
                    Role = "tool",
                    Content = "",
                    ToolResults = toolResults.Select(r => r.Content).ToList(),
                });
            }
            else
            {
                // 纯文本回复 - 保存并结束
                history.Add(new OpenAiMessage
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
    /// 调用 OpenAI 兼容 API 并解析响应
    /// </summary>
    private async Task<OpenAiResponse> CallOpenAiApiAsync(
        List<OpenAiMessage> history,
        List<OpenAiToolDefinition> tools,
        string systemPrompt,
        CancellationToken ct)
    {
        var requestBody = BuildOpenAiRequest(history, tools, systemPrompt);
        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        });

        _logger.LogDebug("OpenAI API 请求: {Json}", json);

        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        var httpResponse = await _httpClient.PostAsync(_apiUrl, httpContent, ct);
        var responseBody = await httpResponse.Content.ReadAsStringAsync(ct);

        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogError("LLM API 错误 ({Status}): {Body}",
                httpResponse.StatusCode, responseBody);
            throw new InvalidOperationException(
                $"LLM API 请求失败 ({httpResponse.StatusCode}): {responseBody}");
        }

        using var responseJson = JsonDocument.Parse(responseBody);
        var root = responseJson.RootElement;

        // 解析 OpenAI 响应格式
        var choices = root.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
            throw new InvalidOperationException("API 返回了空的 choices");

        var message = choices[0].GetProperty("message");

        var text = message.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String
            ? contentEl.GetString() ?? ""
            : "";

        var toolCalls = new List<ToolUseContent>();
        if (message.TryGetProperty("tool_calls", out var toolCallsEl))
        {
            foreach (var tc in toolCallsEl.EnumerateArray())
            {
                var function = tc.GetProperty("function");
                var argumentsStr = function.GetProperty("arguments").GetString() ?? "{}";

                var toolUse = new ToolUseContent
                {
                    Id = tc.GetProperty("id").GetString() ?? "",
                    Name = function.GetProperty("name").GetString() ?? "",
                    Arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsStr) ?? [],
                };
                toolCalls.Add(toolUse);
            }
        }

        return new OpenAiResponse
        {
            Text = text,
            ToolCalls = toolCalls,
            TextEvents = BuildTextEvents(text),
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
    private static string BuildSystemPrompt(List<OpenAiToolDefinition> tools)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是一个智能助手，可以通过调用工具来帮助用户完成任务。");
        sb.AppendLine();
        sb.AppendLine("## 可用工具");
        foreach (var tool in tools)
        {
            sb.AppendLine($"- **{tool.Function.Name}**: {tool.Function.Description ?? "无描述"}");
        }
        sb.AppendLine();
        sb.AppendLine("根据用户的问题，选择合适的工具并正确传入参数。");
        sb.AppendLine("如果不需要工具，直接回答用户问题。");
        sb.AppendLine("请用中文回复用户。");
        return sb.ToString();
    }

    /// <summary>
    /// 构建 OpenAI 兼容的请求体
    /// </summary>
    private Dictionary<string, object> BuildOpenAiRequest(
        List<OpenAiMessage> history,
        List<OpenAiToolDefinition> tools,
        string systemPrompt)
    {
        var messages = new List<Dictionary<string, object>>();

        // OpenAI 格式：system 放在 messages[0]
        messages.Add(new Dictionary<string, object>
        {
            ["role"] = "system",
            ["content"] = systemPrompt,
        });

        foreach (var msg in history)
        {
            if (msg.Role == "user" && msg.ToolResults.Count == 0)
            {
                // 普通用户消息
                messages.Add(new Dictionary<string, object>
                {
                    ["role"] = "user",
                    ["content"] = msg.Content,
                });
                continue;
            }

            if (msg.Role == "user" && msg.ToolResults.Count > 0)
            {
                // 纯文本用户消息 + 工具结果作为 content 数组
                var content = new List<object>();
                if (!string.IsNullOrEmpty(msg.Content))
                {
                    content.Add(new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["text"] = msg.Content,
                    });
                }
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
                continue;
            }

            if (msg.Role == "tool" && msg.ToolResults.Count > 0)
            {
                // OpenAI 格式：工具结果用 role: "tool"
                foreach (var tr in msg.ToolResults)
                {
                    messages.Add(new Dictionary<string, object>
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = tr.ToolUseId,
                        ["content"] = tr.Content,
                    });
                }
                continue;
            }

            if (msg.Role == "assistant" && msg.ToolCalls.Count == 0)
            {
                // 纯文本 assistant 回复
                messages.Add(new Dictionary<string, object>
                {
                    ["role"] = "assistant",
                    ["content"] = msg.Content,
                });
                continue;
            }

            if (msg.Role == "assistant" && msg.ToolCalls.Count > 0)
            {
                // 带工具调用的 assistant 回复
                var toolCallsList = new List<Dictionary<string, object>>();
                foreach (var tc in msg.ToolCalls)
                {
                    toolCallsList.Add(new Dictionary<string, object>
                    {
                        ["id"] = tc.Id,
                        ["type"] = "function",
                        ["function"] = new Dictionary<string, object>
                        {
                            ["name"] = tc.Name,
                            ["arguments"] = JsonSerializer.Serialize(tc.Arguments),
                        },
                    });
                }

                var assistantMsg = new Dictionary<string, object>
                {
                    ["role"] = "assistant",
                    ["content"] = string.IsNullOrEmpty(msg.Content) ? null : msg.Content,
                    ["tool_calls"] = toolCallsList,
                };

                messages.Add(assistantMsg);
                continue;
            }
        }

        // 工具定义（OpenAI 格式）
        var toolDefs = tools.Select(t => (object)new Dictionary<string, object>
        {
            ["type"] = "function",
            ["function"] = new Dictionary<string, object>
            {
                ["name"] = t.Function.Name,
                ["description"] = t.Function.Description,
                ["parameters"] = t.Function.Parameters,
            },
        }).ToList();

        return new Dictionary<string, object>
        {
            ["model"] = _model,
            ["max_tokens"] = 4096,
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
    private static List<OpenAiMessage> GetOrCreateSession(string sessionId)
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

    /// <inheritdoc />
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
                toolCalls = m.ToolCalls.Select(t => new { t.Id, t.Name, t.Arguments }).ToList(),
            }).ToList();
        }
    }

    /// <inheritdoc />
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
    /// 调用 OpenAI API（安全包装，不抛异常，不 yield）
    /// </summary>
    private async Task<ApiCallResult> TryCallOpenAiApiAsync(
        List<OpenAiMessage> history,
        List<OpenAiToolDefinition> tools,
        string systemPrompt,
        CancellationToken ct)
    {
        try
        {
            var response = await CallOpenAiApiAsync(history, tools, systemPrompt, ct);
            return ApiCallResult.Success(response);
        }
        catch (OperationCanceledException)
        {
            return ApiCallResult.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM API 调用异常");
            return ApiCallResult.Error($"LLM API 调用异常: {ex.Message}");
        }
    }

    private class ApiCallResult
    {
        public OpenAiResponse? Response { get; private set; }
        public string? ErrorMessage { get; private set; }
        public bool IsCancelled { get; private set; }

        public static ApiCallResult Success(OpenAiResponse r) =>
            new() { Response = r };
        public static ApiCallResult Cancelled() =>
            new() { IsCancelled = true, ErrorMessage = "请求已取消" };
        public static ApiCallResult Error(string msg) =>
            new() { ErrorMessage = msg };
    }

    private class OpenAiResponse
    {
        public string Text { get; set; } = "";
        public List<ToolUseContent> ToolCalls { get; set; } = [];
        public List<ChatResponseEvent> TextEvents { get; set; } = [];
    }

    private class ToolExecutionResult
    {
        public ToolResultContent Content { get; set; } = new();
        public ChatResponseEvent Event { get; set; } = new();
    }
}

// ============================================================
// OpenAI 兼容格式的模型类型
// ============================================================

/// <summary>
/// OpenAI 格式的消息（支持 system/user/assistant/tool 角色）
/// </summary>
internal class OpenAiMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    /// <summary>工具调用（assistant 角色）</summary>
    public List<ToolUseContent> ToolCalls { get; set; } = [];

    /// <summary>工具结果（tool 角色）</summary>
    public List<ToolResultContent> ToolResults { get; set; } = [];
}

/// <summary>
/// OpenAI 格式的工具定义
/// </summary>
internal class OpenAiToolDefinition
{
    public string Type { get; set; } = "function";
    public OpenAiFunctionDefinition Function { get; set; } = new();
}

internal class OpenAiFunctionDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JsonElement Parameters { get; set; }
}
