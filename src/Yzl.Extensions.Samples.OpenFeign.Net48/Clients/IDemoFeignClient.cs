using System.Collections.Generic;
using System.Threading.Tasks;
using Yzl.Extensions.Samples.OpenFeign.Net48.Fallbacks;
using Yzl.Extensions.Samples.OpenFeign.Net48.Models;
using Yzl.Extensions.Samples.OpenFeign.Net48.Resolvers;
using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;
using Yzl.Extensions.Http.OpenFeign.Execution.ResponseResolver;

namespace Yzl.Extensions.Samples.OpenFeign.Net48.Clients
{
    /// <summary>
    /// 声明式 HTTP 客户端 — 演示 CRUD、查询、请求头、自定义解析器、超时熔断等核心功能。
    /// </summary>
    [FeignClient(name: "demo-api", url: "http://localhost:16600", fallback: typeof(DemoFeignClientFallback), timeout: 5000)]
    public interface IDemoFeignClient
    {
        [Get("/api/users/{id}")]
        Task<UserDto> GetById([PathVariable("id")] long id);

        [Get("/api/users/{id}")]
        UserDto GetByIdSync([PathVariable("id")] long id);

        [Get("/api/users/query")]
        Task<object> Query([RequestParam("id")] long id, [RequestParam("name")] string name);

        [Get("/api/users/map")]
        Task<object> QueryMap([QueryMap] Dictionary<string, string> values);

        [Get("/api/users/{id}/wrapped", RawFormat = false)]
        Task<UserDto> GetWrappedData([PathVariable("id")] long id);

        [Get("/api/users/{id}/wrapped")]
        Task<ResponseResult<UserDto>> GetWrappedRaw([PathVariable("id")] long id);

        [FeignResponse(typeof(StatusResponseResolver))]
        [Get("/api/users/{id}/status-result", RawFormat = false)]
        Task<UserDto> GetStatusResult([PathVariable("id")] long id);

        [Post("/api/users")]
        Task<UserDto> Create([RequestBody] CreateUserRequest request);

        [Put("/api/users/{id}")]
        Task<UserDto> Update([PathVariable("id")] long id, [RequestBody] UpdateUserRequest request);

        [Patch("/api/users/{id}")]
        Task<UserDto> Patch([PathVariable("id")] long id, [RequestBody] Dictionary<string, object?> patch);

        [Delete("/api/users/{id}")]
        Task<bool> Delete([PathVariable("id")] long id);

        [Get("/api/headers")]
        Task<object> Headers([RequestHeader("X-Token")] string token, [RequestHeader] string requestSource = "manual-header");

        [Head("/api/methods/head")]
        Task Head();

        [Options("/api/methods/options")]
        Task<object> Options();

        [Trace("/api/methods/trace")]
        Task<object> Trace();

        [Get("/api/timeout", timeout: 1000)]
        string TimeoutWithFallback();
    }
}
