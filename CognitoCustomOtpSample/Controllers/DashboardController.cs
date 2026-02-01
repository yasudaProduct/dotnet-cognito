using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CognitoCustomOtpSample.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ILogger<DashboardController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? "";
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "";

        ViewBag.Email = email;
        ViewBag.Username = username;

        return View();
    }
}
