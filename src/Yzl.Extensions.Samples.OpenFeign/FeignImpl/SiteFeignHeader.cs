using Yzl.Extensions.Http.OpenFeign.Headers;

namespace Yzl.Extensions.Samples.OpenFeign.FeignImpl;

public class SiteFeignHeader : IFeignRequestHeaderProvider
{
    public void Apply(IDictionary<string, string> headers)
    {
        headers.TryAdd("source", "PC");
        
        headers.Add("a1", "a");
    }
}
