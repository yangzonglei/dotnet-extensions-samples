using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Yzl.Extensions.Samples.Mcp.WebClient.Services;

/// <summary>
/// 管理 MCP 客户端连接，提供工具列表和调用能力。
/// 自动获取 JWT Token 并通过 Authorization 头传递。
/// </summary>
public class McpClientService : IAsyncDisposable
{
    private McpClient? _client;
    private readonly string _mcpServerUrl;
    private readonly McpAuthOptions _authOptions;
    private readonly ILogger<McpClientService> _logger;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly HttpClient _httpClient;

    public McpClientService(IConfiguration configuration, ILogger<McpClientService> logger)
    {
        _mcpServerUrl = configuration.GetValue<string>("McpServerUrl") ?? "http://localhost:16607/mcp";
        _authOptions = configuration.GetSection("McpAuth").Get<McpAuthOptions>()
                       ?? new McpAuthOptions();
        _logger = logger;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// 获取或初始化 MCP 客户端连接（自动携带 JWT Token）
    /// </summary>
    public async Task<McpClient> GetClientAsync()
    {
        if (_client != null) return _client;

        await _connectLock.WaitAsync();
        try
        {
            if (_client != null) return _client;

            // 1. 从认证服务器获取 JWT Token
            var token = await AcquireTokenAsync();

            // 2. 创建传输层，携带 Authorization 头
            _logger.LogInformation("正在连接 MCP 服务: {Url}", _mcpServerUrl);

            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(_mcpServerUrl),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string>
                {
                    { "Authorization", $"Bearer {token}" }
                }
            });

            // 3. 创建 MCP 客户端
            _client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions
                {
                    ClientInfo = new Implementation
                    {
                        Name = "Yzl MCP Web Chat Client",
                        Version = "1.0.0"
                    },
                });

            _logger.LogInformation("MCP 连接成功，服务器: {Name} v{Version}",
                _client.ServerInfo?.Name, _client.ServerInfo?.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "连接 MCP 服务失败: {Url}", _mcpServerUrl);
            throw;
        }
        finally
        {
            _connectLock.Release();
        }

        return _client;
    }

    /// <summary>
    /// 调用 MCP 服务器 Token 端点获取 JWT。
    /// </summary>
    private async Task<string> AcquireTokenAsync()
    {
        if (string.IsNullOrEmpty(_authOptions.TokenEndpoint) ||
            string.IsNullOrEmpty(_authOptions.ClientId))
        {
            _logger.LogWarning("McpAuth 未配置，使用空 Token（MCP 服务器可能拒绝请求）");
            return string.Empty;
        }

        _logger.LogInformation("正在获取 JWT Token: {Endpoint}", _authOptions.TokenEndpoint);

        var request = new TokenRequest
        {
            ClientId = _authOptions.ClientId,
            ClientSecret = _authOptions.ClientSecret ?? string.Empty
        };

        var response = await _httpClient.PostAsJsonAsync(_authOptions.TokenEndpoint, request);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        var token = tokenResponse?.AccessToken ?? string.Empty;

        _logger.LogInformation("JWT Token 获取成功，有效期: {ExpiresIn}s", tokenResponse?.ExpiresIn ?? 0);
        return token;
    }

    /// <summary>
    /// 列出所有可用工具（含 Schema）
    /// </summary>
    public async Task<List<McpClientTool>> ListToolsAsync()
    {
        var client = await GetClientAsync();
        return (await client.ListToolsAsync()).ToList();
    }

    /// <summary>
    /// 调用指定工具
    /// </summary>
    public async Task<CallToolResult> CallToolAsync(string toolName, Dictionary<string, object?>? args)
    {
        var client = await GetClientAsync();
        return await client.CallToolAsync(toolName, args);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await _client.DisposeAsync();
            _client = null;
        }
        _httpClient.Dispose();
        _connectLock.Dispose();
    }

    // ===== 内部 DTO =====

    private sealed class McpAuthOptions
    {
        public string? TokenEndpoint { get; init; }
        public string? ClientId { get; init; }
        public string? ClientSecret { get; init; }
    }

    private sealed record TokenRequest
    {
        [JsonPropertyName("clientId")]
        public string ClientId { get; init; } = string.Empty;

        [JsonPropertyName("clientSecret")]
        public string ClientSecret { get; init; } = string.Empty;
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("tokenType")]
        public string TokenType { get; init; } = string.Empty;

        [JsonPropertyName("expiresIn")]
        public int ExpiresIn { get; init; }
    }
}
