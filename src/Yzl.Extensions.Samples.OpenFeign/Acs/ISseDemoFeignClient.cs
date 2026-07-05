using Samples.Models;
using Yzl.Extensions.Http.OpenFeign.Attributes;
using Yzl.Extensions.Http.OpenFeign.Attributes.Methods;
using Yzl.Extensions.Http.OpenFeign.Sse;

namespace Yzl.Extensions.Samples.OpenFeign.Acs;

[FeignClient(name: "demo-api-sse", url: "{DemoApi:BaseUrl}", timeout: 10000)]
public interface ISseDemoFeignClient
{
    [Sse(CompleteField = "CompleteSucc")]
    [Get("/api/sse/stream")]
    IAsyncEnumerable<SseEventDto> StreamAsAsyncEnumerable();

    [Sse(CompleteField = "CompleteSucc")]
    [Get("/api/sse/stream")]
    ISseStream<SseEventDto> StreamAsSseStream();
}
