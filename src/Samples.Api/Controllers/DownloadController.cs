using System.Text;
using Microsoft.AspNetCore.Mvc;
using Yzl.Extensions.Core.Filters;
using Yzl.Extensions.Samples.TestDashboard;

namespace Samples.Api.Controllers;

[Route("api/download")]
[HttpRequestLog]
[TestDashboardInfo("📥 文件下载", Order = 4)]
public class DownloadController : ControllerBase
{
    /// <summary>
    ///  测试文件下载
    /// </summary>
    /// <returns></returns>
    [HttpGet("files/abc.doc")]
    public FileContentResult DownloadFile()
    {
        var bytes = Encoding.UTF8.GetBytes("OpenFeign file download test content.");
        return File(bytes, "application/msword", "abc.doc");
    }
}
