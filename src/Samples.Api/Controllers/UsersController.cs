using Microsoft.AspNetCore.Mvc;
using Samples.Models;
using Yzl.Extensions.Core.Filters;
using Yzl.Extensions.Samples.TestDashboard;

namespace Samples.Api.Controllers;

[ApiController]
[HttpRequestLog]
[TestDashboardInfo("👥 用户 CRUD", Order = 0)]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    [HttpGet("{id:long}")]
    public UserDto GetById(long id) => new(id, "Alice", 20, "Shanghai");

    [HttpGet("query")]
    public object Query([FromQuery] long id, [FromQuery] string name) => new { id, name, source = "RequestParam" };

    [HttpGet("map")]
    public IDictionary<string, string?> QueryMap() => Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString());

    [HttpGet("{id:long}/wrapped")]
    public ResponseResult<UserDto> GetWrapped(long id) => new(0, new UserDto(id, "Wrapped Alice", 21, "Hangzhou"), "success");

    [HttpGet("{id:long}/status-result")]
    public StatusResponseResult<UserDto> GetStatusResult(long id) => new("success", new UserDto(id, "Status Alice", 22, "Shenzhen"), "ok");

    [HttpPost]
    public UserDto Create(CreateUserRequest request) => new(1001, request.Name, request.Age, request.City);

    [HttpPut("{id:long}")]
    public UserDto Update(long id, UpdateUserRequest request) => new(id, request.Name, request.Age, request.City);

    [HttpPatch("{id:long}")]
    public UserDto Patch(long id, [FromBody] Dictionary<string, object?> patch)
    {
        var name = patch.TryGetValue("name", out var nameValue) ? nameValue?.ToString() ?? "Patched Alice" : "Patched Alice";
        var city = patch.TryGetValue("city", out var cityValue) ? cityValue?.ToString() : "Beijing";

        return new UserDto(id, name, 23, city);
    }

    [HttpDelete("{id:long}")]
    public bool Delete(long id) => id > 0;
}
