using Yzl.Extensions.Samples.Mcp.WebClient.Models;

namespace Yzl.Extensions.Samples.Mcp.WebClient.Services;

/// <summary>
/// LLM 聊天服务接口，支持多种 LLM 提供商（Claude、OpenAI 兼容等）。
/// </summary>
public interface IChatService
{
    /// <summary>API Key 是否已配置</summary>
    bool IsConfigured { get; }

    /// <summary>处理用户消息，流式返回 AI 回复（含自动工具调用）</summary>
    IAsyncEnumerable<ChatResponseEvent> ProcessMessageAsync(
        string sessionId, string userMessage, CancellationToken ct = default);

    /// <summary>获取会话历史</summary>
    List<object>? GetSessionHistory(string sessionId);

    /// <summary>清除会话历史</summary>
    void ClearSession(string sessionId);
}
