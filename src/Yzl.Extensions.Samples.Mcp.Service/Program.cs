using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Yzl.Extensions.AI.Mcp;
using Yzl.Extensions.Http.OpenFeign;
using Yzl.Extensions.Http.OpenFeign.Serializer;
using Yzl.Extensions.Samples.Mcp.Service.Acs;

Console.WriteLine("""
                  ╔═══════════════════════════════════════════════════════╗
                  ║       Yzl.Extensions.AI.Mcp 服务端测试              ║
                  ║                                                      ║
                  ║     MCP 端点: http://localhost:16607/mcp              ║
                  ║     Token:    POST /api/auth/token                   ║
                  ║                                                      ║
                  ║     MCP 协议服务端演示                               ║
                  ║     JWT 认证 · 工具注册 · OpenFeign 集成            ║
                  ╚═══════════════════════════════════════════════════════╝
                  """);

var builder = WebApplication.CreateBuilder(args);

// ===== 注册 Yzl MCP 工具（扫描当前程序集中的 [McpTool] 标注类） =====
builder.Services.AddMcpTools(typeof(Program).Assembly);

// 配置 MCP Server
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
    })
    .WithYzlTools();

// ===== JWT 认证配置 =====
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSection.GetValue<string>("SecretKey")!;
var issuer = jwtSection.GetValue<string>("Issuer")!;
var audience = jwtSection.GetValue<string>("Audience")!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

builder.Services.AddAuthorization();

// ===== Yzl.Extensions.Http.OpenFeign（启用OpenFeign客户端支持） =====
builder.Services.AddFeignStarter(builder.Configuration, options =>
{
    options.SerializerType = typeof(SystemTextJsonFeignSerializer);
});

var app = builder.Build();

// ===== 认证与授权中间件（必须放在 MapMcp 之前） =====
app.UseAuthentication();
app.UseAuthorization();

// ===== 映射 MCP 端点（需要认证） =====
app.MapMcp("/mcp").RequireAuthorization();

// ===== Demo: 获取 JWT Token 的接口 =====
// 生产环境中应替换为标准的 OAuth 2.0 / OIDC 流程
app.MapPost("/api/auth/token", (TokenRequest request) =>
{
    // Demo 级别的简单校验，生产环境应使用 Identity 系统
    if (request.ClientId != "mcp-client" || request.ClientSecret != "mcp-secret")
    {
        return Results.Unauthorized();
    }

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, request.ClientId),
        new Claim(ClaimTypes.Role, "user"),
        new Claim("scope", "mcp:tools")
    };

    var expirationMinutes = jwtSection.GetValue<int>("ExpirationMinutes");
    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
        signingCredentials: credentials
    );

    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

    return Results.Ok(new TokenResponse
    {
        AccessToken = tokenString,
        TokenType = "Bearer",
        ExpiresIn = expirationMinutes * 60
    });
});

// ===== 测试 OpenFeign 调用 Test.Api =====
app.MapGet("/feign-test", async (ITestApiFeignClient client) => new
{
    ping = await client.Ping(),
    user = await client.GetByIdAsync(1),
});

app.MapGet("/", () => "Yzl MCP Server is running. MCP endpoint: /mcp (requires JWT Bearer token). Get token via POST /api/auth/token");

app.Run();

// ===== DTOs =====
public record TokenRequest(string ClientId, string ClientSecret);
public record TokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresIn { get; init; }
}
