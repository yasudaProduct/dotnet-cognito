using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using CognitoMfaSample.Filters;
using CognitoMfaSample.Services;

namespace CognitoMfaSample.Controllers;

[AuthRequired]
public class DashboardController : Controller
{
    private readonly IUserService _userService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IUserService userService, ILogger<DashboardController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? "";
        var user = await _userService.GetUserByEmailAsync(email);

        ViewBag.Email = user?.Email ?? email;
        return View();
    }

    public IActionResult SecretPage()
    {
        return View();
    }
}
