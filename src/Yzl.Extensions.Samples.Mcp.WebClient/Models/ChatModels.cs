using System.Text.Json;

namespace Yzl.Extensions.Samples.Mcp.WebClient.Models;

/// <summary>
/// 聊天请求
/// </summary>
public class ChatRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 聊天响应（SSE 中的事件）
/// </summary>
public class ChatResponseEvent
{
    /// <summary>事件类型: text, tool_call, tool_result, error, done</summary>
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object> ToolArgs { get; set; } = [];
    public string ToolResult { get; set; } = string.Empty;
}

/// <summary>
/// 向 LLM 注册的工具定义
/// </summary>
public class LlmToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JsonElement InputSchema { get; set; }
}

/// <summary>
/// 会话中的一条消息
/// </summary>
public class ConversationMessage
{
    public string Role { get; set; } = string.Empty;     // user, assistant
    public string Content { get; set; } = string.Empty;

    /// <summary>工具调用内容（assistant 的 tool_use）</summary>
    public List<ToolUseContent> ToolUses { get; set; } = [];

    /// <summary>工具结果内容（user 角色的 tool_result）</summary>
    public List<ToolResultContent> ToolResults { get; set; } = [];
}

public class ToolUseContent
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> Arguments { get; set; } = [];
}

public class ToolResultContent
{
    public string ToolUseId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
