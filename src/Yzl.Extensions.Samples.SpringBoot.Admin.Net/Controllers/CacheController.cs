using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Yzl.Extensions.Samples.SpringBoot.Admin.Net.Controllers;

public class CacheController(IMemoryCache memoryCache) : Controller
{
    [HttpGet]
    public IActionResult AddCache(string name)
    {
        Random random = new Random();
        int randomNumber = random.Next();

        memoryCache.Set(name, randomNumber, TimeSpan.FromDays(1));

        return Json(true);
    }

    [HttpGet]
    public IActionResult GetCache(string name)
    {
        var o = memoryCache.Get(name);

        return Json(o);
    }
}
