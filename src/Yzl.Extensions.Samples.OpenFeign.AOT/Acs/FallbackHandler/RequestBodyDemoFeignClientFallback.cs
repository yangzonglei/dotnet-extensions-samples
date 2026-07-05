using Yzl.Extensions.Samples.OpenFeign.AOT.Acs;
using Samples.Models;

namespace Yzl.Extensions.Samples.OpenFeign.AOT.Acs.FallbackHandler;

public sealed class RequestBodyDemoFeignClientFallback : IRequestBodyDemoFeignClient
{
    public Task<UserDto> SendObject(CreateUserRequest payload) => Task.FromResult(new UserDto(0, payload.Name, payload.Age, payload.City));

    public Task<object> SendString(string body) => Task.FromResult<object>(new { body, fallback = true });

    public Task<object> SendBytes(byte[] body) => Task.FromResult<object>(new { length = body.Length, fallback = true });

    public Task<object> SendStream(Stream body) => Task.FromResult<object>(new { fallback = true });

    public Task<object> SendHttpContent(StringContent content) => Task.FromResult<object>(new { fallback = true });
}
