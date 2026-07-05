using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Samples.Models;
using Yzl.Extensions.Core.Filters;
using Yzl.Extensions.Samples.TestDashboard;

namespace Samples.Api.Controllers;

[ApiController]
[HttpRequestLog]
[TestDashboardInfo("🔴 SSE 流式传输", Order = 3)]
public sealed class SseController : ControllerBase
{
    [Route("api/sse/stream")]
    [HttpGet]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        for (var i = 1; i <= 3; i++)
        {
            var data = JsonSerializer.Serialize(new SseEventDto(i, $"SSE message {i}"));
            await Response.WriteAsync($"data: {data}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            await Task.Delay(200, cancellationToken);
        }

        var complete = JsonSerializer.Serialize(new SseEventDto(4, "complete", true));
        await Response.WriteAsync($"data: {complete}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    [Route("api/RandomChinese/stream")]
    [HttpGet]
    public async Task RandomChinese(CancellationToken cancellationToken)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        const string text = "你好世界，这是一段随机中文文本用于测试SSE流式传输";
        var totalChars = text.Length;
        const int batchSize = 3;
        var totalBatches = (int)Math.Ceiling((double)totalChars / batchSize);

        for (var batch = 0; batch < totalBatches; batch++)
        {
            var start = batch * batchSize;
            var len = Math.Min(batchSize, totalChars - start);
            var content = text.Substring(start, len);
            var progress = (double)(batch + 1) / totalBatches * 100;

            var dto = new RandomChineseSseDto
            {
                batchNumber = batch + 1,
                totalBatches = totalBatches,
                receivedChars = start + len,
                content = content,
                progress = Math.Round(progress, 1),
                completeSucc = false,
                totalChars = totalChars,
                fullText = ""
            };

            var json = JsonSerializer.Serialize(dto);
            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            await Task.Delay(300, cancellationToken);
        }

        // 发送完成事件
        var complete = new RandomChineseSseDto
        {
            batchNumber = totalBatches,
            totalBatches = totalBatches,
            receivedChars = totalChars,
            content = "",
            progress = 100,
            completeSucc = true,
            totalChars = totalChars,
            fullText = text
        };

        var completeJson = JsonSerializer.Serialize(complete);
        await Response.WriteAsync($"data: {completeJson}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
