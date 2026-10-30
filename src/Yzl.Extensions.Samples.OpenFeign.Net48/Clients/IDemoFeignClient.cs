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
    /// <remarks>
    /// ⚠️ 超时写在「客户端级」：Net48 拦截器虽然会把方法级 timeout 写进请求属性
    /// （<c>FeignRequestOptions.TimeoutKey</c>），但当前没有任何地方读取它，
    /// 弹性策略只取这里的客户端级 timeout。客户端级 2s &lt; 上游 3s，降级才会触发。
    /// </remarks>
    [FeignClient(name: "demo-api", url: "http://localhost:16600", fallback: typeof(DemoFeignClientFallback), timeout: 2000)]
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

        // 同步超时降级：上游 /api/timeout 睡 3 秒，客户端超时 2 秒 —— 降级必然触发
        // 方法级 timeout 在 Net48 形态下不生效（弹性策略只读客户端级），故继承此处的 2s
        [Get("/api/timeout", timeout: 2000)]
        string TimeoutWithFallback();

        // 异步超时降级：与上面同步版同一路径、同一超时，用来对照验证
        // 「Task 返回值失败时是否同样走 fallback」（修复前这条会抛 FeignClientException）
        [Get("/api/timeout", timeout: 2000)]
        Task<string> TimeoutWithFallbackAsync();
    }
}
