using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;
using Samples.Models;
using Yzl.Extensions.Samples.Mcp.Service.Acs.FallbackHandler;

namespace Yzl.Extensions.Samples.Mcp.Service.Acs;

/// <summary>
/// OpenFeign 客户端，调用 Test.Api（http://localhost:16600）的接口
/// </summary>
[FeignClient(name: "test", url: "http://localhost:16600", fallback: typeof(TestApiFeignClientFallback), timeout: 5000)]
public interface ITestApiFeignClient
{
    [Get("/api/test/ping")]
    Task<string> Ping();

    [Get("/api/test/users/{id}")]
    Task<UserDto> GetByIdAsync([PathVariable("id")] long id);

    [Get("/api/test/users/{id}")]
    UserDto GetById([PathVariable("id")] long id);
}
