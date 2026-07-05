using Microsoft.AspNetCore.Mvc;

namespace Yzl.Extensions.Samples.SpringBoot.Admin.Net.Controllers;

public class TestController : Controller
{
    public IActionResult Index()
    {
        return Json("aaa");
    }
}
