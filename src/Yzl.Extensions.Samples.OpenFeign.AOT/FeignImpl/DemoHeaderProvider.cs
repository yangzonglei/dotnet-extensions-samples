using Yzl.Extensions.Http.OpenFeign.Headers;

namespace Yzl.Extensions.Samples.OpenFeign.AOT.FeignImpl;

public sealed class DemoHeaderProvider : IFeignRequestHeaderProvider
{
    public int Order => -100;

    public void Apply(IDictionary<string, string> headers)
    {
        headers.TryAdd("X-Demo-Global", "from-aot-header-provider");
        headers.TryAdd("Authorization", "Bearer demo-aot-token");
    }
}
