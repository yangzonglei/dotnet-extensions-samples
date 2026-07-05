using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Yzl.Extensions.Samples.OpenFeign.Net48.Clients;
using Yzl.Extensions.Samples.OpenFeign.Net48.Models;

namespace Yzl.Extensions.Samples.OpenFeign.Net48.Fallbacks
{
    /// <summary>
    /// IDemoFeignClient 的容错实现 — 当请求超时或网络异常时返回默认数据。
    /// </summary>
    public sealed class DemoFeignClientFallback : IDemoFeignClient
    {
        public Task<UserDto> GetById(long id) =>
            Task.FromResult(new UserDto(id, "fallback-user", 0));

        public UserDto GetByIdSync(long id) =>
            new(id, "fallback-user-sync", 0);

        public Task<object> Query(long id, string name) =>
            Task.FromResult<object>(new { id, name, fallback = true });

        public Task<object> QueryMap(Dictionary<string, string> values) =>
            Task.FromResult<object>(new { values, fallback = true });

        public Task<UserDto> GetWrappedData(long id) =>
            Task.FromResult(new UserDto(id, "fallback-wrapped", 0));

        public Task<ResponseResult<UserDto>> GetWrappedRaw(long id) =>
            Task.FromResult(new ResponseResult<UserDto> { Code = 0, Data = new UserDto(id, "fallback-raw", 0), Msg = "fallback" });

        public Task<UserDto> GetStatusResult(long id) =>
            Task.FromResult(new UserDto(id, "fallback-status", 0));

        public Task<UserDto> Create(CreateUserRequest request) =>
            Task.FromResult(new UserDto(0, request.Name ?? "fallback", request.Age, request.City));

        public Task<UserDto> Update(long id, UpdateUserRequest request) =>
            Task.FromResult(new UserDto(id, request.Name ?? "fallback", request.Age, request.City));

        public Task<UserDto> Patch(long id, Dictionary<string, object?> patch) =>
            Task.FromResult(new UserDto(id, "fallback-patch", 0));

        public Task<bool> Delete(long id) =>
            Task.FromResult(false);

        public Task<object> Headers(string token, string requestSource) =>
            Task.FromResult<object>(new { token, requestSource, fallback = true });

        public Task Head() => Task.CompletedTask;

        public Task<object> Options() =>
            Task.FromResult<object>(new { method = "OPTIONS", fallback = true });

        public Task<object> Trace() =>
            Task.FromResult<object>(new { method = "TRACE", fallback = true });

        public string TimeoutWithFallback() => "fallback timeout";
    }
}
