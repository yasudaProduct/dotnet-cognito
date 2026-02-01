using System.Security.Claims;
using CognitoEmailOtpSample.Models;
using CognitoEmailOtpSample.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace CognitoEmailOtpSample.Controllers;

public class AuthController : Controller
{
    private readonly ILocalAuthService _localAuthService;
    private readonly ICognitoEmailOtpService _cognitoService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ILocalAuthService localAuthService,
        ICognitoEmailOtpService cognitoService,
        ILogger<AuthController> logger)
    {
        _localAuthService = localAuthService;
        _cognitoService = cognitoService;
        _logger = logger;
    }

    // GET: /Auth/SignIn
    [HttpGet]
    public IActionResult SignIn()
    {
        return View();
    }

    // POST: /Auth/SignIn
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignIn(SignInViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            // ステップ1: ローカル認証（既存の独自パスワード認証）
            var isLocalAuthValid = await _localAuthService.ValidateCredentialsAsync(model.Email, model.Password);

            if (!isLocalAuthValid)
            {
                ModelState.AddModelError(string.Empty, "メールアドレスまたはパスワードが正しくありません");
                return View(model);
            }

            // ステップ2: Cognito Email OTP チャレンジを開始
            var (challengeName, session) = await _cognitoService.StartEmailOtpAsync(model.Email);

            _logger.LogInformation("ローカル認証成功、Email OTP チャレンジ開始: {Email}", model.Email);

            // OTP入力画面へ遷移
            return RedirectToAction(nameof(EmailOtpChallenge), new
            {
                email = model.Email,
                session = session
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "認証エラー: {Message}", ex.Message);
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "予期しないエラー");
            ModelState.AddModelError(string.Empty, "エラーが発生しました。しばらくしてから再度お試しください。");
            return View(model);
        }
    }

    // GET: /Auth/EmailOtpChallenge
    [HttpGet]
    public IActionResult EmailOtpChallenge(string email, string session)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(session))
        {
            return RedirectToAction(nameof(SignIn));
        }

        var model = new OtpChallengeViewModel
        {
            Email = email,
            Session = session
        };

        return View(model);
    }

    // POST: /Auth/EmailOtpChallenge
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailOtpChallenge(OtpChallengeViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            // Cognito Email OTP を検証
            var isOtpValid = await _cognitoService.VerifyEmailOtpAsync(
                model.Session,
                model.Email,
                model.OtpCode);

            if (!isOtpValid)
            {
                ModelState.AddModelError(string.Empty, "OTP コードが正しくありません");
                return View(model);
            }

            // OTP 検証成功 → Cookie 認証でサインイン
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, model.Email),
                new Claim(ClaimTypes.Email, model.Email)
            };

            var claimsIdentity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            _logger.LogInformation("ユーザー認証完了（Cookie認証）: {Email}", model.Email);

            return RedirectToAction("Index", "Dashboard");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "OTP検証エラー: {Message}", ex.Message);
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "予期しないエラー");
            ModelState.AddModelError(string.Empty, "エラーが発生しました。しばらくしてから再度お試しください。");
            return View(model);
        }
    }

    // POST: /Auth/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("ユーザーがサインアウトしました");
        return RedirectToAction(nameof(SignIn));
    }
}
