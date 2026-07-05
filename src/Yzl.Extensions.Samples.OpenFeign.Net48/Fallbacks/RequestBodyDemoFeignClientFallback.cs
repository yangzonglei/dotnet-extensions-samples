using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Yzl.Extensions.Samples.OpenFeign.Net48.Clients;
using Yzl.Extensions.Samples.OpenFeign.Net48.Models;

namespace Yzl.Extensions.Samples.OpenFeign.Net48.Fallbacks
{
    /// <summary>
    /// IRequestBodyDemoFeignClient 的容错实现。
    /// </summary>
    public sealed class RequestBodyDemoFeignClientFallback : IRequestBodyDemoFeignClient
    {
        public Task<UserDto> SendObject(CreateUserRequest request) =>
            Task.FromResult(new UserDto(0, request.Name ?? "fallback", request.Age, request.City));

        public Task<string> SendString(string body) =>
            Task.FromResult($"fallback-string: {body}");

        public Task<string> SendBytes(byte[] body) =>
            Task.FromResult("fallback-bytes");

        public Task<string> SendStream(Stream body) =>
            Task.FromResult("fallback-stream");

        public Task<string> SendHttpContent(StringContent content) =>
            Task.FromResult("fallback-http-content");
    }
}
