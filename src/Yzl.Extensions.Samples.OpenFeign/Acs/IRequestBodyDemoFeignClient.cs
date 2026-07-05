using System.Text;
using Samples.Models;
using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;

namespace Yzl.Extensions.Samples.OpenFeign.Acs;

[FeignClient(name: "demo-api-body", url: "{DemoApi:BaseUrl}", timeout: 5000)]
public interface IRequestBodyDemoFeignClient
{
    [Post("/api/users")]
    Task<UserDto> SendObject([RequestBody] CreateUserRequest request);

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
