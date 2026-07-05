using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Yzl.Extensions.Samples.OpenFeign.Net48.Clients;

namespace Yzl.Extensions.Samples.OpenFeign.Net48.Fallbacks
{
    /// <summary>
    /// IDownloadClient 的容错实现。
    /// </summary>
    public sealed class DownloadClientFallback : IDownloadClient
    {
        public Task<Stream> DownloadAsync() =>
            Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("fallback-download-content")));

        public Stream Download() =>
            new MemoryStream(Encoding.UTF8.GetBytes("fallback-download-content-sync"));

        public Task<byte[]> DownloadBytesAsync() =>
            Task.FromResult(Encoding.UTF8.GetBytes("fallback-download-bytes"));
    }
}
