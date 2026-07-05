using Samples.Models;
using Yzl.Extensions.Http.OpenFeign.Sse;

namespace Yzl.Extensions.Samples.OpenFeign.Acs.FallbackHandler;

public class TestApiFeignClientFallback : ITestApiFeignClient
{
    public Task<string> Ping()
    {
        throw new NotImplementedException();
    }

    public Task<UserDto> GetById(long id)
    {
        throw new NotImplementedException();
    }

    public UserDto GetByIdSync(long id)
    {
        throw new NotImplementedException();
    }

    public UserDto GetByIdSync2(long id)
    {
        throw new NotImplementedException();
    }

    public ResponseResult<UserDto> GetByIdSync3(long id)
    {
        throw new NotImplementedException();
    }

    public UserDto GetByIdSync4(long id)
    {
        throw new NotImplementedException();
    }

    public StatusResponseResult<UserDto> GetByIdSync5(long id)
    {
        throw new NotImplementedException();
    }

    public Task<object> Query(long id, string name)
    {
        throw new NotImplementedException();
    }

    public Task<object> QueryMap(Dictionary<string, string> map)
    {
        throw new NotImplementedException();
    }

    public Task<UserDto> Create(CreateUserRequest req)
    {
        throw new NotImplementedException();
    }

    public Task<UserDto> Update(long id, UpdateUserRequest req)
    {
        throw new NotImplementedException();
    }

    public Task<bool> Delete(long id)
    {
        throw new NotImplementedException();
    }

    public Task<object> Headers(string token)
    {
        throw new NotImplementedException();
    }

    public Task Head()
    {
        throw new NotImplementedException();
    }

    public Task<Stream> DownloadAsync()
    {
        throw new NotImplementedException();
    }

    public Stream Download()
    {
        throw new NotImplementedException();
    }

    public Task<byte[]> DownloadBytesAsync()
    {
        throw new NotImplementedException();
    }

    public string TimeOut(string a, string utk)
    {
        return "fallback timeout";
    }

    public IAsyncEnumerable<RandomChineseSseDto> RandomChinese()
    {
        throw new NotImplementedException();
    }

    public ISseStream<RandomChineseSseDto> RandomChinese2()
    {
        throw new NotImplementedException();
    }

    public int NotData2Int(long id)
    {
        return -888;
    }

    public UserDto NotData2Obj(long id)
    {
        return new UserDto(1, "fallback", 123);
    }
}
