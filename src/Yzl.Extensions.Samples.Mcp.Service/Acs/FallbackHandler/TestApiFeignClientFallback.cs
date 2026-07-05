using Samples.Models;

namespace Yzl.Extensions.Samples.Mcp.Service.Acs.FallbackHandler;

/// <summary>
/// TestApi 调用失败的降级处理
/// </summary>
public class TestApiFeignClientFallback : ITestApiFeignClient
{
    public Task<string> Ping()
    {
        return Task.FromResult("fallback ping");
    }

    public Task<UserDto> GetByIdAsync(long id)
    {
        throw new NotImplementedException();
    }

    UserDto ITestApiFeignClient.GetById(long id)
    {
        throw new NotImplementedException();
    }

    public Task<UserDto> GetById(long id)
    {
        return Task.FromResult(new UserDto(id, "fallback user", 0));
    }
}
