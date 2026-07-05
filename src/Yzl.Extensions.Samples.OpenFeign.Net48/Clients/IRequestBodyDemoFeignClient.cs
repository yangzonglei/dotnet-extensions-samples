using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Yzl.Extensions.Samples.OpenFeign.Net48.Fallbacks;
using Yzl.Extensions.Samples.OpenFeign.Net48.Models;
using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;

namespace Yzl.Extensions.Samples.OpenFeign.Net48.Clients
{
    /// <summary>
    /// 声明式 HTTP 客户端 — 演示五种不同的请求体类型：DTO、string、byte[]、Stream、StringContent。
    /// </summary>
    [FeignClient(name: "demo-api-body", url: "http://localhost:16600", fallback: typeof(RequestBodyDemoFeignClientFallback), timeout: 5000)]
    public interface IRequestBodyDemoFeignClient
    {
        [Post("/api/users")]
        Task<UserDto> SendObject([RequestBody] CreateUserRequest request);

        [Post("/api/body/string")]
        Task<string> SendString([RequestBody] string body);

        [Post("/api/body/bytes")]
        Task<string> SendBytes([RequestBody] byte[] body);

        [Post("/api/body/stream")]
        Task<string> SendStream([RequestBody] Stream body);

        [Post("/api/body/http-content")]
        Task<string> SendHttpContent([RequestBody] StringContent content);
    }

    /// <summary>
    /// IRequestBodyDemoFeignClient 的辅助方法（.NET 4.8 不支持接口默认实现）
    /// </summary>
    public static class RequestBodyDemoFeignClientHelper
    {
        public static StringContent CreateJsonContent(string json) => new(json, Encoding.UTF8, "application/json");
    }
}
