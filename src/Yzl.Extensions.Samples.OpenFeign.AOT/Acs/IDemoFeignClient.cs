using Yzl.Extensions.Samples.OpenFeign.AOT.Acs.FallbackHandler;
using Samples.Models;
using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;

namespace Yzl.Extensions.Samples.OpenFeign.AOT.Acs;

// 降级演示客户端。
//
// ⚠️ AOT 源生成器只读取「客户端级」timeout：方法级 timeout 既不会出现在生成的代理里，
// 也不会进入 FeignAotRequestContext，属于被静默忽略的写法。
// 因此这里把客户端级 timeout 设为 2s —— 小于上游 /api/timeout 的 3s，降级才会真正触发。
[FeignClient(name: "demo-api-aot", url: "{DemoApi:BaseUrl}", fallback: typeof(DemoFeignClientFallback), timeout: 2000)]
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

    [Get("/api/users/{id}/status-result", RawFormat = false)]
    Task<UserDto> GetStatusResult([PathVariable("id")] long id);

    [Post("/api/users")]
    Task<UserDto> Create([RequestBody] CreateUserRequest payload);

    [Put("/api/users/{id}")]
    Task<UserDto> Update([PathVariable("id")] long id, [RequestBody] UpdateUserRequest payload);

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
    // 方法级 timeout 在 AOT 形态下不生效（源生成器只读客户端级），故继承此处的 2s
    [Get("/api/timeout", timeout: 2000)]
    string TimeoutWithFallback();

    // 异步超时降级：与上面同步版同一路径、同一超时，用来对照验证
    // 「Task 返回值失败时是否同样走 fallback」（修复前这条会抛 FeignClientException）
    [Get("/api/timeout", timeout: 2000)]
    Task<string> TimeoutWithFallbackAsync();
}
