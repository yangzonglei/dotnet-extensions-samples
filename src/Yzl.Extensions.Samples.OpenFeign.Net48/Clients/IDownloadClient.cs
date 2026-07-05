using System.IO;
using System.Threading.Tasks;
using Yzl.Extensions.Samples.OpenFeign.Net48.Fallbacks;
using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;

namespace Yzl.Extensions.Samples.OpenFeign.Net48.Clients
{
    /// <summary>
    /// 声明式 HTTP 客户端 — 演示文件下载（Stream 同步/异步 和 byte[]）。
    /// </summary>
    [FeignClient(name: "demo-api-body", url: "http://localhost:16600", fallback: typeof(DownloadClientFallback), timeout: 5000)]
    public interface IDownloadClient
    {
        [Get("/api/download/files/abc.doc")]
        Task<Stream> DownloadAsync();

        [Get("/api/download/files/abc.doc")]
        Stream Download();

        [Get("/api/download/files/abc.doc")]
        Task<byte[]> DownloadBytesAsync();
    }
}
