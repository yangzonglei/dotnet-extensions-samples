using Yzl.Extensions.Samples.OpenFeign.AOT.Acs;
using Samples.Models;

namespace Yzl.Extensions.Samples.OpenFeign.AOT.Acs.FallbackHandler;

public sealed class DemoFeignClientFallback : IDemoFeignClient
{
    public Task<UserDto> GetById(long id) => Task.FromResult(new UserDto(id, "fallback-user", 0));

    public UserDto GetByIdSync(long id) => new(id, "fallback-user-sync", 0);

    public Task<object> Query(long id, string name) => Task.FromResult<object>(new { id, name, fallback = true });

    public Task<object> QueryMap(Dictionary<string, string> values) => Task.FromResult<object>(new { values, fallback = true });

    public Task<UserDto> GetWrappedData(long id) => Task.FromResult(new UserDto(id, "fallback-wrapped", 0));

    public Task<ResponseResult<UserDto>> GetWrappedRaw(long id) => Task.FromResult(new ResponseResult<UserDto>(0, new UserDto(id, "fallback-raw", 0), "fallback"));

    public Task<UserDto> GetStatusResult(long id) => Task.FromResult(new UserDto(id, "fallback-status", 0));

    public Task<UserDto> Create(CreateUserRequest payload) => Task.FromResult(new UserDto(0, payload.Name, payload.Age, payload.City));

    public Task<UserDto> Update(long id, UpdateUserRequest payload) => Task.FromResult(new UserDto(id, payload.Name, payload.Age, payload.City));

    public Task<UserDto> Patch(long id, Dictionary<string, object?> patch) => Task.FromResult(new UserDto(id, "fallback-patch", 0));

    public Task<bool> Delete(long id) => Task.FromResult(false);

    public Task<object> Headers(string token, string requestSource) => Task.FromResult<object>(new { token, requestSource, fallback = true });

    public Task Head() => Task.CompletedTask;

    public Task<object> Options() => Task.FromResult<object>(new { method = "OPTIONS", fallback = true });

    public Task<object> Trace() => Task.FromResult<object>(new { method = "TRACE", fallback = true });

    public string TimeoutWithFallback() => "fallback timeout";
}
