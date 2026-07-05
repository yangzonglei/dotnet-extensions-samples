using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Core.Filters;
using Yzl.Extensions.Samples.TestDashboard;

namespace Samples.Api.Controllers;

[ApiController]
[HttpRequestLog]
[TestDashboardInfo("🔧 HTTP 方法测试", Order = 2)]
[Route("api")]
public sealed class MethodsController : ControllerBase
{
    [HttpGet("headers")]
    public object Headers([FromHeader(Name = "X-Token")] string? token, [FromHeader(Name = "X-Demo-Global")] string? globalHeader) => new
    {
        token,
        globalHeader
    };

    [HttpGet("timeout")]
    public async Task<string> Timeout(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        return "timeout endpoint finished";
    }

    [HttpHead("methods/head")]
    public IActionResult Head()
    {
        Response.Headers.Append("X-Demo-Head", "ok");
        return Ok();
    }

    [HttpOptions("methods/options")]
    public IActionResult Options()
    {
        Response.Headers.Allow = "GET,POST,PUT,PATCH,DELETE,HEAD,OPTIONS,TRACE";
        return Ok(new { method = "OPTIONS", success = true });
    }

    [AcceptVerbs("TRACE")]
    [Route("methods/trace")]
    public object Trace() => new { method = "TRACE", path = Request.Path.Value };

    [HttpPost("body/string")]
    public async Task<object> StringBody()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        return new { length = body.Length, body };
    }

    [HttpPost("body/bytes")]
    public async Task<object> BytesBody()
    {
        using var memory = new MemoryStream();
        await Request.Body.CopyToAsync(memory);

        return new { length = memory.Length };
    }

    [HttpPost("body/stream")]
    public async Task<object> StreamBody()
    {
        using var memory = new MemoryStream();
        await Request.Body.CopyToAsync(memory);

        return new { length = memory.Length };
    }

    [HttpPost("body/http-content")]
    public async Task<object> HttpContentBody()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        return new
        {
            contentType = Request.ContentType,
            length = body.Length,
            body
        };
    }
}
