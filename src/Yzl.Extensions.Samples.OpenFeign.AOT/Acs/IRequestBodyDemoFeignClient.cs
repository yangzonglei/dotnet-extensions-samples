using System.Text;
using Yzl.Extensions.Samples.OpenFeign.AOT.Acs.FallbackHandler;
using Samples.Models;
using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;

namespace Yzl.Extensions.Samples.OpenFeign.AOT.Acs;

[FeignClient(name: "demo-api-aot-body", url: "{DemoApi:BaseUrl}", fallback: typeof(RequestBodyDemoFeignClientFallback), timeout: 5000)]
public interface IRequestBodyDemoFeignClient
{
    [Post("/api/users")]
    Task<UserDto> SendObject([RequestBody] CreateUserRequest payload);

    [Post("/api/body/string")]
    Task<object> SendString([RequestBody] string body);

    [Post("/api/body/bytes")]
    Task<object> SendBytes([RequestBody] byte[] body);

    [Post("/api/body/stream")]
    Task<object> SendStream([RequestBody] Stream body);

    [Post("/api/body/http-content")]
    Task<object> SendHttpContent([RequestBody] StringContent content);

    static StringContent CreateJsonContent(string json) => new(json, Encoding.UTF8, "application/json");
}
