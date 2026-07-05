using Yzl.Extensions.Samples.OpenFeign.AOT.Acs.FallbackHandler;
using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;

namespace Yzl.Extensions.Samples.OpenFeign.AOT.Acs;

[FeignClient(name: "demo-api-aot-download", url: "{DemoApi:BaseUrl}", fallback: typeof(DownloadClientFallback), timeout: 5000)]
public interface IDownloadClient
{
    [Get("/api/download/files/abc.doc")]
    Task<Stream> DownloadAsync();

    [Get("/api/download/files/abc.doc")]
    Stream Download();

    [Get("/api/download/files/abc.doc")]
    Task<byte[]> DownloadBytesAsync();
}
