using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Samples.Models;
using Yzl.Extensions.Samples.OpenFeign.Acs;
using Yzl.Extensions.Samples.TestDashboard;

namespace Yzl.Extensions.Samples.OpenFeign.Controllers;

[Route("api/[controller]")]
[TestDashboardInfo("🏗️ Controller API (api/Test)", Order = 9, Badge = "控制器")]
public class TestController(ITestApiFeignClient client) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var query = await client.Query(1, "Tom");

        return Ok(query);
    }

    [HttpGet("timeout")]
    public IActionResult TestTimeout()
    {
        var timeOut = client.TimeOut();

        return Ok(timeOut);
    }

    [HttpGet("get_id_async")]
    public async Task<IActionResult> GetByIdAsync()
    {
        var data = await client.GetById(1);

        return Ok(data);
    }

    [HttpGet("get_id_sync")]
    public IActionResult GetByIdSync()
    {
        var data = client.GetByIdSync(1);

        return Ok(data);
    }

    [HttpGet("get_id_sync2")]
    public IActionResult GetByIdSync2(int userId)
    {
        var data = client.GetByIdSync2(userId);

        if (data == null)
        {
            return Content("GetByIdSync2 result is null");
        }

        return Ok(data);
    }

    [HttpGet("get_id_sync3")]
    public IActionResult GetByIdSync3(int userId)
    {
        var data = client.GetByIdSync3(userId);

        if (data == null)
        {
            return Content("GetByIdSync2 result is null");
        }

        return Ok(data);
    }

    [HttpGet("get_id_sync4")]
    public IActionResult GetByIdSync4(int userId)
    {
        var data = client.GetByIdSync4(userId);

        if (data == null)
        {
            return Content("GetByIdSync4 result is null");
        }

        return Ok(data);
    }

    [HttpGet("get_id_sync5")]
    public IActionResult GetByIdSync5(int userId)
    {
        var data = client.GetByIdSync5(userId);

        if (data == null)
        {
            return Content("GetByIdSync5 result is null");
        }

        return Ok(data);
    }

    [HttpGet("stream-proxy")]
    public async Task StreamProxy(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");

        await foreach (var item in client.RandomChinese().WithCancellation(cancellationToken))
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(item);

            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpGet("stream-proxy1")]
    public async Task StreamProxy1(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");

        var options = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        await foreach (var item in client.RandomChinese2().WithCancellation(cancellationToken))
        {
            var json = System.Text.Json.JsonSerializer.Serialize(item, options);

            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpGet("download-async")]
    public async Task<IActionResult> DownloadAsync(CancellationToken cancellationToken)
    {
        await using var stream = await client.DownloadAsync();
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken);

        return Content(content);
    }

    [HttpGet("download-sync")]
    public IActionResult Download()
    {
        using var stream = client.Download();
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        return Content(content);
    }

    [HttpGet("download-bytes")]
    public async Task<IActionResult> DownloadBytesAsync()
    {
        var bytes = await client.DownloadBytesAsync();
        var content = System.Text.Encoding.UTF8.GetString(bytes);

        return Content(content);
    }

    /// <summary>
    /// 测试 ResponseResult 没有 data 属性
    /// </summary>
    [HttpGet("users/{id}/not-data")]
    public int NotData2Int(long id)
    {
        return client.NotData2Int(id);
    }

    /// <summary>
    /// 测试 ResponseResult 没有 data 属性
    /// </summary>
    [HttpGet("users/{id}/not-data1")]
    public UserDto NotData2Obj(long id)
    {
        return client.NotData2Obj(id) ?? new UserDto(1, "NotData2Obj data属性找不到", 12);
    }
}
