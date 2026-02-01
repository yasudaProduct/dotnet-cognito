using Microsoft.AspNetCore.Mvc;
using CognitoSample.Filters;
using CognitoSample.Services;

namespace CognitoSample.Controllers;

[AuthRequired]
public class DashboardController : Controller
{
    private readonly ICognitoService _cognitoService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ICognitoService cognitoService, ILogger<DashboardController> logger)
    {
        _cognitoService = cognitoService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var accessToken = HttpContext.Session.GetString("AccessToken");
        var email = HttpContext.Session.GetString("UserEmail") ?? "";

        try
        {
            var user = await _cognitoService.GetUserAsync(accessToken!);
            ViewBag.Username = user.Username;
            ViewBag.Email = email;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user info");
            ViewBag.Username = email;
            ViewBag.Email = email;
        }

        return View();
    }

    public IActionResult SecretPage()
    {
        return View();
    }
}
