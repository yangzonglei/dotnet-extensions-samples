using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;
using Samples.Models;
using Yzl.Extensions.Samples.OpenFeign.Acs.FallbackHandler;
using Yzl.Extensions.Samples.OpenFeign.FeignImpl;
using Yzl.Extensions.Http.OpenFeign.Execution.ResponseResolver;
using Yzl.Extensions.Http.OpenFeign.Sse;

namespace Yzl.Extensions.Samples.OpenFeign.Acs;

[FeignClient(name: "test", url: "http://localhost:16600", fallback: typeof(TestApiFeignClientFallback), timeout: 5000)]
public interface ITestApiFeignClient
{
    [Get("/api/test/ping")]
    Task<string> Ping();

    [Get("/api/test/users/{id}")]
    Task<UserDto> GetById([PathVariable("id")] long id);

    [Get("/api/test/users/{id}")]
    UserDto GetByIdSync([PathVariable("id")] long id);

    /// <summary>
    /// 测试默认响应结果 测试默认 RawFormat=false 
    /// </summary>
    [Get("/api/test/users/{id}/getbyid2", RawFormat = false)]
    UserDto GetByIdSync2([PathVariable("id")] long id);

    /// <summary>
    /// 测试默认响应结果
    /// </summary>
    [Get("/api/test/users/{id}/getbyid2")]
    ResponseResult<UserDto> GetByIdSync3([PathVariable("id")] long id);

    /// <summary>
    /// 测试自定义响应结果 测试默认 RawFormat=false 
    /// </summary>
    [FeignResponse(typeof(ResultFeignResponseResolver))]
    [Get("/api/test/users/{id}/getbyid3", RawFormat = false)]
    UserDto GetByIdSync4([PathVariable("id")] long id);

    /// <summary>
    /// 测试自定义响应结果 测试默认 RawFormat=false 
    /// </summary>
    [Get("/api/test/users/{id}/getbyid3", RawFormat = false)]
    StatusResponseResult<UserDto> GetByIdSync5([PathVariable("id")] long id);

    [Get("/api/test/users/query")]
    Task<object> Query(
        [RequestParam("id")] long id,
        [RequestParam("name")] string name);

    [Get("/api/test/users/map")]
    Task<object> QueryMap([QueryMap] Dictionary<string, string> map);

    [Post("/api/test/users")]
    Task<UserDto> Create([RequestBody] CreateUserRequest req);

    [Put("/api/test/users/{id}")]
    Task<UserDto> Update(
        [PathVariable("id")] long id,
        [RequestBody] UpdateUserRequest req);

    [Delete("/api/test/users/{id}")]
    Task<bool> Delete([PathVariable("id")] long id);

    [Get("/api/test/headers")]
    Task<object> Headers([RequestHeader("X-Token")] string token);

    [Head("/api/test/head")]
    Task Head();

    [Get("/api/test/files/abc.doc")]
    Task<Stream> DownloadAsync();

    [Get("/api/test/files/abc.doc")]
    Stream Download();

    [Get("/api/test/files/abc.doc")]
    Task<byte[]> DownloadBytesAsync();

    [Get("/api/test/timeout", timeout: 7000)]
    string TimeOut([RequestHeader] string a = "123", [RequestHeader(name: "user-token", Encoded = true)] string utk = "你好");

    [Sse(CompleteField = "completeSucc")]
    [Get("/api/RandomChinese/stream")]
    IAsyncEnumerable<RandomChineseSseDto> RandomChinese();

    [Sse(CompleteField = "completeSucc")]
    [Get("/api/RandomChinese/stream")]
    ISseStream<RandomChineseSseDto> RandomChinese2();

    /// <summary>
    ///  测试 ResponseResult 没有 data 属性
    /// </summary>
    [Get("/api/test/users/{id}/not-data", RawFormat = false)]
    int NotData2Int([PathVariable("id")] long id);

    /// <summary>
    ///  测试 ResponseResult 没有 data 属性
    /// </summary>
    [Get("/api/test/users/{id}/not-data", RawFormat = false)]
    UserDto NotData2Obj([PathVariable("id")] long id);
}
