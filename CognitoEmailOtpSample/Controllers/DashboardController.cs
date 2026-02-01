using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CognitoEmailOtpSample.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ILogger<DashboardController> logger)
    {
        _logger = logger;
    }

    // GET: /Dashboard/Index
    public IActionResult Index()
    {
        var userName = User.Identity?.Name ?? "Guest";
        _logger.LogInformation("ダッシュボードにアクセス: {UserName}", userName);

        ViewData["UserName"] = userName;
        return View();
    }
}
