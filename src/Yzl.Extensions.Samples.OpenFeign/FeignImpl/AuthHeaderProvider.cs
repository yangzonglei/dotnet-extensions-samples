using Yzl.Extensions.Http.OpenFeign.Headers;

namespace Yzl.Extensions.Samples.OpenFeign.FeignImpl;

public class AuthHeaderProvider : IFeignRequestHeaderProvider
{
    public int Order => -100;

    public void Apply(IDictionary<string, string> headers)
    {
        headers["Authorization"] = "Bearer " + GetToken();
        headers.TryAdd("source", "PC111");
    }

    private string GetToken() => "xxx.yyy.zzz";
}
