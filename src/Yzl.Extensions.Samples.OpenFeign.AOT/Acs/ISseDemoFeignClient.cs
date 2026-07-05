using Yzl.Extensions.Samples.OpenFeign.AOT.Acs.FallbackHandler;
using Samples.Models;
using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;
using Yzl.Extensions.Http.OpenFeign.Sse;

namespace Yzl.Extensions.Samples.OpenFeign.AOT.Acs;

[FeignClient(name: "demo-api-aot-sse", url: "{DemoApi:BaseUrl}", fallback: typeof(SseDemoFeignClientFallback), timeout: 10000)]
public interface ISseDemoFeignClient
{
    [Sse(CompleteField = "CompleteSucc")]
    [Get("/api/sse/stream")]
    IAsyncEnumerable<SseEventDto> StreamAsAsyncEnumerable();

    [Sse(CompleteField = "CompleteSucc")]
    [Get("/api/sse/stream")]
    ISseStream<SseEventDto> StreamAsSseStream();
}
