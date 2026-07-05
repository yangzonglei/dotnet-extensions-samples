using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;

namespace Yzl.Extensions.Samples.OpenFeign.Acs;

[FeignClient(name: "demo-api-body", url: "{DemoApi:BaseUrl}", timeout: 5000)]
public interface IDownloadClient
{
    [Get("/api/download/files/abc.doc")]
    Task<Stream> DownloadAsync();

    [Get("/api/download/files/abc.doc")]
    Stream Download();

    [Get("/api/download/files/abc.doc")]
    Task<byte[]> DownloadBytesAsync();
}
