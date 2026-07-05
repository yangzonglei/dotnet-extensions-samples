using Yzl.Extensions.Http.OpenFeign.Headers;

namespace Yzl.Extensions.Samples.OpenFeign.HeaderProviders;

public sealed class DemoHeaderProvider : IFeignRequestHeaderProvider
{
    public int Order => -100;

    public void Apply(IDictionary<string, string> headers)
    {
        headers.TryAdd("X-Demo-Global", "from-header-provider");
        headers.TryAdd("Authorization", "Bearer demo-token");
    }
}
