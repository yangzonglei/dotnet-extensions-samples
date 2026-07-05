using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Samples.Models;
using Yzl.Extensions.Core.Filters;
using Yzl.Extensions.Samples.TestDashboard;

namespace Samples.Api.Controllers;

[ApiController]
[HttpRequestLog]
[TestDashboardInfo("🧪 综合测试", Order = 1)]
[Route("api/test")]
public sealed class TestController : ControllerBase
{
    [HttpGet("ping")]
    public string Ping() => "pong";

    [HttpGet("users/{id:long}")]
    public UserDto GetById(long id) => new(id, "Alice", 20, "Shanghai");

    [HttpGet("users/{id:long}/getbyid2")]
    public ResponseResult<UserDto> GetById2(long id)
        => new(0, new UserDto(id, "GetById2 User", 25), "success");

    [HttpGet("users/{id:long}/getbyid3")]
    public object GetById3(long id)
        => new { status = 200, result = new UserDto(id, "GetById3 User", 26), message = "ok" };

    [HttpGet("users/query")]
    public object Query([FromQuery] long id, [FromQuery] string name)
        => new { id, name, source = "RequestParam" };

    [HttpGet("users/map")]
    public IDictionary<string, string?> QueryMap()
        => Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString());

    [HttpPost("users")]
    public UserDto Create(CreateUserRequest request)
        => new(2001, request.Name, request.Age, request.City);

    [HttpPut("users/{id:long}")]
    public UserDto Update(long id, UpdateUserRequest request)
        => new(id, request.Name, request.Age, request.City);

    [HttpDelete("users/{id:long}")]
    public bool Delete(long id) => id > 0;

    [HttpGet("users/{id:long}/not-data")]
    public object NotData(long id)
        => new { code = 0, info = new UserDto(id, "NoDataField User", 30), msg = "no data field" };

    [HttpGet("headers")]
    public object Headers([FromHeader(Name = "X-Token")] string? token)
        => new { token, requestSource = "header" };

    [HttpGet("timeout")]
    public async Task<string> Timeout(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        return "timeout endpoint finished";
    }

    [HttpHead("head")]
    public IActionResult Head()
    {
        Response.Headers.Append("X-Demo-Head", "ok");
        return Ok();
    }

    [HttpGet("files/abc.doc")]
    public FileContentResult DownloadFile()
    {
        var bytes = Encoding.UTF8.GetBytes("OpenFeign file download test content.");
        return File(bytes, "application/msword", "abc.doc");
    }
}
