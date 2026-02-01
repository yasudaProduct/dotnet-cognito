using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using CognitoCustomOtpSample.Models;
using CognitoCustomOtpSample.Services;

namespace CognitoCustomOtpSample.Controllers;

public class AuthController : Controller
{
    private readonly ILocalAuthService _localAuthService;
    private readonly ICognitoCustomOtpService _cognitoService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ILocalAuthService localAuthService,
        ICognitoCustomOtpService cognitoService,
        ILogger<AuthController> logger)
    {
        _localAuthService = localAuthService;
        _cognitoService = cognitoService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult SignIn()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SignIn(SignInViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            // Step 1: 独自認証でメールアドレスとパスワードを検証
            var isValid = await _localAuthService.ValidateCredentialsAsync(model.Email, model.Password);
            if (!isValid)
            {
                ModelState.AddModelError("", "メールアドレスまたはパスワードが正しくありません。");
                return View(model);
            }

            _logger.LogInformation("Local authentication successful for user: {Email}", model.Email);

            // Step 2: 独自認証が成功したら、Cognito CUSTOM_AUTH で OTP 送信を開始
            var (session, challengeName) = await _cognitoService.StartCustomOtpAsync(model.Email);

            if (challengeName != "CUSTOM_CHALLENGE")
            {
                _logger.LogError("Unexpected challenge: {ChallengeName}", challengeName);
                ModelState.AddModelError("", "認証処理中にエラーが発生しました。");
                return View(model);
            }

            _logger.LogInformation("OTP challenge started for user: {Email}", model.Email);
            TempData["InfoMessage"] = "ワンタイムパスワードをメールに送信しました。";

            // OTP 入力画面へリダイレクト
            return RedirectToAction("OtpChallenge", new { session, email = model.Email });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sign in failed for user: {Email}", model.Email);
            ModelState.AddModelError("", "ログイン中にエラーが発生しました。");
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult OtpChallenge(string session, string email)
    {
        if (string.IsNullOrEmpty(session) || string.IsNullOrEmpty(email))
        {
            return RedirectToAction("SignIn");
        }

        return View(new OtpChallengeViewModel
        {
            Session = session,
            Email = email
        });
    }

    [HttpPost]
    public async Task<IActionResult> OtpChallenge(OtpChallengeViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            // OTP を検証
            var isValid = await _cognitoService.VerifyCustomOtpAsync(model.Session, model.Email, model.OtpCode);

            if (!isValid)
            {
                ModelState.AddModelError("OtpCode", "ワンタイムパスワードが正しくありません。");
                return View(model);
            }

            _logger.LogInformation("OTP verification successful for user: {Email}", model.Email);

            // Cookie 認証でサインイン
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, model.Email),
                new Claim(ClaimTypes.Email, model.Email)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            TempData["SuccessMessage"] = "ログインしました。";
            return RedirectToAction("Index", "Dashboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OTP verification failed for user: {Email}", model.Email);
            ModelState.AddModelError("", "認証中にエラーが発生しました。");
            return View(model);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["SuccessMessage"] = "ログアウトしました。";
        return RedirectToAction("SignIn");
    }
}
