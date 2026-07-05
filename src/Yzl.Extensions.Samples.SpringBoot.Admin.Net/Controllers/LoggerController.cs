using Microsoft.AspNetCore.Mvc;
using NLog;

namespace Yzl.Extensions.Samples.SpringBoot.Admin.Net.Controllers;

public class LoggerController(ILogger<LoggerController> logger) : Controller
{
    public IActionResult Index(string str = "")
    {
        logger.LogWarning("LoggerController.Index.LogWarning--{Str}", str);
        logger.LogInformation("LoggerController.Index.LogInformation--{Str}", str);
        logger.LogDebug("LoggerController.Index.LogDebug--{Str}", str);

        return Json(true);
    }

    public IActionResult GetLogger()
    {
        var loggingConfiguration = LogManager.Configuration;

        return Json(loggingConfiguration.LoggingRules);
    }
}
